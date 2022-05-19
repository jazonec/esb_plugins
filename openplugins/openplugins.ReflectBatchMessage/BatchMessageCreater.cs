using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace openplugins.ReflectBatchMessage
{
    internal class BatchMessageCreater : IStandartOutgoingConnectionPoint
    {
        private readonly ILogger _logger;
        private IMessageFactory _messageFactory;
        private readonly bool _debugMode;

        private readonly int _delayMinutes;
        private static TimeSpan skipOneMinute = TimeSpan.FromMinutes(1);

        private readonly BatchMessage _batchMessage = new BatchMessage();
        private readonly HashSet<Guid> _processedMessages = new HashSet<Guid>();
        private readonly int _maxBatchSize;

        private readonly string _batchMessageType;

        public void Cleanup()
        {
        }

        public void Dispose()
        {
        }

        public void Initialize()
        {
            _logger.Info(string.Format("Initialize: 'delay'={0} minutes; 'max batch size'={1} items", _delayMinutes, _maxBatchSize));
        }

        public BatchMessageCreater(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();
            _debugMode = (bool)settings["debug"];
            _delayMinutes = (int)settings["delay"] != 0 ? (int)settings["delay"] : 1;
            _maxBatchSize = (int)settings["size"] != 0 ? (int)settings["size"] : 1000;
            _batchMessageType = (string)settings["type"] ?? "BatchMessage";
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            DateTime _oldTime = DateTime.Now;

            _logger.Info(string.Format("Start running {0}", _oldTime.ToString()));
            while (!ct.IsCancellationRequested)
            {
                if (_oldTime > DateTime.Now)
                {
                    ct.WaitHandle.WaitOne(skipOneMinute);
                    continue;
                }

                // Сработал тик, обновим метку времени
                WriteLogString(string.Format("Tick {0}", _oldTime.ToString()));
                _oldTime = DateTime.Now.AddMinutes(_delayMinutes);
                WriteLogString(string.Format("Next tick {0}", _oldTime.ToString()));

                _batchMessage.Timestamp = DateTime.Now;

                // выберем всё из очереди и закинем в массив батча
                while (true)
                {
                    Message message = messageSource.PeekLockMessage(ct, 1000);
                    if (message == null)
                    {
                        // выбрали всё из очереди
                        break;
                    }
                    if (_processedMessages.Contains(message.Id))
                    {
                        WriteLogString(string.Format("Повторная блокировка сообщения <{0}>", message.Id));
                        continue;
                    }
                    try
                    {
                        _batchMessage.AddMessage(message);
                    }catch (Exception ex)
                    {
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.InvalidMessageFormat, ex.Message);
                        continue;
                    }
                    _ = _processedMessages.Add(message.Id);
                    if (_processedMessages.Count == _maxBatchSize)
                    {
                        // уткнулись в лимит
                        SendBatchMessageToESB(messageSource, replyHandler, ct);
                    }
                }

                if (_processedMessages.Count == 0)
                {
                    _logger.Info(string.Format("Нет новых сообщений на момент {0}", DateTime.Now.ToString()));
                    ct.WaitHandle.WaitOne(skipOneMinute);
                    continue;
                }

                // отправим кусок, который не добрался до лимита
                SendBatchMessageToESB(messageSource, replyHandler, ct);

                ct.WaitHandle.WaitOne(skipOneMinute);
            }
        }
        private void SendBatchMessageToESB(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            WriteLogString(string.Format("Количество: _processedMessages.Count={0}", _processedMessages.Count));

            Message newMessage = _messageFactory.CreateMessage(_batchMessageType);
            newMessage.Body = Encoding.UTF8.GetBytes(_batchMessage.DoSerialize());

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (replyHandler.HandleReplyMessage(newMessage))
                    {
                        break;
                    }
                    ct.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                }
            }
            catch (MessageHandlingException ex)
            {
                _logger.Error(string.Format("Возникла ошибка при обработке ответного сообщения {0}", newMessage), ex);
            }

            // подтвердим сообщения в очереди
            foreach (Guid _id in _processedMessages)
            {
                messageSource.CompletePeekLock(_id);
            }

            // зачистим
            _batchMessage.Clear();
            _processedMessages.Clear();
        }
        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }
    }
    public class BatchMessage
    {
        public DateTime Timestamp;
        public string Description;
        public List<BatchMessageType> types;

        private readonly Dictionary<string, int> typesOffset = new Dictionary<string, int>();

        public BatchMessage()
        {
            Timestamp = DateTime.Now;
            Description = "Новый батч, по таймеру";
            types = new List<BatchMessageType>();
        }

        public void AddMessage(Message mes)
        {
            // если не распарсим XML - упадем и бросим вверх ошибку
            _ = XElement.Parse(Encoding.UTF8.GetString(mes.Body));
            BatchMessageType messagesArray;
            if (!typesOffset.TryGetValue(mes.Type, out int typeOffset))
            {
                messagesArray = new BatchMessageType(mes.Type);
                types.Add(messagesArray);
                typeOffset = types.Count - 1;
                typesOffset.Add(mes.Type, typeOffset);
            }
            else
            {
                messagesArray = types[typeOffset];
            }
            messagesArray.AddMessage(mes.Id, mes.ClassId, mes.Body);
        }

        public void Clear()
        {
            types.Clear();
            typesOffset.Clear();
        }

        public string DoSerialize()
        {
            string Result;
            int totalQty = 0;

            // массив типов сообщений, порпавших в batch
            XElement xtypes = new XElement("Types");

            foreach (BatchMessageType messageTypes in types)
            {
                XElement type = new XElement(messageTypes.Type);
                XElement xmessages = new XElement("messages");
                foreach (BatchMessageLine message in messageTypes.GetLines())
                {
                    object[] originalAttributes = { new XAttribute("originalId", message.Id), new XAttribute("originalClassId", message.ClassId) };
                    XElement xmessage = new XElement("message", originalAttributes);
                    xmessage.Add(XElement.Parse(Encoding.UTF8.GetString(message.Body)));

                    xmessages.Add(xmessage);
                    totalQty++;
                }
                type.Add(xmessages);
                xtypes.Add(type);
            }

            //Создание новой xml
            XDocument xDocument = new XDocument();
            //Корневой элемент новой xml
            XElement root = new XElement("BatchMessage");

            root.Add(new XElement("MessagesQty"));
            root.Add(xtypes);
            root.Element("MessagesQty").Value = totalQty.ToString();
            xDocument.Add(root);
            Result = xDocument.ToString();
            return Result;
        }
    }
    public class BatchMessageType
    {
        public string Type;
        private readonly List<BatchMessageLine> messages;

        public BatchMessageType(string type)
        {
            Type = type;
            messages = new List<BatchMessageLine>();
        }
        public BatchMessageLine[] GetLines()
        {
            return messages.ToArray();
        }

        public void AddMessage(Guid id, string classId, byte[] body)
        {
            messages.Add(new BatchMessageLine(id, classId, body));
        }
    }
    public class BatchMessageLine
    {
        public Guid Id;
        public string ClassId;
        public byte[] Body;

        public BatchMessageLine(Guid id, string classId, byte[] body)
        {
            Id = id;
            ClassId = classId;
            Body = body;
        }
    }
}