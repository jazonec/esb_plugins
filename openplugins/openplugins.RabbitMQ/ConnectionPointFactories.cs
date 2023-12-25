using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace openplugins.RabbitMQ
{
    internal class IngoingConnectionPointFactory : IIngoingConnectionPointFactory
    {
        public const string SETTINGS_PARAMETER = "Settings";
        public IIngoingConnectionPoint Create(Dictionary<string, string> parameters, IServiceLocator serviceLocator)
        {
            IngoingSettings settings;
            if (!parameters.ContainsKey(SETTINGS_PARAMETER))
            {
                throw new ArgumentException(String.Format("Не задан параметр <{0}>", SETTINGS_PARAMETER));
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

            return new ConsumerManager(settings, serviceLocator);
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
                throw new ArgumentException(String.Format("Не задан параметр <{0}>", SETTINGS_PARAMETER));
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

            return new ProduserManager(settings, serviceLocator);
        }
    }
}
