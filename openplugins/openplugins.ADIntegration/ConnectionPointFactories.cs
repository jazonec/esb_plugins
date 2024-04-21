using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace openplugins.ADIntegration
{
    public sealed class IngoingConnectionPointFactory : IIngoingConnectionPointFactory
    {
        public const string SETTINGS_PARAMETER = "Settings";

        IIngoingConnectionPoint IIngoingConnectionPointFactory.Create(Dictionary<string, string> parameters, IServiceLocator serviceLocator)
        {
            IngoingSettings settings;
            //JObject settings;
            if (!parameters.ContainsKey(SETTINGS_PARAMETER))
            {
                throw new ArgumentException(string.Format("Не задан параметр <{0}>", SETTINGS_PARAMETER));
            }
            var settingsString = parameters[SETTINGS_PARAMETER];

            try
            {
                settings = JsonConvert.DeserializeObject<IngoingSettings>(settingsString);
            }
            catch (Exception ex)
            {
                serviceLocator.GetLogger(GetType()).Error(ex);
                throw new FormatException("Некоректный json с настройками");
            }

            return new IngoingManager(settings, serviceLocator);
        }
    }
    public sealed class OutgoingConnectionPointFactory : IOutgoingConnectionPointFactory
    {
        public const string SETTINGS_PARAMETER = "Settings";
        public IOutgoingConnectionPoint Create(Dictionary<string, string> parameters, IServiceLocator serviceLocator)
        {
            OutgoingSettings settings;
            if (!parameters.ContainsKey(SETTINGS_PARAMETER))
            {
                throw new ArgumentException(string.Format("Не задан параметр <{0}>", SETTINGS_PARAMETER));
            }
            var settingsString = parameters[SETTINGS_PARAMETER];

            try
            {
                settings = JsonConvert.DeserializeObject<OutgoingSettings>(settingsString);
            }
            catch (Exception ex)
            {
                serviceLocator.GetLogger(GetType()).Error(ex);
                throw new FormatException("Некоректный json с настройками");
            }

            return new OutgoingManager(settings, serviceLocator);
        }
    }
}
