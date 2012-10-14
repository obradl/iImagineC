using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Diagnostics;
using System.Threading;
using System.Reflection;

namespace iImagineC.WCF.Test
{
    [TestClass]
    public class SubscriberServiceTests
    {
        private Stopwatch _sw = new Stopwatch();

        [TestMethod]
        public void SubscriberService_PublishToAll_AllClientsTest()
        {
            using (var host = new WcfHost())
            {
                _sw.Start();

                //Register clients
                List<Client> clients = new List<Client>();
                for (int i = 0; i < 500; i++)
                {
                    clients.Add(new Client(i.ToString()));
                }
                Trace.TraceInformation("Client register took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();

                //Publish Data
                host.instance.PublishToAll("Roman");
                Trace.TraceInformation("Publishing to clients took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();

                //Wait for all data to come in
                clients.ForEach(a => a.OnData.WaitOne(100));
                Trace.TraceInformation("Waiting for data on the client side took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();

                //Check All data is received
                clients.ForEach(a => Assert.AreEqual("Roman", a.Data));
                Trace.TraceInformation("Checking client data took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();

                //Close clients
                clients.ForEach(a => a.Dispose());
                Trace.TraceInformation("Client disconnect took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();
            }
            Trace.TraceInformation("Shutting down host took {0} ms", _sw.ElapsedMilliseconds);
            _sw.Stop();
        }

        [TestMethod]
        public void SubscriberService_PublishToAll_DisconnectedClients()
        {
            using (var host = new WcfHost())
            {
                _sw.Start();

                //Register clients
                List<Client> clients = new List<Client>();
                for (int i = 0; i < 500; i++)
                {
                    clients.Add(new Client(i.ToString()));
                }
                Thread.Sleep(100);//Wait for wcf to finish registrations
                Assert.AreEqual(500, host.ActiveClients, "Active clients on the server should be 500");
                Trace.TraceInformation("Client register took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();

                //Simulate loosing some clients
                for (int i = 0; i < 500; i++)
                {
                    if (i.ToString().Contains("5") || i.ToString().Contains("2"))
                        clients[i].Dispose();
                }

                //Publish Data
                host.instance.PublishToAll("Roman");
                Trace.TraceInformation("Publishing to clients took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();

                //Wait for all data to come in
                clients.ForEach(a => a.OnData.WaitOne(100));
                Trace.TraceInformation("Waiting for data on the client side took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();

                //Check All data is received
                int clientsWithData = 0;
                clients.ForEach(a =>
                    {
                        if (a.Data != "closed")
                        {
                            Assert.AreEqual("Roman", a.Data);
                            clientsWithData++;
                        }
                    });
                Trace.TraceInformation("Clients with data {0}", clientsWithData);
                Trace.TraceInformation("Checking client data took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();

                //Check open clients on server
                Assert.IsTrue(host.ActiveClients < 500, "Active clients should be less than 500");
                Assert.AreEqual(clientsWithData, host.ActiveClients, "Active clients on server does not match clients");

                //Close clients
                clients.ForEach(a => a.Dispose());
                Trace.TraceInformation("Client disconnect took {0} ms", _sw.ElapsedMilliseconds);
                _sw.Restart();
            }
            Trace.TraceInformation("Shutting down host took {0} ms", _sw.ElapsedMilliseconds);
            _sw.Stop();
        }

        [TestMethod]
        public void SubscriberService_PublishToAll_StaleClients()
        {
            using (var host = new WcfHost())
            {
                List<Client> clients = new List<Client>();
                try
                {
                    _sw.Start();

                    //Register clients
                    for (int i = 0; i < 500; i++)
                    {
                        clients.Add(new Client(i.ToString()));
                    }
                    Thread.Sleep(100);//Wait for wcf to finish registrations
                    Assert.AreEqual(500, host.ActiveClients, "Active clients on the server should be 500");
                    Trace.TraceInformation("Client register took {0} ms", _sw.ElapsedMilliseconds);
                    _sw.Restart();

                    //Simulate loosing some clients
                    List<Client> toRemove = new List<Client>();
                    for (int i = 0; i < 500; i++)
                    {
                        if (i.ToString().Contains("5") || i.ToString().Contains("2"))
                            clients[i].Kill();
                    }
                    Trace.TraceInformation("Simulating dead clients took {0} ms", _sw.ElapsedMilliseconds);
                    _sw.Restart();

                    //Publish Data
                    host.instance.PublishToAll("Roman");
                    Trace.TraceInformation("Publishing to clients took {0} ms", _sw.ElapsedMilliseconds);
                    _sw.Restart();

                    //Wait for all data to come in
                    clients.ForEach(a => a.OnData.WaitOne(1000));
                    Trace.TraceInformation("Waiting for data on the client side took {0} ms", _sw.ElapsedMilliseconds);
                    _sw.Restart();
                    Thread.Sleep(1000);

                    //Check All data is received
                    int clientsWithData = 0;
                    int rogueClients = 0;
                    clients.ForEach(a =>
                    {
                        if (a.Data != "dead")
                        {
                            //Assert.AreEqual("Roman", a.Data);
                            if (a.Data != "Roman") rogueClients++;
                            clientsWithData++;
                        }
                    });
                    Trace.TraceInformation("Clients with WRONG DATA {0}", rogueClients);
                    Trace.TraceInformation("Clients with data {0}", clientsWithData);
                    Trace.TraceInformation("Checking client data took {0} ms", _sw.ElapsedMilliseconds);
                    Trace.Flush();
                    _sw.Restart();

                    //Re-Publish to clean up dead clients
                    Assert.IsTrue(host.instance.PublishToAllSerial("Again", 20000), "Previous publish is in progress");

                    //Check open clients on server
                    Trace.TraceInformation("Active clients {0}, Clients with Data {1}", host.ActiveClients, clientsWithData);
                    Assert.IsTrue(host.ActiveClients < 500, "Active clients should be less than 500");
                    Assert.AreEqual(clientsWithData, host.ActiveClients, "Active clients on server does not match clients");

                }
                finally
                {
                    //Close clients
                    clients.ForEach(a => a.Dispose());
                    Trace.TraceInformation("Client disconnect took {0} ms", _sw.ElapsedMilliseconds);
                    _sw.Restart();
                }
            }
            Trace.TraceInformation("Shutting down host took {0} ms", _sw.ElapsedMilliseconds);
            _sw.Stop();
        }

        [TestMethod]
        public void TestWhenWCFCallBackIsNotOpen()
        {
            using (var host = new WcfHost())
            {
                Client client = new Client("Roman");
                Thread.Sleep(100);

                var state = host.GetClientChannel().State;
                Assert.AreEqual(CommunicationState.Opened, state, "Initial state");

                client.Kill();
                state = host.GetClientChannel().State;
                Assert.AreEqual(CommunicationState.Opened, state, "stale state");

                host.instance.PublishToAll("Test");
                Thread.Sleep(300);

                state = host.GetClientChannel().State;
                Assert.AreEqual(CommunicationState.Closed, state, "Final state");

                Assert.AreEqual(1, host.ActiveClients, "Active clients should be 1");

                host.instance.PublishToAll("Test");
                Thread.Sleep(300);

                Assert.AreEqual(0, host.ActiveClients, "Active clients should be 0");

            }
        }
    }
}
