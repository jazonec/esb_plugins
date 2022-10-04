using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System.Data.OleDb;
using System.Threading;

namespace openplugins.OleDb
{
    internal class IngoingOleDbClient : IStandartIngoingConnectionPoint
    {
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private IMessageHandler _messageHandler;
        private readonly bool _debugMode;

        private string _connectionString;

        public IngoingOleDbClient(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();
            _debugMode = (bool)settings["debug"];
            _connectionString = (string)settings["connectionString"];
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
            OleDbConnection cnn = new OleDbConnection(_connectionString);
            cnn.Open();
            WriteLogString("Соединение проверено");
            cnn.Close();
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