using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Data.OleDb;
using System.Text;
using System.Threading;

namespace openplugins.OleDb
{
    internal delegate void MessageReceivedDelegate(string message);
    internal delegate void DebugDelegate(string message);
    internal delegate void ErrorDelegate(string message, Exception ex);
    internal class IngoingOleDbClient : IStandartIngoingConnectionPoint
    {
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private IMessageHandler _messageHandler;
        private readonly bool _debugMode;
        private readonly string _command;
        private string _connectionString;

        public IngoingOleDbClient(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();
            _debugMode = (bool)settings["debug"];
            _connectionString = (string)settings["connectionString"];
            _command = (string)settings["command"];
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
            if (_debugMode)
            {
                WriteLogString("30 секунд до старта!");
                ct.WaitHandle.WaitOne(30000);
            }
            try
            {
                OleDbConnection cnn = new OleDbConnection(_connectionString);
                cnn.Open();
                WriteLogString("Соединение проверено");
                cnn.Close();
            }
            catch (Exception ex)
            {
                _logger.Error("Ошибка установки соединения!", ex);
                return;
            }

            _messageHandler = messageHandler;

            // todo: обрабатываем из настроек массив запросов, создаем для каждого свой класс и запускаем
            RequestOleDb request = new RequestOleDb(_connectionString, _command);
            request.OnMessageReceived += new MessageReceivedDelegate(SendMessagetoESB);
            request.OnDebug += new DebugDelegate(Debug);
            request.OnError += new ErrorDelegate(Error);
            request.Start(ct);

            while (!ct.IsCancellationRequested)
            {
                ct.WaitHandle.WaitOne(5000);
            }
            request.Dispose();
        }
        private void SendMessagetoESB(string message)
        {
            Message esbMessage = _messageFactory.CreateMessage("OleDb_message");
            esbMessage.Body = Encoding.UTF8.GetBytes(message);
            try
            {
                int fiveTimes = 5;
                while (!_messageHandler.HandleMessage(esbMessage))
                {
                    Thread.Sleep(1000);
                    fiveTimes--;
                    if (fiveTimes == 0)
                    {
                        throw new Exception("Не удалось отправить сообщение в шину");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Не удалось отправить сообщение в шину: " + ex.Message, ex);
                throw ex;
            }
        }
        private void Debug(string log)
        {
            WriteLogString(log);
        }
        private void Error(string err, Exception ex)
        {
            _logger.Error(err, ex);
        }
        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }
    }
}