using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Reflection;

namespace iImagineC.WCF.Test
{
    class Client : ISubscribedClient, IDisposable
    {
        private readonly InstanceContext _instance;
        private readonly DuplexChannelFactory<ISubscriberService> _factory;
        private readonly ISubscriberService _chan;
        public readonly AutoResetEvent OnData = new AutoResetEvent(false);
        public string Data = "";

        public Client(string name)
        {
            string url = "net.tcp://localhost:8600/Test";

            _instance = new InstanceContext(this);
            Binding binding = new NetTcpBinding(SecurityMode.Transport);
            binding.OpenTimeout = TimeSpan.FromSeconds(1);
            binding.ReceiveTimeout = TimeSpan.FromSeconds(1);
            binding.SendTimeout = TimeSpan.FromSeconds(1);
            binding.CloseTimeout = TimeSpan.FromSeconds(1);

            _factory = new DuplexChannelFactory<ISubscriberService>(_instance, binding, new EndpointAddress(url));

            _chan = _factory.CreateChannel();
            _chan.Subscribe(name);
        }

        public void Callback(string data)
        {
            Data = data;
            OnData.Set();
        }

        public void Unsubscribe()
        {
            _chan.Unsubscribe();
        }

        public void Kill()
        {
            CallMethod(_instance, "FaultInternal");
            Data = "dead"; OnData.Set();
        }

        public void Dispose()
        {
            try { if (_factory != null) _factory.Close(); Data = "closed"; OnData.Set(); }
            catch (Exception) { }
        }

        private void CallMethod(object instance, string name)
        {
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo minfo = instance.GetType().GetMethod(name, bindingFlags);

            minfo.Invoke(instance, null);
        }
    }
}
