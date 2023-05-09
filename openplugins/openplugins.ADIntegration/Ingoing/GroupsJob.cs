using Newtonsoft.Json.Linq;
using Quartz;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Threading.Tasks;

namespace openplugins.ADIntegration
{
    internal class GroupsJob : IJob
    {
        ReadGroups settings;
        IngoingManager ingoingManager;
        LdapConnection ldapConnection;
        public Task Execute(IJobExecutionContext context)
        {
            ingoingManager = context.JobDetail.JobDataMap["mainclass"] as IngoingManager;
            settings = context.JobDetail.JobDataMap["settings"] as ReadGroups;
            ldapConnection = context.JobDetail.JobDataMap["ldapConnection"] as LdapConnection;

            ingoingManager.WriteLogString("Cработало расписание на выгрузку групп");
            SendADGroupsToESB();
            ingoingManager.WriteLogString("Закончил выгрузку групп");
            return Task.CompletedTask;
        }
        private void SendADGroupsToESB()
        {
            using (DirectorySearcher ds = new DirectorySearcher(ldapConnection.De, settings.GroupFilter))
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

                        List<SearchResult> members = GetGroupMembers(sr.GetDirectoryEntry());
                        JObject objectToSend = Utils.SerializeGroup(sr, settings.Fields, members);

                        ingoingManager.SendToESB(objectToSend, "ADGroup", settings.ClassId);
                    }
                }
            }
        }
        private List<SearchResult> GetGroupMembers(DirectoryEntry objGroupEntry)
        {
            List<SearchResult> _members = new List<SearchResult>();
            foreach (object objMember in objGroupEntry.Properties["member"])
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