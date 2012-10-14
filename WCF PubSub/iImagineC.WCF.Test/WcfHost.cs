using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Principal;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Reflection;
using System.Collections;

namespace iImagineC.WCF.Test
{
    public class WcfHost : IDisposable
    {
        public SubscriberService instance;
        ServiceHost host;

        public WcfHost()
        {
            instance = new SubscriberService();
            host = new ServiceHost(instance, new Uri("net.tcp://localhost:8600"));
            Binding binding = new NetTcpBinding(SecurityMode.Transport);

            host.AddServiceEndpoint(typeof(ISubscriberService).FullName, binding, "net.tcp://localhost:8600/Test");
            host.Open();
        }

        public void Dispose()
        {
            try { host.Close(); }
            catch { }
        }

        public int ActiveClients
        {
            get
            {
                BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                var result = instance.GetType().GetField("_subscribers", bindingFlags).GetValue(instance) as ICollection;

                return result.Count;
            }
        }

        public ICommunicationObject GetClientChannel()
        {
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var list = instance.GetType().GetField("_subscribers", bindingFlags).GetValue(instance) as IList;
            var sub = list[0];
            var result = sub.GetType().GetField("_channel", bindingFlags).GetValue(sub) as ICommunicationObject;

            return result as ICommunicationObject;
        }
    }
}
