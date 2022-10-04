using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace openplugins.OleDb
{
    public class IngoingConnectionPointFactory : IIngoingConnectionPointFactory
    {
        public const string SETTINGS_PARAMETER = "Settings";
        public IIngoingConnectionPoint Create(Dictionary<string, string> parameters, IServiceLocator serviceLocator)
        {
            JObject settings;
            if (!parameters.ContainsKey(SETTINGS_PARAMETER))
            {
                throw new ArgumentException(String.Format("Не задан параметр <{0}>", SETTINGS_PARAMETER));
            }
            var settingsString = parameters[SETTINGS_PARAMETER];

            try
            {
                settings = JObject.Parse(settingsString);
            }
            catch (Exception ex)
            {
                serviceLocator.GetLogger(GetType()).Error(ex);
                throw new FormatException("Некоректный json с настройками");
            }

            return new IngoingOleDbClient(settings, serviceLocator);
        }
    }
}
