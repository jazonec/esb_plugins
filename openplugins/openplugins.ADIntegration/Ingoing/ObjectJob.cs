using Newtonsoft.Json.Linq;
using Quartz;
using System.DirectoryServices;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace openplugins.ADIntegration
{
    internal class ObjectJob : IJob
    {
        ReadObjects settings;
        IngoingManager ingoingManager;
        LdapConnection ldapConnection;
        public Task Execute(IJobExecutionContext context)
        {
            ingoingManager = context.JobDetail.JobDataMap["mainclass"] as IngoingManager;
            settings = context.JobDetail.JobDataMap["settings"] as ReadObjects;
            ldapConnection = context.JobDetail.JobDataMap["ldapConnection"] as LdapConnection;

            try
            {
                ingoingManager.WriteLogString(string.Format("Cработало расписание {0}", settings.Name));
                SendObjectsToESB();
                ingoingManager.WriteLogString("Закончил выгрузку");
            }
            catch (Exception ex)
            {
                ingoingManager.WriteError("Ошибка в задании выгрузки!", ex);
            }
            return Task.CompletedTask;
        }

        private void SendObjectsToESB()
        {
            JObject objectToSend;
            using (DirectorySearcher ds = new DirectorySearcher(ldapConnection.De, settings.ObjectFilter))
            {
                ds.PageSize = 400;

                using (SearchResultCollection results = ds.FindAll())
                {
                    foreach (SearchResult sr in results)
                    {
                        if (ingoingManager.IsCancellationRequested)
                        {
                            break; // выходим если шина пытается остановить адаптер
                        }
                        if (settings.Fields.Contains("members"))
                        {
                            List<SearchResult> members = GetObjectMembers(sr.GetDirectoryEntry());
                            objectToSend = Utils.SerializeObject(sr, settings.Fields, members);
                        }
                        else
                        {
                            objectToSend = Utils.SerializeObject(sr, settings.Fields);
                        }

                        ingoingManager.SendToESB(objectToSend, "ADGroup", settings.ClassId);
                    }
                }
            }
        }

        private List<SearchResult> GetObjectMembers(DirectoryEntry entry)
        {
            List<SearchResult> _members = new List<SearchResult>();
            foreach (object objMember in entry.Properties["member"])
            {
                DirectoryEntry _localDe = ldapConnection.GetDirectoryEntry(objMember.ToString());
                using (DirectorySearcher ds = new DirectorySearcher(_localDe)
                {
                    Filter = "(&(objectCategory=User)(objectClass=person))"
                })
                {
                    SearchResult res = ds.FindOne();
                    if (res != null)
                    {
                        _members.Add(res);
                    }
                }
            }
            return _members;
        }
    }
}