using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using RestSharp;
using RestSharp.Serializers;
using System.Threading;
using System.Text;
using System.Net;
using System.IO;
using System.Threading.Tasks;

namespace openplugins.AESServerClient
{
    internal class OutgoingRESTClient : IStandartOutgoingConnectionPoint
    {
        // main settings
        private readonly ILogger _logger;
        private readonly bool _debugMode;
        private readonly bool _encode;

        // endpoint
        private readonly EndpointSettings _endpointSettings;

        // RSA
        private readonly EncryptTools _encryptTools;

        public OutgoingRESTClient(JObject settings, IServiceLocator serviceLocator)
        {
            if (!settings.ContainsKey("endpoint"))
            {
                throw new ArgumentNullException("endpoint");
            }
            if (!settings.ContainsKey("rsa"))
            {
                throw new ArgumentNullException("rsa");
            }

            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = (bool)settings["debug"];
            _encode = (bool)settings["encode"];

            _endpointSettings = new EndpointSettings((JObject)settings["endpoint"]);
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

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                Message message = messageSource.PeekLockMessage(ct, 1000);
                if (message != null)
                {
                    JObject o = new JObject();
                    o["id"] = message.Id.ToString();

                    if (_encode)
                    {
                        byte[] key = EncryptTools.RandomString(16);
                        o["file"] = Convert.ToBase64String(EncryptTools.EncryptStringToBytes_Aes(Encoding.UTF8.GetString(message.Body), key));
                        o["keyhash"] = _encryptTools.Encrypt_RSA(key);
                    }
                    else
                    {
                        o["file"] = Convert.ToBase64String(message.Body);
                        o["keyhash"] = "";
                    }
                    WriteLogString(o.ToString());
                    try
                    {
                        SendMessageToEndpoint(o);
                        messageSource.CompletePeekLock(message.Id);
                    }
                    catch (WebException ex)
                    {
                        _logger.Error("Ошибка выполнения запроса!", ex);
                        messageSource.AbandonPeekLock(message.Id);
                    }
                    catch (InvalidDataException ex)
                    {
                        _logger.Error("Ошибка передачи данных в эндпоинт!", ex);
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.RejectedMessage, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Ошибка выполнения запроса!", ex);
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.UnknowError, ex.Message);
                    }
                }
                else
                {
                    ct.WaitHandle.WaitOne(new TimeSpan(0, 0, 30));
                }
            }
        }

        private void SendMessageToEndpoint(JObject message)
        {
            using (RestClient client = new RestClient(_endpointSettings.Url))
            {
                RestRequest request = new RestRequest(_endpointSettings.Url, _endpointSettings.Method)
                    .AddHeader("login", _endpointSettings.Username)
                    .AddHeader("key", _endpointSettings.Password)
                    .AddHeader("Content-Encoding", "UTF-8")
                    .AddStringBody(message.ToString(), ContentType.Json);

                var resTask = client.ExecuteAsync(request);
                resTask.Wait();
                var response = resTask.Result;
                if (resTask.Status == TaskStatus.Faulted)
                {
                    throw new WebException(resTask.Exception.Message);
                }
                else if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidDataException(Encoding.Unicode.GetString(Encoding.Unicode.GetBytes(response.Content)));
                }
            }
        }

        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }

    }
    internal class EndpointSettings
    {
        public string Url { get; set; }
        public string Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public Method Method { get; set; }

        public EndpointSettings(JObject settings)
        {
            Url = (string)settings["url"];
            Port = (string)settings["port"];
            Username = (string)settings["username"];
            Password = (string)settings["password"];
            string methodString = (string)settings["method"];
            switch (methodString)
            {
                case "POST":
                    Method = Method.Post;
                    break;
                case "PUT":
                    Method = Method.Put;
                    break;
                case "DELETE":
                    Method = Method.Delete;
                    break;
                case "GET":
                    Method = Method.Get;
                    break;
                default:
                    Method = Method.Post;
                    break;
            }
        }
    }
}