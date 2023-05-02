using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

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
            var settingsString = parameters[SETTINGS_PARAMETER];

            JObject settings;
            try
            {
                settings = JObject.Parse(settingsString);
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
