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
            ct.WaitHandle.WaitOne(60000);
            try
            {
                _serverConfig = new ServerConfig()
                    .AddLogger(new MainLogger(_logger))
                    .AddRoute(_serverSettings.Path, new MainHandler(messageHandler, _messageFactory, _encryptTools, _serverSettings.Path))
                    .AddRoute(_serverSettings.Path + "/*", new RSAHandler(_encryptTools, _serverSettings.Path))
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
                ct.WaitHandle.WaitOne(60000);// Проверяем состояние раз в минуту
                if (task.Status == TaskStatus.Faulted)
                {
                    throw new Exception("Сервер самостоятельно остановился! ", task.Exception.InnerException);
                }
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
    internal class MainLogger : Ceen.IMessageLogger
    {
        private ESB_ConnectionPoints.PluginsInterfaces.ILogger _logger;
        public MainLogger(ESB_ConnectionPoints.PluginsInterfaces.ILogger logger)
        {
            _logger = logger;
        }

        public Task LogMessageAsync(IHttpContext context, Exception ex, LogLevel loglevel, string message, DateTime when)
        {
            switch (loglevel)
            {
                case LogLevel.Error:
                    _logger.Error(message, ex);
                    break;
                case LogLevel.Warning:
                    _logger.Warning(message);
                    break;
                case LogLevel.Information:
                    _logger.Info(message);
                    break;
                case LogLevel.Debug:
                    _logger.Debug(message);
                    break;
                default:
                    _logger?.Error(message);
                    break;

            }
            return Task.FromResult(true);
        }

        Task Ceen.ILogger.LogRequestCompletedAsync(IHttpContext context, Exception ex, DateTime started, TimeSpan duration)
        {
            return Task.FromResult(true);
        }
    }
    internal class MainHandler : IHttpModule
    {
        private readonly IMessageFactory _messageFactory;
        private readonly IMessageHandler _messageHandler;
        private readonly EncryptTools _encryptTools;
        private readonly string _mainPath;
        private const int AES_KEY_LENGTH = 16;

        public MainHandler(IMessageHandler messageHandler,
                           IMessageFactory messageFactory,
                           EncryptTools encryptTools,
                           string path)
        {
            _messageFactory = messageFactory;
            _messageHandler = messageHandler;
            _encryptTools = encryptTools;
            _mainPath = path;
        }

        private string GetRequestData(IHttpContext context, IHttpRequest request)
        {
            using (Stream body = request.Body)
            {
                Encoding encoding = request.GetEncodingForCharset();
                using (StreamReader reader = new StreamReader(body, encoding))
                {
                    if (request.ContentType != null)
                    {
                        context.LogDebugAsync(string.Format("Client data content type {0}", request.ContentType));
                    }
                    context.LogDebugAsync(string.Format("Client data content length {0}", request.ContentLength));

                    context.LogDebugAsync("Start of client data:");
                    string s = reader.ReadToEnd();
                    context.LogDebugAsync(s);
                    context.LogDebugAsync("End of client data:");
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
            await context.LogDebugAsync("Method: " + request.Method);
            await context.LogDebugAsync("Path: " + request.Path);

            if (request.ContentLength == 0)
            {
                response.StatusCode = Ceen.HttpStatusCode.BadRequest;
                response.StatusMessage = "payload is requirable";
                await context.LogDebugAsync("Запрос с пустым телом");
                return true;
            }
            if (request.Method != "POST")
            {
                response.StatusCode = Ceen.HttpStatusCode.MethodNotAllowed;
                return true;
            }
            string body = GetRequestData(context, request);
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
                        await context.LogInformationAsync(errorMessage);
                        return true;
                    }

                    try
                    {
                        messageId = Guid.Parse((string)incomingMessage["id"]);
                    }
                    catch (Exception ex)
                    {
                        await context.LogInformationAsync("Некорректный id (" + (string)incomingMessage["id"] + "), сообщению назначен новый.", ex);
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
                            await context.LogErrorAsync(
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
                            await context.LogErrorAsync("Ошибка расшифровки входящего сообщения!", ex);
                            return true;
                        }
                    }

                    Message message = _messageFactory.CreateMessage("HTTPRequest");
                    foreach(var header in request.Headers)
                    {
                        message.AddPropertyWithValue("HTTP_HEADER_" + header.Key, header.Value);
                        await context.LogDebugAsync(string.Format("HTTP_HEADER_{0}: {1}", header.Key, header.Value));
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
                        await context.LogWarningAsync(decrypted);
                        await context.LogErrorAsync("Не смог отправить поступившее сообщение в шину!");
                    }
                }
                catch (Exception ex)
                {
                    response.StatusCode = Ceen.HttpStatusCode.InternalServerError;
                    await response.WriteAllJsonAsync("{\"error\":\"" + ex.Message + "\"}");
                    await context.LogErrorAsync("Ошибка получения сообщения", ex);
                }
                return true;
            }
            else
            {
                response.StatusCode = Ceen.HttpStatusCode.NotFound;
                await response.WriteAllJsonAsync("{\"error\":\"Функция не поддерживается\"}");
                await context.LogErrorAsync("Вызов неподдерживаемой функции: " + request.Path);
                return true;
            }
        }

    }
    internal class RSAHandler : IHttpModule
    {
        private readonly EncryptTools _encryptTools;
        private readonly string _mainPath;

        public RSAHandler(EncryptTools encryptTools, string path)
        {
            _encryptTools = encryptTools;
            _mainPath = path;
        }

        private string GetRequestData(IHttpContext context, IHttpRequest request)
        {
            using (Stream body = request.Body)
            {
                Encoding encoding = request.GetEncodingForCharset();
                using (StreamReader reader = new StreamReader(body, encoding))
                {
                    if (request.ContentType != null)
                    {
                        context.LogDebugAsync(string.Format("Client data content type {0}", request.ContentType));
                    }
                    context.LogDebugAsync(string.Format("Client data content length {0}", request.ContentLength));

                    context.LogDebugAsync("Start of client data:");
                    string s = reader.ReadToEnd();
                    context.LogDebugAsync(s);
                    context.LogDebugAsync("End of client data:");
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
            await context.LogDebugAsync("Method: " + request.Method);
            await context.LogDebugAsync("Path: " + request.Path);

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
            string body = GetRequestData(context, request);
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
                    await context.LogErrorAsync("Ошибка шифровки сообщения", ex);
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
                    await context.LogErrorAsync("Ошибка дешифровки сообщения", ex);
                }
                return true;
            }
            else
            {
                response.StatusCode = Ceen.HttpStatusCode.NotFound;
                await response.WriteAllJsonAsync("{\"error\":\"Функция не поддерживается\"}");
                await context.LogErrorAsync("Вызов неподдерживаемой функции: " + request.Path);
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