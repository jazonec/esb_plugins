using System;
using System.Collections.Generic;
using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;

namespace openplugins.AESServerClient
{
    public sealed class IngoingConnectionPointFactory : IIngoingConnectionPointFactory
    {
        public const string SETTINGS_PARAMETER = "Settings";

        IIngoingConnectionPoint IIngoingConnectionPointFactory.Create(Dictionary<string, string> parameters, IServiceLocator serviceLocator)
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

            return new IngoingRESTServer(settings, serviceLocator);
        }
    }
}
