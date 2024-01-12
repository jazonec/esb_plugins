using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace openplugins.Reflectors
{
    public sealed class OutgoingConnectionPointFactory : IOutgoingConnectionPointFactory
    {
        public const string SETTINGS_PARAMETER = "Settings";
        public IOutgoingConnectionPoint Create(Dictionary<string, string> parameters, IServiceLocator serviceLocator)
        {
            if (!parameters.ContainsKey(SETTINGS_PARAMETER))
            {
                throw new ArgumentException(string.Format("Не задан параметр <{0}>", SETTINGS_PARAMETER));
            }
            string settingsString = parameters[SETTINGS_PARAMETER];

            PluginSettings settings = new PluginSettings();
            try
            {
                settings = JsonConvert.DeserializeObject<PluginSettings>(settingsString);
            }
            catch (Exception ex)
            {
                serviceLocator.GetLogger(GetType()).Error(ex);
                throw new FormatException("Некоректный json с настройками");
            }

            return new ReflectorManager(settings, serviceLocator);
        }
    }
}
