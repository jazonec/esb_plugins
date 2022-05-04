using ESB_ConnectionPoints.PluginsInterfaces;
using Newtonsoft.Json.Linq;

namespace openplugins.AESServerClient
{
    internal class OutgoingRESTClient : IOutgoingConnectionPoint
    {
        private JObject settings;
        private IServiceLocator serviceLocator;

        public OutgoingRESTClient(JObject settings, IServiceLocator serviceLocator)
        {
            this.settings = settings;
            this.serviceLocator = serviceLocator;
        }

        public void Dispose()
        {
        }
    }
}