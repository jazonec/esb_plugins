using ESB_ConnectionPoints.PluginsInterfaces;
using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Ceen;
using Ceen.Httpd;
using Ceen.Httpd.Handler;
using Newtonsoft.Json.Linq;

namespace openplugins.AESServerClient
{
    internal class IngoingRESTServer : IStandartIngoingConnectionPoint
    {
        private readonly ESB_ConnectionPoints.PluginsInterfaces.ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private readonly bool _debugMode;

        private readonly ServerSettings _serverSettings;

        private readonly EncryptTools _encryptTools;

        public IngoingRESTServer(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();
            _debugMode = (bool)settings["debug"];

            _serverSettings = new ServerSettings((JObject)settings["server"]);

            _encryptTools = new EncryptTools((JObject)settings["rsa"]);
        }

        public void Cleanup()
        {
        }

        public void Dispose()
        {
        }

        public void Initialize()
        {
        }

        public void Run(IMessageHandler messageHandler, CancellationToken ct)
        {
            ServerConfig _serverConfig;

            WriteLogString("Приступаю к инициализации сервера, порт:" + _serverSettings.Port);
            try
            {
                _serverConfig = new ServerConfig()
                    .AddRoute(_serverSettings.Path, new MainHandler(messageHandler, _messageFactory, _logger, _encryptTools, _serverSettings.Path, _debugMode))
                    .AddRoute(_serverSettings.Path + "/*", new RSAHandler(_logger, _encryptTools, _serverSettings.Path, _debugMode))
                    .AddRoute(new FileHandler("."));
                _serverConfig.LoadCertificate(_serverSettings.CertFile, _serverSettings.CertPassword);
                WriteLogString("Инициализация завершена");
            }
            catch (Exception ex)
            {
                _logger.Error("Возникла ошибка инициализации сервера", ex);
                return;
            }

            Task task = HttpServer.ListenAsync(new IPEndPoint(IPAddress.Any, _serverSettings.Port), true, _serverConfig, ct);
            WriteLogString("Сервер запущен");
            while (!ct.IsCancellationRequested)
            {
                if (task.Status == TaskStatus.Running)
                {
                    continue;
                }
                throw new Exception("Сервер самостоятельно остановился! " + task.Exception.Message);
            }
            WriteLogString("Останавливаю сервер");
            task.Wait();
        }

        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }
    }
    internal class MainHandler : IHttpModule
    {
        private readonly ESB_ConnectionPoints.PluginsInterfaces.ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private readonly IMessageHandler _messageHandler;
        private readonly EncryptTools _encryptTools;
        private readonly string _mainPath;
        private readonly bool _debugMode;
        private const int AES_KEY_LENGTH = 16;

        public MainHandler(IMessageHandler messageHandler,
                           IMessageFactory messageFactory,
                           ESB_ConnectionPoints.PluginsInterfaces.ILogger logger,
                           EncryptTools encryptTools,
                           string path,
                           bool debugMode)
        {
            _debugMode = debugMode;
            _logger = logger;
            _messageFactory = messageFactory;
            _messageHandler = messageHandler;
            _encryptTools = encryptTools;
            _mainPath = path;
        }

        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }

        private string GetRequestData(IHttpRequest request)
        {
            using (Stream body = request.Body)
            {
                Encoding encoding = request.GetEncodingForCharset();
                using (StreamReader reader = new StreamReader(body, encoding))
                {
                    if (request.ContentType != null)
                    {
                        WriteLogString(string.Format("Client data content type {0}", request.ContentType));
                    }
                    WriteLogString(string.Format("Client data content length {0}", request.ContentLength));

                    WriteLogString("Start of client data:");
                    string s = reader.ReadToEnd();
                    WriteLogString(s);
                    WriteLogString("End of client data:");
                    body.Close();
                    reader.Close();
                    return s;
                }
            }
        }

        public async Task<bool> HandleAsync(IHttpContext context)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;
            WriteLogString("Method: " + request.Method);
            WriteLogString("Path: " + request.Path);

            if (request.ContentLength == 0)
            {
                response.StatusCode = Ceen.HttpStatusCode.BadRequest;
                response.StatusMessage = "payload is requirable";
                return true;
            }
            if (request.Method != "POST")
            {
                response.StatusCode = Ceen.HttpStatusCode.MethodNotAllowed;
                return true;
            }
            string body = GetRequestData(request);
            if (request.Path == _mainPath)
            {
                try
                {
                    JObject incomingMessage = JObject.Parse(body);
                    Guid messageId;
                    bool isJsonError = false;
                    string errorMessage = "";
                    if (!incomingMessage.ContainsKey("file"))
                    {
                        isJsonError = true;
                        errorMessage += "Missed mandatory property: file; ";
                    }
                    if (!incomingMessage.ContainsKey("keyhash"))
                    {
                        isJsonError = true;
                        errorMessage += "Missed mandatory property: keyhash; ";
                    }
                    if (!incomingMessage.ContainsKey("id"))
                    {
                        isJsonError = true;
                        errorMessage += "Missed mandatory property: id; ";
                    }
                    if (isJsonError)
                    {
                        response.StatusCode = Ceen.HttpStatusCode.BadRequest;
                        response.StatusMessage = "Missed mandatory property(s)";
                        await response.WriteAllJsonAsync("{\"error\":\"" + errorMessage + "\"}");
                        _logger.Error(errorMessage);
                        return true;
                    }

                    try
                    {
                        messageId = Guid.Parse((string)incomingMessage["id"]);
                    }
                    catch (Exception ex)
                    {
                        WriteLogString(ex.Message);
                        _logger.Info("Некорректный id (" + (string)incomingMessage["id"] + "), сообщению назначен новый.");
                        messageId = Guid.NewGuid();
                    }

                    byte[] encrypted = Convert.FromBase64String((string)incomingMessage["file"]);
                    string decrypted;
                    if ((string)incomingMessage["keyhash"] == "")
                    {
                        // не шифрованное
                        decrypted = Encoding.UTF8.GetString(encrypted);
                    }
                    else
                    {
                        byte[] key = _encryptTools.Decrypt_RSA(Convert.FromBase64String((string)incomingMessage["keyhash"]));
                        if (key.Length != AES_KEY_LENGTH)
                        {
                            errorMessage = "Wrong key lenght: " + key.Length;
                            response.StatusCode = Ceen.HttpStatusCode.BadRequest;
                            await response.WriteAllJsonAsync("{\"error\":\"" + errorMessage + "\"}");
                            _logger.Error(
                                String.Format(
                                    "Некорректная длина AES-ключа после расшифровки: {0}. Требуется {1}",
                                    key.Length,
                                    AES_KEY_LENGTH));
                            return true;
                        }

                        try
                        {
                            decrypted = EncryptTools.DecryptStringFromBytes_Aes(encrypted, key);
                        }
                        catch (Exception ex)
                        {
                            response.StatusCode = Ceen.HttpStatusCode.BadRequest;
                            await response.WriteAllJsonAsync("{\"decryptionError\":\"" + ex.Message + "\"}");
                            _logger.Error("Ошибка расшифровки входящего сообщения!", ex);
                            return true;
                        }
                    }

                    Message message = _messageFactory.CreateMessage("HTTPRequest");
                    foreach(var header in request.Headers)
                    {
                        message.AddPropertyWithValue("HTTP_HEADER_" + header.Key, header.Value);
                        WriteLogString(string.Format("HTTP_HEADER_{0}: {1}", header.Key, header.Value));
                    }
                    message.Body = Encoding.UTF8.GetBytes(decrypted);
                    message.Id = messageId;

                    int count = 0;
                    while (count < 5)
                    {
                        try
                        {
                            _messageHandler.HandleMessage(message);
                            break;
                        }
                        finally
                        {
                            count++;
                        }
                    }
                    if (count == 5)
                    {
                        _logger.Warning(decrypted);
                        _logger.Error("Не смог отправить поступившее сообщение в шину!");
                    }
                }
                catch (Exception ex)
                {
                    response.StatusCode = Ceen.HttpStatusCode.InternalServerError;
                    await response.WriteAllJsonAsync("{\"error\":\"" + ex.Message + "\"}");
                    _logger.Error("Ошибка получения сообщения", ex);
                }
                return true;
            }
            else
            {
                response.StatusCode = Ceen.HttpStatusCode.NotFound;
                await response.WriteAllJsonAsync("{\"error\":\"Функция не поддерживается\"}");
                _logger.Error("Вызов неподдерживаемой функции: " + request.Path);
                return true;
            }
        }

    }
    internal class RSAHandler : IHttpModule
    {
        private readonly ESB_ConnectionPoints.PluginsInterfaces.ILogger _logger;
        private readonly EncryptTools _encryptTools;
        private readonly string _mainPath;
        private readonly bool _debugMode;

        public RSAHandler(ESB_ConnectionPoints.PluginsInterfaces.ILogger logger,
                           EncryptTools encryptTools,
                           string path,
                           bool debugMode)
        {
            _debugMode = debugMode;
            _logger = logger;
            _encryptTools = encryptTools;
            _mainPath = path;
        }

        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }

        private string GetRequestData(IHttpRequest request)
        {
            using (Stream body = request.Body)
            {
                Encoding encoding = request.GetEncodingForCharset();
                using (StreamReader reader = new StreamReader(body, encoding))
                {
                    if (request.ContentType != null)
                    {
                        WriteLogString(string.Format("Client data content type {0}", request.ContentType));
                    }
                    WriteLogString(string.Format("Client data content length {0}", request.ContentLength));

                    WriteLogString("Start of client data:");
                    string s = reader.ReadToEnd();
                    WriteLogString(s);
                    WriteLogString("End of client data:");
                    body.Close();
                    reader.Close();
                    return s;
                }
            }
        }

        public async Task<bool> HandleAsync(IHttpContext context)
        {
            IHttpRequest request = context.Request;
            IHttpResponse response = context.Response;
            WriteLogString("Method: " + request.Method);
            WriteLogString("Path: " + request.Path);

            if (request.ContentLength == 0)
            {
                response.StatusCode = Ceen.HttpStatusCode.BadRequest;
                response.StatusMessage = "payload is requirable";
                return true;
            }
            if (request.Method != "POST")
            {
                response.StatusCode = Ceen.HttpStatusCode.MethodNotAllowed;
                return true;
            }
            string body = GetRequestData(request);
            if (request.Path == (_mainPath + "/rsaencrypt"))
            {
                try
                {
                    byte[] encrypted = _encryptTools.Encrypt_RSA(Encoding.UTF8.GetBytes(body));
                    await response.WriteAllAsync(Convert.ToBase64String(encrypted));
                }
                catch (Exception ex)
                {
                    response.StatusCode = Ceen.HttpStatusCode.InternalServerError;
                    await response.WriteAllAsync(ex.Message);
                    _logger.Error("Ошибка шифровки сообщения", ex);
                }
                return true;
            }
            else if (request.Path == (_mainPath + "/rsadecrypt"))
            {
                try
                {
                    byte[] original = Convert.FromBase64String(body);
                    byte[] decrypted = _encryptTools.Decrypt_RSA(original);
                    await response.WriteAllAsync(decrypted);
                }
                catch (Exception ex)
                {
                    response.StatusCode = Ceen.HttpStatusCode.InternalServerError;
                    await response.WriteAllAsync(ex.Message);
                    _logger.Error("Ошибка дешифровки сообщения", ex);
                }
                return true;
            }
            else
            {
                response.StatusCode = Ceen.HttpStatusCode.NotFound;
                await response.WriteAllJsonAsync("{\"error\":\"Функция не поддерживается\"}");
                _logger.Error("Вызов неподдерживаемой функции: " + request.Path);
                return true;
            }
        }
    }
    internal class ServerSettings
    {
        public int Port { get; set; }
        public string CertFile { get; set; }
        public string CertPassword { get; set; }
        public string Path { get; set; }
        public ServerSettings(JObject settings)
        {
            CertPassword = (string)settings["certpassword"];
            CertFile = (string)settings["certfile"];
            Port = (int)settings["port"];
            Path = (string)settings["path"];
        }
    }
}