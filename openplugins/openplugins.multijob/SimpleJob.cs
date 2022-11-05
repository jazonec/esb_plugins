using Quartz;
using System.Threading.Tasks;

namespace openplugins.multijob
{
    internal class SimpleJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            MultiJobRunner mainclass = context.JobDetail.JobDataMap["mainclass"] as MultiJobRunner;
            mainclass.SendToESB(context.JobDetail.Key.ToString(), "Hello from SimpleJob");
            
            context.Put("SimpleJob_Result", "Some message from SimpleJob " + context.JobDetail.Key.ToString());
            return Task.CompletedTask;
        }
    }
}