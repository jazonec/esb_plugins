using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;

namespace openplugins.Reflectors
{
    internal class ReflectorManager : IStandartOutgoingConnectionPoint
    {
        private readonly ILogger _logger;
        public readonly IMessageFactory _messageFactory;
        private readonly DebugSettings _debugMode;
        private readonly IDictionary<string, IReflector> typeReflectors;
        private readonly IDictionary<string, IReflector> classReflectors;
        private readonly IList<IReflector> reflectorsList;
        private readonly PluginReflectors reflectors;

        public ReflectorManager(PluginSettings settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _debugMode = settings.debug;
            _messageFactory = serviceLocator.GetMessageFactory();

            typeReflectors = new Dictionary<string, IReflector>();
            classReflectors = new Dictionary<string, IReflector>();
            reflectorsList = new List<IReflector>();

            reflectors = settings.reflectors;
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
                if (classReflectors.ContainsKey(classId))
                {
                    throw new ArgumentException("Некорректные настройки, класс не может использоваться в нескольких рефлекторах.", classId);
                }
                classReflectors.Add(classId, reflector);
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

        public void WriteLogString(string log)
        {
            if (_debugMode.DebugMode)
            {
                _logger.Debug(log);
            }
        }
        public void WriteErrorString(string log)
        {
            _logger.Error(log);
        }
        public void WriteErrorString(string log, Exception ex)
        {
            _logger.Error(log, ex);
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
            CreateReflector(reflectors.unbatch);
            CreateReflector(reflectors.encryptor);
            CreateReflector(reflectors.multiply);
            CreateReflector(reflectors.blackHole);
            CreateReflector(reflectors.batch);
            CreateReflector(reflectors.changed);
        }

        private void CreateReflector(ChangedSettings changedSettings)
        {
            if (changedSettings != null)
            {
                ChangedReflector changed = new ChangedReflector(changedSettings, this);
                reflectorsList.Add(changed);
                FillTypes(changed);
                FillClassIDs(changed);
            }
            else
            {
                WriteLogString("Пустой blackHole, пропускаю");
            }
        }

        private void CreateReflector(BatchReflectorSettings batchSettings)
        {
            if (batchSettings != null)
            {
                throw new NotImplementedException();
            }
        }

        private void CreateReflector(BlackHoleSettings blackHoleSettings)
        {
            if (blackHoleSettings != null)
            {
                BlackHole blackhole = new BlackHole(blackHoleSettings, this);
                reflectorsList.Add(blackhole);
                FillTypes(blackhole);
                FillClassIDs(blackhole);
            }
            else
            {
                WriteLogString("Пустой blackHole, пропускаю");
            }
        }

        private void CreateReflector(MultiplySettings multiplySettings)
        {
            if (multiplySettings != null)
            {
                MultiplyReflector multiply = new MultiplyReflector(multiplySettings, this);
                reflectorsList.Add(multiply);
                FillTypes(multiply);
                FillClassIDs(multiply);
            }
            else
            {
                WriteLogString("Пустой multiply, пропускаю");
            }
        }

        private void CreateReflector(EncryptorSettings encryptorSettings)
        {
            if (encryptorSettings != null)
            {
                Encryptor encryptor = new Encryptor(encryptorSettings, this);
                reflectorsList.Add(encryptor);
                FillTypes(encryptor);
                FillClassIDs(encryptor);
            }
            else
            {
                WriteLogString("Пустой encryptor, пропускаю");
            }
        }

        private void CreateReflector(UnbatchSettings unbatchSettings)
        {
            if (unbatchSettings != null)
            {
                UnbatchReflector unbatch = new UnbatchReflector(unbatchSettings, this);
                reflectorsList.Add(unbatch);
                FillTypes(unbatch);
                FillClassIDs(unbatch);
            }
            else
            {
                WriteLogString("Пустой unbatch, пропускаю");
            }
        }

        public void Run(IMessageSource messageSource, IMessageReplyHandler replyHandler, CancellationToken ct)
        {
            if (_debugMode.DebugMode)
            {
                WriteLogString(String.Format("{0} секунд до старта!", _debugMode.StartDelay));
                ct.WaitHandle.WaitOne(_debugMode.StartDelay * 1000);
            }

            ChekSettings();
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

        private void ChekSettings()
        {
            if (reflectorsList.Count == 0)
            {
                throw new ArgumentNullException("reflectors");
            }
            if (classReflectors.Count == 0 && typeReflectors.Count == 0)
            {
                throw new ArgumentNullException("classId & type");
            }
        }
    }
}