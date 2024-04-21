using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json;
using System.Text;
using System;

namespace openplugins.OleDb
{
    internal class OutgoingCommand
    {
        private readonly OutgoingManager mainClass;
        PluginDbConnection Connection { set; get; }

        public OutgoingCommand(OutgoingCommandSettings commandSettings, ConnectionSettings connection, OutgoingManager outgoingManager)
        {
            Connection = new PluginDbConnection(connection);
            mainClass = outgoingManager;
        }
        public OutgoingCommand(ConnectionSettings connection)
        {
            Connection = new PluginDbConnection(connection);
        }

        internal void ProcessMessage(Message message)
        {
            throw new NotImplementedException();
        }
        internal void ProcessMessageAsCommand(Message message)
        {
            string messageText = Encoding.UTF8.GetString(message.Body);
            string result;
            MessageAsCommand cmd;
            try
            {
                cmd = JsonConvert.DeserializeObject<MessageAsCommand>(messageText);
            }
            catch (Exception ex)
            {
                throw new InvalidMessageFormatException("Некорректный формат сообщения-команды", ex);
            }
            result = ExecuteCommand(cmd);
            if (cmd.CreateResponse)
            {
                mainClass.SendResponseMessage(message, result, cmd.ResponseMessageType, cmd.ResponseMessageClassId);
            }
        }
        private string ExecuteCommand(MessageAsCommand cmd)
        {
            switch (cmd.CommandType)
            {
                case PluginCommandType.NonQuery:
                    return string.Format("{0} rows affected", (Connection.ExecuteNonQuery(cmd, cmd.Parameters)));
                case PluginCommandType.Query:
                    return Utils.Serialize(Connection.GetDataTable(cmd, cmd.Parameters)).ToString();
                default:
                    throw new InvalidOperationException("Неизвестный тип команды: " + cmd.CommandType);
            };
        }
    }
}