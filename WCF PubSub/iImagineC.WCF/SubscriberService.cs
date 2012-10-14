using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace iImagineC.WCF
{
    /// <summary>
    /// InstanceContextMode = InstanceContextMode.Single Indicates that only one instance of this class will service all the WCF calls
    /// ConcurrencyMode = ConcurrencyMode.Multiple Indicates that a number of simultaneous calls on worker threads could call methods concurrently
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, IncludeExceptionDetailInFaults = true)]
    public class SubscriberService : ISubscriberService, IPublisher
    {
        //====================================================================================================
        private readonly object _locker = new object();
        private readonly AutoResetEvent _publishInProgress = new AutoResetEvent(true);
        private readonly List<Subscriber> _subscribers = new List<Subscriber>();
        private readonly Action<string, object[]> _onError;
        //====================================================================================================

        //====================================================================================================
        public SubscriberService() : this(null){}
        public SubscriberService(Action<string, object[]> onError)
        {
            if (onError == null) onError = (s, e) => Trace.TraceError(s, e);
            _onError = onError;
        }
        //====================================================================================================

        //====================================================================================================
        /// <summary>
        /// Registers a WCF subscriber/client to receive PublishToAll data.
        /// </summary>
        /// <param name="name">Identifier for logging purposes only.</param>
        public void Subscribe(string name)
        {
            try
            {
                var callback = OperationContext.Current.GetCallbackChannel<ISubscribedClient>();
                var subscriber = new Subscriber(callback, name);

                lock (_locker)
                {
                    _subscribers.Remove(subscriber);//kill any old subscriptions if Subscribed was called twice.
                    _subscribers.Add(subscriber);
                }
                //Can log successful subscriptions here
                Trace.TraceInformation("Client {0} subscribed.", name);
            }
            catch (Exception e)
            {
                _onError("Unable to subscribe client {0}. {1}", new object[]{name, e});
                throw;//Subscribe is NOT a OneWay, so exceptions will propogate to let the client know they did not subscribe.
            }
        }
        //====================================================================================================
        public void Unsubscribe()
        {
            try
            {
                var callback = OperationContext.Current.GetCallbackChannel<ISubscribedClient>();
                Subscriber subscriber;

                lock (_locker)
                {
                    subscriber = _subscribers.FirstOrDefault(a => a.Channel == callback);
                    _subscribers.Remove(subscriber);
                }

                //Can log successful unsubscriptions here
                if (subscriber != null)
                    Trace.TraceInformation("Client {0} unsubscribed.", subscriber.Name);
                else Trace.TraceWarning("Client could not be found to unsubscribe.");
            }
            catch (Exception e)
            {
                _onError("Unable to unsubscribe client. {0}", new object[] { e });
                //throw;//Unsubscribe is marked as OneWay, exceptions will not propogate anyway. (Can enable for WCF logging).
            }
        }
        //====================================================================================================
        /// <summary>
        /// Blocks to call PublishToAll if there are active publish calls in progress.
        /// 
        /// This does NOT guarantee synchronization unless used exclusively. 
        /// If PublishToAllSerial is used exclusively (PublishToAll is not used) then all calls to publish
        /// will be serialised (this call will wait for any previous publish call to finish).
        /// 
        /// Note: if the client callback is marked as OneWay this does NOT guarantee that the data will reach the client in order.
        /// If data order is important on the client side (eg, sending deltas). Then mark ISubscribedClient Callback as two way.
        /// </summary>
        /// <param name="data">Data to publish</param>
        /// <param name="serialPublishMaxWaitTime">time to wait for previous publish to finish.</param>
        /// <returns>False if timed out waiting for previous publish to finish, otherwise true.</returns>
        public bool PublishToAllSerial(string data, int serialPublishMaxWaitTime = 10000)
        {
            if (!_publishInProgress.WaitOne(serialPublishMaxWaitTime))
                return false;
            
            PublishToAll(data);
            return true;
        }
        //====================================================================================================
        /// <summary>
        /// Publishes data to all subscribers/clients paralleled out on a worker thread.
        /// </summary>
        /// <param name="data">Data to publish.</param>
        public void PublishToAll(string data)
        {
            _publishInProgress.Reset();//If flagged as set (no wait) reset to make PublishToAllSerial wait

            List<Subscriber> listeners;
            lock (_locker)
            {
                var toRemove = _subscribers.Where(a => ((ICommunicationObject)a.Channel).State != CommunicationState.Opened).ToList();
                toRemove.ForEach(a => _subscribers.Remove(a));
                listeners = _subscribers.ToList();
            }

            ThreadPool.QueueUserWorkItem(_ =>
                {
                    Parallel.ForEach(listeners, subscriber =>
                    {
                        try { subscriber.Channel.Callback(data); }
                        //To catch transfer related exceptions, client callback must NOT be marked as one way.
                        catch (Exception e) { _onError("Failed to send data to {0}. {1}", new object[]{ subscriber.Name, e }); }
                    });

                    Trace.TraceInformation("Finished publishing data to all subscribers");
                    _publishInProgress.Set();
                });
        }
        //====================================================================================================
    }

    internal class Subscriber : IEquatable<Subscriber>
    {
        //====================================================================================================
        private readonly ISubscribedClient _channel;
        private readonly string _name;
        //====================================================================================================

        //====================================================================================================
        public Subscriber(ISubscribedClient channel, string name)
        {
            if (channel == null) throw new ArgumentNullException("channel", "ISubscribedClient channel can not be null");
            _channel = channel;
            _name = name;
        }
        //====================================================================================================

        //====================================================================================================
        public ISubscribedClient Channel { get { return _channel; } }
        public string Name { get { return _name; } }
        //====================================================================================================

        //====================================================================================================
        public bool Equals(Subscriber other)
        {
            if (other == null) return false;
            return this._channel.Equals(other._channel);
        }
        public override bool Equals(object obj)
        {
            return this.Equals(obj as Subscriber);
        }
        public override int GetHashCode()
        {
            return _channel.GetHashCode();
        }
        //====================================================================================================
    }
}
