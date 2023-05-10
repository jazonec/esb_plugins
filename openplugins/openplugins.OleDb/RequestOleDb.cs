using Newtonsoft.Json;
using System;
using System.Data;
using System.Data.OleDb;
using System.Threading;

namespace openplugins.OleDb
{
    internal class RequestOleDb : IDisposable
    {
        private OleDbConnection connection;
        private string oleDbCommand;

        public event MessageReceivedDelegate OnMessageReceived;
        public event DebugDelegate OnDebug;
        public event ErrorDelegate OnError;

        public RequestOleDb(string connectionString, string oleDbCommand)
        {
            connection = new OleDbConnection(connectionString);
            this.oleDbCommand = oleDbCommand;   
        }

        public void Dispose()
        {
            connection?.Close();
        }

        internal void Start(CancellationToken ct)
        {
            // todo: тут будет обработка расписания запуска
            // пока сделаем в лоб раз в 2 минуты
            while (!ct.IsCancellationRequested)
            {
                var _ct = new CancellationToken();
                _ct.WaitHandle.WaitOne(120000);
                try
                {
                    connection.Open();
                    OleDbCommand command = new OleDbCommand(oleDbCommand, connection);
                    OleDbDataAdapter adapter = new OleDbDataAdapter(command);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);
                    connection.Close();
                    OnDebug?.Invoke("Получена выборка");
                    OnMessageReceived?.Invoke(JsonConvert.SerializeObject(dt));
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(string.Format("Ошибка при обращении к функции {0}", oleDbCommand), ex);
                }
            }

        }
    }
}