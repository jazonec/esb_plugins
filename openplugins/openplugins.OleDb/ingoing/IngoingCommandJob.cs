using Newtonsoft.Json.Linq;
using Quartz;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System;

namespace openplugins.OleDb
{
    internal class IngoingCommandJob : IJob
    {
        IngoingCommandSettings settings;
        IngoingManager ingoingManager;
        PluginDbConnection dbConnection;
        public Task Execute(IJobExecutionContext context)
        {
            ingoingManager = context.JobDetail.JobDataMap["mainclass"] as IngoingManager;
            settings = context.JobDetail.JobDataMap["settings"] as IngoingCommandSettings;
            dbConnection = ingoingManager.dbConnection;

            ingoingManager.WriteLogString(string.Format("Сработало расписание выполнения команды '{0}'", settings.Name));

            try
            {
                ExecuteCommand();
            }
            catch (Exception ex)
            {
                ingoingManager.WriteError(string.Format("Ошибка при выполнении '{0}'", settings.Name), ex);
            }

            return Task.CompletedTask;
        }

        private void ExecuteCommand()
        {
            DataTable dt = dbConnection.GetDataTable(settings.SQL);
            JObject dtSerialized = new JObject
            {
                { "RecordSet", Utils.Serialize(dt) }
            };
            if (settings.SendEachRow)
            {
                JArray list = (JArray)dtSerialized["RecordSet"];
                for (int i = 0; i < list.Count; i++)
                {
                    JObject row = (JObject)list[i];
                    ingoingManager.SendMessagetoESB(row.ToString(), settings.MessageType, settings.MessageClassId);
                    ExecuteAfterSend(row);
                }
            }
            else
            {
                ingoingManager.SendMessagetoESB(dtSerialized.ToString(), settings.MessageType, settings.MessageClassId);
                ExecuteAfterSend(dtSerialized);
            }
        }
        private void ExecuteAfterSend(JObject data)
        {
            if (settings.ExecuteAfterSend)
            {
                if (settings.CommandAfterSend == null)
                {
                    throw new ArgumentNullException("CommandAfterSend", "Отсутствуют настройки команды постобработки!");
                }
                try
                {
                    List<CommandParameterWithData> parameters = Utils.GetParametersFromRow(data, settings.CommandAfterSend.Parameters);
                    int rowAffected = dbConnection.ExecuteNonQuery(settings.CommandAfterSend, parameters);
                    ingoingManager.WriteLogString(string.Format("AfterSend rows affected: {0}", rowAffected));
                }
                catch (Exception ex)
                {
                    ingoingManager.WriteError("Ошибка выполнения команды постобработки!", ex);
                }
            }
        }
    }
}