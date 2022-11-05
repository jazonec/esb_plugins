using ESB_ConnectionPoints.PluginsInterfaces;
using System.Runtime.Remoting.Messaging;
using System;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Quartz.Impl;
using Quartz;
using Quartz.Impl.Matchers;
using System.Threading.Tasks;
using static Quartz.Logging.OperationName;
using System.Text;

namespace openplugins.multijob
{
    internal class MultiJobRunner : IStandartIngoingConnectionPoint , IJobListener
    {
        private readonly ILogger _logger;
        private readonly IMessageFactory _messageFactory;
        private IMessageHandler _messageHandler;
        private readonly bool _debugMode;

        IScheduler scheduler;

        public string Name => "yuppy";

        public MultiJobRunner(JObject settings, IServiceLocator serviceLocator)
        {
            _logger = serviceLocator.GetLogger(GetType());
            _messageFactory = serviceLocator.GetMessageFactory();
            _debugMode = (bool)settings["debug"];
        }
        public async void Run(IMessageHandler messageHandler, CancellationToken ct)
        {
            if (_debugMode)
            {
                // 30 секунд для подключения дебага
                WriteLogString("30 секунд до старта");
                ct.WaitHandle.WaitOne(30000);
            }
            _messageHandler = messageHandler;

            scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            await scheduler.Start();
            // Listener позволяет получать состояния job, см https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/trigger-and-job-listeners.html
            scheduler.ListenerManager.AddJobListener(this, GroupMatcher<JobKey>.AnyGroup());

            await CreateJobAsync(ct, "job_01", "trigger_01", 15);
            await CreateJobAsync(ct, "job_02", "trigger_02", 20);

            while (!ct.IsCancellationRequested)
            {
                ct.WaitHandle.WaitOne(5000);
            }
            await scheduler.Shutdown();
        }

        private async Task CreateJobAsync(CancellationToken ct, string jobName, string triggerName, int intervalInSeconds)
        {
            IJobDetail jobDetail = JobBuilder.Create<SimpleJob>()
                .WithIdentity(jobName, "group")
                .Build();
            jobDetail.JobDataMap["mainclass"] = this;

            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(triggerName, "group")
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(intervalInSeconds)
                    .RepeatForever())
                .Build();
            await scheduler.ScheduleJob(jobDetail, trigger, ct);
        }
        internal void SendToESB(string typeMessage, string stringBody)
        {
            Message message = _messageFactory.CreateMessage(typeMessage);
            message.Body = Encoding.UTF8.GetBytes(stringBody);
            _messageHandler.HandleMessage(message);
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
        }

        public void Initialize()
        {
        }

        public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException, CancellationToken cancellationToken = default)
        {
            try
            {
                WriteLogString("Job finished, " + context.Get("SimpleJob_Result"));
            }catch(Exception ex)
            {
                _logger.Error(ex);
            }
            return Task.CompletedTask;
        }
    }
}