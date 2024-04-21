using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Quartz;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace openplugins.OleDb
{
    internal class Utils
    {
        public static IJobDetail CreateJob(
            string name, IConnectionPoint mainClass = null, object settings = null)
        {
            IJobDetail jobDetail = JobBuilder.Create<IngoingCommandJob>()
                .WithIdentity(name, "group")
                .Build();
            jobDetail.JobDataMap["mainclass"] = mainClass;
            jobDetail.JobDataMap["settings"] = settings;
            return jobDetail;
        }
        public static ITrigger CreateTrigger(string name, string cron)
        {
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity(name + "_trigger", "group")
                .WithCronSchedule(cron)
                .StartNow()
                .Build();
            return trigger;
        }
        public static JArray Serialize(DataTable dt, SerializeFormat format = SerializeFormat.json)
        {
            if (format == SerializeFormat.json)
            {
                return JArray.Parse(JsonConvert.SerializeObject(dt));
            }
            else if (format == SerializeFormat.xml)
            {
                throw new NotImplementedException("XML-формат еще не реализован!");
            }
            else
            {
                throw new InvalidOperationException("Неизвестный формат сериализации!");
            }
        }

        internal static List<CommandParameterWithData> GetParametersFromRow(JObject row, List<Parameter> parameters)
        {
            List<CommandParameterWithData> result = new List<CommandParameterWithData>();
            foreach (Parameter par in parameters)
            {
                CommandParameterWithData parameter = new CommandParameterWithData
                {
                    ParameterType = par.ParameterType,
                    Name = par.Name
                };
                switch (par.ParameterType)
                {
                    case ParameterType.Int:
                        parameter.ValueInt = (int)row.SelectToken(par.JsonPath);
                        break;
                    case ParameterType.String:
                        parameter.ValueString = (string)row.SelectToken(par.JsonPath);
                        break;
                    case ParameterType.DateTime:
                        parameter.ValueDateTime = (DateTime)row.SelectToken(par.JsonPath);
                        break;
                    case ParameterType.Float:
                        parameter.ValueFloat = (float)row.SelectToken(par.JsonPath);
                        break;
                    default:
                        throw new Exception("Unknown parameter type");
                }
                result.Add(parameter);
            }
            return result;
        }

        internal static string SerializeRow(object[] rowData, SerializeFormat format = SerializeFormat.json)
        {
            if (format == SerializeFormat.json)
            {
                return JsonConvert.SerializeObject(rowData);
            }
            else if (format == SerializeFormat.xml)
            {
                throw new NotImplementedException("XML-формат еще не реализован!");
            }
            else
            {
                throw new InvalidOperationException("Неизвестный формат сериализации!");
            }
        }
    }
    public enum SerializeFormat
    {
        json,
        xml
    }
    public class CustomDateTimeConverter : DateTimeConverterBase
    {
        private const string Format = "yyyy-MM-dd HH:mm:ss";

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((DateTime)value).ToString(Format));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
            {
                return null;
            }

            var s = reader.Value.ToString();
            if (DateTime.TryParseExact(s, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            return null;
        }
    }
}
