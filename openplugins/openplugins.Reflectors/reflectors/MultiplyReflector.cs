﻿using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace openplugins.Reflectors
{
    internal class MultiplyReflector : IReflector
    {
        IMessageSource messageSource;
        IMessageReplyHandler replyHandler;

        private readonly ReflectorManager manager;
        private readonly IList<string> types = new List<string>();
        private readonly IList<string> classIDs = new List<string>();
        private readonly int reflectAmount;

        public MultiplyReflector(JObject settings, ReflectorManager manager)
        {
            this.manager = manager;
            JArray typeArr = (JArray)settings["type"];
            if (typeArr != null)
            {
                foreach (string type in typeArr.Select(v => (string)v))
                {
                    types.Add(type);
                }
            }
            JArray classArr = (JArray)settings["classId"];
            if (classArr != null)
            {
                foreach (string classId in classArr.Select(v => (string)v))
                {
                    classIDs.Add(classId);
                }
            }
            reflectAmount = (int)settings["amount"];
        }

        public IMessageSource MessageSource { set => messageSource = value; }
        public IMessageReplyHandler ReplyHandler { set => replyHandler = value; }

        public void Dispose()
        {
        }

        public IList<string> GetClassIDs()
        {
            return classIDs;
        }

        public IList<string> GetTypes()
        {
            return types;
        }

        public void ProceedMessage(Message message)
        {
            manager.WriteLogString(String.Format("Умножаем сообщение {0}, количество {1}", message.Id, reflectAmount));
            for (int i=0; i<reflectAmount; i++)
            {
                Message reflectMessage = manager._messageFactory.CreateMessage(message.Type + "_reflect_" + (i + 1).ToString());
                reflectMessage.Body = message.Body;
                reflectMessage.Properties = message.Properties;
                replyHandler.HandleReplyMessage(reflectMessage);
            }
            messageSource.CompletePeekLock(message.Id);
        }
    }
}