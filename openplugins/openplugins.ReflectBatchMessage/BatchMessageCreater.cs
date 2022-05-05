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
        private readonly bool _debugMode;

        private int _delayMinutes;
        private static TimeSpan skipOneMinute = TimeSpan.FromMinutes(1);

        private BatchMessage _batchMessage = new BatchMessage();
        private HashSet<Guid> _processedMessages = new HashSet<Guid>();
        private readonly int _maxBatchSize;

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
            _debugMode = (bool)settings["debug"];
            _delayMinutes = (int)settings["delay"] != 0 ? (int)settings["delay"] : 1;
            _maxBatchSize = (int)settings["size"] != 0 ? (int)settings["size"] : 1000;
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            DateTime _oldTime = DateTime.Now;
            _logger.Info(string.Format("Start running {0}", _oldTime.ToString()));
        }

    }
    public class BatchMessage
    {
        public DateTime Timestamp;
        public string Description;
        public BatchMessageType[] types = new BatchMessageType[0];

        public BatchMessage()
        {
            Timestamp = DateTime.Now;
            Description = "Новый батч, по таймеру";
        }

        public string DoSerialize()
        {
            string Result;

            //Создание новой xml
            XDocument xDocument = new XDocument();
            //Корневой элемент новой xml
            XElement root = new XElement("BatchMessage");
            //Создаем элементы дерева
            root.Add(new XElement("Type"));
            root.Add(new XElement("MessagesQty"));
            root.Add(new XElement("Messages"));
            root.Element("MessagesQty").Value = lines.Length.ToString();

            foreach (BatchMessageLine line in lines)
            {
                XElement xMessage = new XElement("Message", new XAttribute("originalId", line.Id));
                xMessage.Add(XElement.Parse(Encoding.UTF8.GetString(line.Body)));

                root.Element("Messages").Add(xMessage);
            }

            xDocument.Add(root);
            Result = xDocument.ToString();

            return Result;
        }
    }

    public class BatchMessageType
    {
        public string Type;
        public BatchMessageLine[] lines = new BatchMessageLine[0];
    }
    public class BatchMessageLine
    {
        public Guid Id;
        public byte[] Body;
    }
}