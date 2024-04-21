using ESB_ConnectionPoints.PluginsInterfaces;
using Quartz.Impl;
using Quartz;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace openplugins.OleDb
{
    internal class IngoingManager : IStandartIngoingConnectionPoint
    {
        private readonly bool _debugMode;
        private readonly IngoingSettings settings;
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        public readonly PluginDbConnection dbConnection;

        private IMessageHandler messageHandler;
        private CancellationToken ct;
        private IScheduler scheduler;

        public bool IsCancellationRequested { get => ct.IsCancellationRequested; }

        public IngoingManager(IngoingSettings settings, IServiceLocator serviceLocator)
        {
            this.settings = settings;
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();

            _debugMode = settings.Debug.DebugMode;

            dbConnection = new PluginDbConnection(settings.Connection);
        }
        public void SendMessagetoESB(string message, string type, string classId)
        {
            Message esbMessage = _messageFactory.CreateMessage(type);
            esbMessage.ClassId = classId ?? "";
            esbMessage.Body = Encoding.UTF8.GetBytes(message);
            messageHandler.HandleMessage(esbMessage);
        }
        public async void Run(IMessageHandler messageHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString(string.Format("{0} секунд до старта!", settings.Debug.StartDelay));
                ct.WaitHandle.WaitOne(settings.Debug.StartDelay * 1000); // time to connect for debug
            }

            this.messageHandler = messageHandler;
            this.ct = ct;

            scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            await scheduler.Start();

            await CreateJobsAsync(settings.Commands);

            while (!ct.IsCancellationRequested)
            {
                ct.WaitHandle.WaitOne(5000);
            }
            await scheduler.Shutdown();
        }
        private async Task CreateJobsAsync(List<IngoingCommandSettings> commands)
        {
            foreach (var command in commands)
            {
                IJobDetail jobDetail = Utils.CreateJob(command.Name, this, command);
                ITrigger trigger = Utils.CreateTrigger(command.Name, command.Cron);
                await scheduler.ScheduleJob(jobDetail, trigger, ct);
                WriteLogString(string.Format("Добавлена команда '{0}' с расписанием '{1}'", command.Name, command.Cron));
            }
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
                _logger?.Debug(log);
            }
        }
        internal void WriteError(string v, Exception ex)
        {
            _logger?.Error(v, ex);
        }
    }
}