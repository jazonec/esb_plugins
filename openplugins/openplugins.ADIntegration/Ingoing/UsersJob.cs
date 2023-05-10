using Newtonsoft.Json.Linq;
using Quartz;
using System.DirectoryServices;
using System.Threading.Tasks;
using System;

namespace openplugins.ADIntegration
{
    internal class UsersJob : IJob
    {
        ReadUsers settings;
        IngoingManager ingoingManager;
        LdapConnection ldapConnection;
        public Task Execute(IJobExecutionContext context)
        {
            ingoingManager = context.JobDetail.JobDataMap["mainclass"] as IngoingManager;
            settings = context.JobDetail.JobDataMap["settings"] as ReadUsers;
            ldapConnection = context.JobDetail.JobDataMap["ldapConnection"] as LdapConnection;

            try
            {
                ingoingManager.WriteLogString("Cработало расписание на выгрузку пользователей");
                SendADUsersToESB();
                ingoingManager.WriteLogString("Закончил выгрузку пользователей");
            }
            catch (Exception ex)
            {
                ingoingManager.WriteError("Ошибка в задании выгрузки пользователей!", ex);
            }
            return Task.CompletedTask;
        }
        private void SendADUsersToESB()
        {
            using (DirectorySearcher ds = new DirectorySearcher(ldapConnection.De, "(&(objectCategory=person)(objectClass=user))", settings.Fields))
            {
                ds.PageSize = 400;
                using (SearchResultCollection results = ds.FindAll())
                {
                    foreach (SearchResult sr in results)
                    {
                        if (sr.Properties["samAccountName"].Count == 0 ||
                            sr.Properties["objectSid"].Count == 0 ||
                            sr.Properties["objectGuid"].Count == 0
                           )
                        {
                            ingoingManager.WriteError("Отсутствуют обязательные данные (один из samAccountName, SID, Guid)! " + sr.Path, new FormatException());
                            continue; // неполноценный объект в домене? Пусть админы посмотрят
                        }
                        if (ingoingManager.IsCancellationRequested)
                        {
                            break; // выйдем, если шина послала запрос на остановку
                        }

                        JObject objectToSend = Utils.SerializeUser(sr, settings.Fields);
                        ingoingManager.SendToESB(objectToSend, "ADUser", settings.ClassId);
                    }
                }
            }
        }
    }
}