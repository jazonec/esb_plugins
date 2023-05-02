using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime;
using System.Threading;

namespace openplugins.Reflectors
{
    internal class ReflectorManager : IStandartOutgoingConnectionPoint
    {
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private readonly bool _debugMode;
        private readonly IDictionary<string, IReflector> typeReflectors;
        private readonly IDictionary<string, IReflector> classReflectors;
        private readonly IList<IReflector> reflectorsList;

        public ReflectorManager(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = (bool)settings["debug"];
            _messageFactory = serviceLocator.GetMessageFactory();

            typeReflectors = new Dictionary<string, IReflector>();
            classReflectors = new Dictionary<string, IReflector>();
            reflectorsList = new List<IReflector>();

            var _reflectorsSettings = (JObject)settings["reflectors"];

            // ChangeReflector
            JObject _change = (JObject)_reflectorsSettings["change"];
            if (_change != null)
            {
                CreateChangeReflector(_change);
            }
            // BatchReflector
            JObject _batch = (JObject)_reflectorsSettings["batch"];
            if (_batch != null)
            {
                CreateBatchReflector(_batch);
            }
            // BlackHole
            JObject _blackHole = (JObject)_reflectorsSettings["blackHole"];
            if (_blackHole != null)
            {
                CreateBlackHole(_blackHole);
            }
            // MultiplyReflector
            JObject _multiply = (JObject)_reflectorsSettings["multiply"];
            if (_multiply != null)
            {
                CreateMultiplyReflector(_multiply);
            }
            // UnBatchReflector
            JObject _unbatch = (JObject)_reflectorsSettings["unbatch"];
            if (_unbatch != null)
            {
                CreateUnbatchReflector(_unbatch);
            }
        }

        private void CreateUnbatchReflector(JObject settings)
        {
            UnbatchReflector unbatch = new UnbatchReflector(settings, _messageFactory);
            reflectorsList.Add(unbatch);
            FillTypes(unbatch);
            FillClassIDs(unbatch);
        }

        private void CreateMultiplyReflector(JObject settings)
        {
            MultiplyReflector multiply = new MultiplyReflector(settings, _messageFactory);
            reflectorsList.Add(multiply);
            FillTypes(multiply);
            FillClassIDs(multiply);
        }

        private void CreateBlackHole(JObject settings)
        {
            BlackHole blackHole = new BlackHole(settings, _messageFactory);
            reflectorsList.Add(blackHole);
            FillTypes(blackHole);
            FillClassIDs(blackHole);
        }

        private void CreateBatchReflector(JObject settings)
        {
            throw new NotImplementedException();
        }

        private void CreateChangeReflector(JObject settings)
        {
            throw new NotImplementedException();
        }

        private void FillTypes(IReflector reflector)
        {
            foreach (string type in reflector.GetTypes())
            {
                if (typeReflectors.ContainsKey(type))
                {
                    throw new ArgumentException("Некорректные настройки, тип не может использоваться в нескольких рефлекторах.", type);
                }
                typeReflectors.Add(type, reflector);
            }
        }

        private void FillClassIDs(IReflector reflector)
        {
            foreach (string classId in reflector.GetClassIDs())
            {
                if (typeReflectors.ContainsKey(classId))
                {
                    throw new ArgumentException("Некорректные настройки, класс не может использоваться в нескольких рефлекторах.", classId);
                }
                typeReflectors.Add(classId, reflector);
            }
        }

        private void FillReflectors(IMessageSource messageSource, IMessageReplyHandler replyHandler)
        {
            foreach (IReflector reflector in reflectorsList)
            {
                reflector.MessageSource = messageSource;
                reflector.ReplyHandler = replyHandler;
            }
            WriteLogString("Передал в рефлекторы интерфейсы шины");
        }

        private void WriteLogString(string log)
        {
            if (_debugMode)
            {
                _logger.Debug(log);
            }
        }

        public void Cleanup()
        {
        }

        public void Dispose()
        {
            foreach (IReflector reflector in reflectorsList)
            {
                reflector.Dispose();
            }
            WriteLogString("Уничтожил рефлекторы");
        }

        public void Initialize()
        {
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString("20 секунд до старта!");
                ct.WaitHandle.WaitOne(20000);
            }
            _logger.Info(string.Format("Приступил к работе {0}", DateTime.Now.ToString()));

            FillReflectors(messageSource, replyHandler);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Message message = messageSource.PeekLockMessage(ct, 1000);
                    if (message == null)
                    {
                        ct.WaitHandle.WaitOne(5000);
                        continue;
                    }
                    if (typeReflectors.ContainsKey(message.Type))
                    {
                        typeReflectors[message.Type].ProceedMessage(message);
                    }
                    else if (classReflectors.ContainsKey(message.ClassId))
                    {
                        classReflectors[message.ClassId].ProceedMessage(message);
                    }
                    else
                    {
                        messageSource.CompletePeekLock(message.Id, MessageHandlingError.RejectedMessage, "Отсутствует настройка для типа или класса сообщения!");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Ошибка в потоке!", ex);
                }
            }
        }
    }
}