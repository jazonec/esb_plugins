using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using Quartz.Impl;
using Quartz;

namespace openplugins.ADIntegration
{
    internal class IngoingManager : IStandartIngoingConnectionPoint
    {
        private readonly bool _debugMode;
        private readonly IngoingSettings settings;
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private readonly LdapConnection ldapConnection;

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

            ldapConnection = new LdapConnection(settings.Ldap);
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

        public async void Run(IMessageHandler messageHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                WriteLogString(string.Format("{0} секунд до старта!", settings.Debug.StartDelay));
                ct.WaitHandle.WaitOne(settings.Debug.StartDelay * 1000); // time to connect for debug
                WriteLogString("Работаем!");
            }

            this.messageHandler = messageHandler;
            this.ct = ct;

            scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            await scheduler.Start();

            if (settings.Users != null)
            {
                await CreateUsersJobAsync(ct, settings.Users);
            }
            if (settings.Groups != null)
            {
                await CreateGroupJobAsync(ct, settings.Groups);
            }

            while (!ct.IsCancellationRequested)
            {
                ct.WaitHandle.WaitOne(5000);
            }
            await scheduler.Shutdown();
        }
        private IJobDetail CreateJob(Type jobType, object settings)
        {
            var name = jobType.Name;
            IJobDetail jobDetail = JobBuilder.Create(jobType)
                .WithIdentity(name, "group")
                .Build();
            jobDetail.JobDataMap["mainclass"] = this;
            jobDetail.JobDataMap["settings"] = settings;
            jobDetail.JobDataMap["ldapConnection"] = ldapConnection;
            return jobDetail;
        }
        private ITrigger CreateTrigger(Type jobType, string cron)
        {
            var name = jobType.Name + "_trigger";
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(name, "group")
                .WithCronSchedule(cron)
                .StartNow()
                .Build();
            return trigger;
        }
        private async Task CreateGroupJobAsync(CancellationToken ct, ReadGroups settings)
        {
            IJobDetail jobDetail = CreateJob(typeof(GroupsJob), settings);
            ITrigger trigger = CreateTrigger(typeof(GroupsJob), settings.Cron);
            await scheduler.ScheduleJob(jobDetail, trigger, ct);
        }
        private async Task CreateUsersJobAsync(CancellationToken ct, ReadUsers settings)
        {
            IJobDetail jobDetail = CreateJob(typeof(UsersJob), settings);
            ITrigger trigger = CreateTrigger(typeof(UsersJob), settings.Cron);
            await scheduler.ScheduleJob(jobDetail, trigger, ct);
        }
        public void SendToESB(JObject objectToSend, string objectType, string classId)
        {
            Message mes = _messageFactory.CreateMessage(objectType);
            mes.ClassId = classId;
            mes.Body = Encoding.UTF8.GetBytes(objectToSend.ToString());
            while (true)
            {
                bool result = messageHandler.HandleMessage(mes);
                if (result)
                {
                    break;
                }
                ct.WaitHandle.WaitOne(1000);
            }
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