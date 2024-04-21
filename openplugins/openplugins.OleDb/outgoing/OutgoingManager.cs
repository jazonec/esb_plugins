using ESB_ConnectionPoints.PluginsInterfaces;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System;

namespace openplugins.OleDb
{
    internal class OutgoingManager : IStandartOutgoingConnectionPoint
    {
        private readonly ILogger logger;
        private readonly IMessageFactory messageFactory;
        private IMessageReplyHandler replyHandler;
        private readonly bool _debugMode;
        private readonly PluginDbConnection dbConnection;
        private readonly OutgoingSettings settings;

        private readonly Dictionary<string, OutgoingCommand> typeCommands;
        private readonly Dictionary<string, OutgoingCommand> classCommands;
        private readonly List<OutgoingCommand> listCommands;
        private readonly OutgoingCommand universalCommand;

        public OutgoingManager(OutgoingSettings settings, IServiceLocator serviceLocator)
        {
            this.settings = settings;
            logger = serviceLocator.GetLogger(GetType());
            messageFactory = serviceLocator.GetMessageFactory();
            _debugMode = settings.Debug.DebugMode;

            dbConnection = new PluginDbConnection(settings.Connection);
            typeCommands = new Dictionary<string, OutgoingCommand>();
            classCommands = new Dictionary<string, OutgoingCommand>();
            listCommands = new List<OutgoingCommand>();

            if (settings.Commands != null)
            {
                foreach (var commandSettings in settings.Commands)
                {
                    OutgoingCommand command = new OutgoingCommand(commandSettings, settings.Connection, this);
                    listCommands.Add(command);
                    if (commandSettings.MessageType != null)
                        typeCommands.Add(commandSettings.MessageType, command);
                    if (commandSettings.MessageClassId != null)
                        classCommands.Add(commandSettings.MessageType, command);
                }
            }
            universalCommand = new OutgoingCommand(null, settings.Connection, this);
            listCommands.Add(universalCommand);
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString(string.Format("{0} секунд до старта!", settings.Debug.StartDelay));
                ct.WaitHandle.WaitOne(settings.Debug.StartDelay * 1000); // time to connect for debug
            }
            this.replyHandler = replyHandler;

            while (!ct.IsCancellationRequested)
            {
                Message message = messageSource.PeekLockMessage(ct, 1000);
                if (message != null)
                {
                    try
                    {
                        //string messageText = Encoding.UTF8.GetString(message.Body);
                        if (typeCommands.ContainsKey(message.Type))
                        {
                            typeCommands[message.Type].ProcessMessage(message);
                        }
                        else if (classCommands.ContainsKey(message.ClassId))
                        {
                            classCommands[message.ClassId].ProcessMessage(message);
                        }
                        else
                        {
                            universalCommand.ProcessMessageAsCommand(message);
                        }
                        messageSource.CompletePeekLock(message.Id);
                    }
                    catch (Exception ex)
                    {
                        WriteError("Ошибка при обработке сообщения", ex);
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.UnknowError, ex.Message);
                    }
                }
                else
                {
                    ct.WaitHandle.WaitOne(5000);
                }
            }
        }
        internal void SendResponseMessage(Message origMessage, string response, string messageType, string messageClassId)
        {
            Message message = messageFactory.CreateReplyMessage(origMessage, messageType);
            message.Body = Encoding.UTF8.GetBytes(response);
            message.ClassId = messageClassId;
            replyHandler.HandleReplyMessage(message);
        }
        public void Cleanup()
        {
        }
        public void Dispose()
        {
        }
        public void Initialize()
        {
            dbConnection.CheckConnection();
            WriteLogString("Подключение проверено");
        }
        internal void WriteLogString(string log)
        {
            if (_debugMode)
            {
                logger?.Debug(log);
            }
        }
        internal void WriteError(string v, Exception ex)
        {
            logger?.Error(v, ex);
        }
    }
}