using ESB_ConnectionPoints.PluginsInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace openplugins.PLMConnector
{
    internal class PLMOutgoingPoint : IStandartOutgoingConnectionPoint
    {
        private readonly IMessageFactory _messageFactory;

        public PLMOutgoingPoint(IServiceLocator serviceLocator)
        {
            _messageFactory = serviceLocator.GetMessageFactory();
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
                var message = messageSource.PeekLockMessage(ct, 10000);
                if (message == null)
                    continue;

                List<Message> replies = Reflect(message);
                foreach (Message reply in replies)
                {
                    replyHandler
                        .HandleReplyMessage(reply);
                }

                messageSource.CompletePeekLock(message.Id);
            }
        }

        private List<Message> Reflect(Message request)
        {
            List<Message> replies = new List<Message>();
            var xml = Encoding.UTF8.GetString(request.Body);
            XmlDocument requestDocument = new XmlDocument();
            requestDocument.LoadXml(xml);

            XmlElement requestRoot = requestDocument.DocumentElement;
            if (requestRoot != null)
            {
                foreach (XmlElement requestNode in requestRoot)
                {
                    XmlDocument replyDocument = new XmlDocument();
                    var declaration = replyDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
                    replyDocument.AppendChild(declaration);

                    replyDocument
                        .AppendChild(replyDocument.ImportNode(requestNode, true));

                    StringBuilder sb = new StringBuilder("");
                    StringWriter sw = new StringWriter(sb);
                    replyDocument.Save(sw);

                    Message replyMessage = _messageFactory.CreateReplyMessage(request, "ReflectedResponse");
                    replyMessage.Body = Encoding.UTF8.GetBytes(sw.ToString());

                    replies
                        .Add(replyMessage);
                }
            }

            return replies;
        }
    }
}