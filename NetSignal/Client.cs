using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Runtime.InteropServices;
namespace NetSignal
{
    public struct ConnectionData
    {
        public string serverIp;
        public int listenPort;
        public int sendToPort;
        public IPEndPoint toSendToThis;
        public IPEndPoint thisListensTo;

        public IPEndPoint toSendToServer;

        public int clientID;

        public override string ToString()
        {
            return "server IP : " + serverIp + " server port " + listenPort + " sendtoendpoint " + toSendToThis + " listentoendpoint " + thisListensTo + " clientID " + clientID;
        }
    }

    [Serializable]
    public struct FloatDataPackage
    {
        public int id;
        public DateTime timeStamp;
        public float data;

        public override string ToString()
        {
            return "id : " + id + " timestamp " + timeStamp + " data " + data;
        }
    }

    
    public class SignalCompressor
    {
        public static string Compress(FloatDataPackage package)
        {

            return package.id.ToString("00000000000000000000000000000000") +
                package.timeStamp.Ticks.ToString("00000000000000000000000000000000") +
                BitConverter.DoubleToInt64Bits((double)package.data).ToString("00000000000000000000000000000000");
            /*return Convert.ToString( package.id, 2).PadLeft(32,'0')
                + Convert.ToString(package.timeStamp.Ticks,2).PadLeft(64, '0')
                + Convert.ToString(BitConverter.DoubleToInt64Bits((double)package.data),2).PadLeft(64, '0');*/
        }

        public static FloatDataPackage Decompress(string compressed)
        {
            
            //TODO OMG MEMORY
            FloatDataPackage p = new FloatDataPackage();
            p.id = int.Parse(compressed.Substring(0, 32));
            p.timeStamp = new DateTime(Int64.Parse(compressed.Substring(32, 32)));
            p.data = (float)BitConverter.Int64BitsToDouble(long.Parse(compressed.Substring(64, 32)));
            return p;
        }
    }

    public struct IncomingSignal
    {
        public FloatDataPackage data
        {
            get; internal set;
        }

        public override string ToString()
        {
            return "data : " + data;
        }
    }

    public struct OutgoingSignal
    {
        private FloatDataPackage dataMember;//TODO WARNING?!

        public FloatDataPackage data
        {
            set
            {
                if (!dataMember.data.Equals(value.data))
                {
                    dataDirty = true;
                    dataMember.timeStamp = DateTime.UtcNow;//TODO
                }
                dataMember = value;
            }
            internal get { return dataMember; }
        }

        public bool dataDirty;

        public override string ToString()
        {
            return "data : " + data + " dataDirty " + dataDirty;
        }
    }

    public struct Connection
    {
        public UdpClient udpClient;
        public TcpClient tcpClient;
        public TcpListener tcpListener;
    }

    public struct ConnectionMapping
    {
        //maps ip endpoints to index that identifies a client over the session.
        public Dictionary<IPEndPoint, int> EndPointToClientIdentification;

        public Dictionary<int, IPEndPoint> ClientIdentificationToEndpoint;
    }

    public class ConnectionUpdater
    {
        public static void InitializeMultiConnection(ref Connection connectors, ref ConnectionData connectionData)
        {
            connectors = new Connection();

            connectionData.thisListensTo = new IPEndPoint(IPAddress.Any, connectionData.listenPort);
            //connectionData.listenToEndpoint = new IPEndPoint(IPAddress.Parse(connectionData.serverIp), connectionData.listenPort);
            connectors.udpClient = new UdpClient(connectionData.thisListensTo);
            //connectors.udpClient = new UdpClient();
            //connectors.udpClient.Connect(connectionData.listenToEndpoint);//make the udp client react only to incoming messages that come from the given port
            Console.WriteLine("server: udpclient local: " + (IPEndPoint)connectors.udpClient.Client.LocalEndPoint);
//            Console.WriteLine("server: udpclient remote: " + (IPEndPoint)connectors.udpClient.Client.RemoteEndPoint);

            connectors.tcpListener = new TcpListener(IPAddress.Any, connectionData.listenPort);
            connectors.tcpClient = null;
        }

        public static void InitializeSingleConnection(ref Connection connectors, ref ConnectionData connectionData)
        {
            connectors = new Connection();
            try
            {
                //connectionData.listenToEndpoint = new IPEndPoint(IPAddress.Parse(connectionData.serverIp), connectionData.listenPort);
                connectionData.thisListensTo = new IPEndPoint(IPAddress.Any, connectionData.listenPort);

                connectionData.toSendToThis = new IPEndPoint(IPAddress.Parse(connectionData.serverIp), connectionData.sendToPort);

                connectors.udpClient = new UdpClient(connectionData.thisListensTo);
                Console.WriteLine("client connects to udp" + connectionData.listenPort);
                //connectors.udpClient = new UdpClient(connectionData.listenToEndpoint);
                //connectors.udpClient.Connect(connectionData.listenToEndpoint);//make the udp client react only to incoming messages that come from the given port

                connectors.tcpListener = null;
                connectors.tcpClient = new TcpClient();

                Console.WriteLine("client connects to tcp" + connectionData.serverIp + ":" + connectionData.sendToPort + " " + connectionData.toSendToThis);
                connectors.tcpClient.Connect(connectionData.toSendToThis);

                NetworkStream stream = connectors.tcpClient.GetStream();
                //Byte[] data = Encoding.ASCII.GetBytes("hallihallo"); //TODO there is only one type of message right now

                //var myLocalUdpEndpoint = connectionData.listenToEndpoint;
                //Byte[] data = Encoding.ASCII.GetBytes(myLocalUdpEndpoint.Address.ToString() + "|" + myLocalUdpEndpoint.Port);

                Byte[] data = Encoding.ASCII.GetBytes(connectionData.listenPort.ToString());//only send the port we are listening to over udp

                stream.Write(data, 0, data.Length);
                data = new Byte[256];
                string response = null;
                var byteCount = stream.Read(data, 0, data.Length);
                response = Encoding.ASCII.GetString(data, 0, byteCount);
                var myClientID = int.Parse(response);
                connectionData.clientID = myClientID;
                Console.WriteLine("i am client " + myClientID);

                stream.Close();
                connectors.tcpClient.Close(); //TODO reevaluate: open and close on every message?!
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
        }

        public static void ShutDownConnection(ref Connection connectors)
        {
            if (connectors.udpClient != null)
            {
                connectors.udpClient.Close();
            }
            if (connectors.tcpClient != null)
            {
                connectors.tcpClient.Close();
            }
            if (connectors.tcpListener != null)
            {
                connectors.tcpListener.Stop();
            }
        }

        public static void StartThreadAcceptTCPConnections(ConnectionMapping connectionMapping, Connection by, Connection[] storeToConnections, ConnectionData[] storeToConnectionDatas, Func<bool> cancel, Action report)
        {
            if (connectionMapping.EndPointToClientIdentification == null)
                throw new NullReferenceException("need to init mapping otherwise will not be referable");

            Action accept = () => AcceptTCPConnections(connectionMapping, by, storeToConnections, storeToConnectionDatas, cancel, report);
            var thread = new Thread(new ThreadStart(accept));
            thread.Start();
        }

        public static void AcceptTCPConnections(ConnectionMapping connectionMapping, Connection by, Connection[] storeToConnections, ConnectionData[] storeToConnectionDatas, Func<bool> cancel, Action report)
        {
            try
            {
                by.tcpListener.Start();
                Byte[] bytes = new byte[256];
                string data = null;
                while (!cancel())
                {
                    //waiting for connection
                    TcpClient connection = by.tcpListener.AcceptTcpClient();

                    data = null;

                    NetworkStream stream = connection.GetStream();

                    int i;
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        //TODO here we need user defined filtering to deserialize into actual IncomingSignals!
                        //expect ud endpoint
                        data = Encoding.ASCII.GetString(bytes, 0, i);

                        //incoming connection, try to identify
                        IPEndPoint clientEndpoint;
                        int clientID;
                        IdentifyClient(data, connectionMapping, storeToConnections, connection, out clientEndpoint, out clientID);

                        if (clientID == -1)
                        {
                            //sth went wrong
                            connection.Close();
                            continue;
                        }

                        //safe the client
                        storeToConnections[clientID].tcpClient = connection;
                        storeToConnectionDatas[clientID].thisListensTo = clientEndpoint;
                        storeToConnectionDatas[clientID].clientID = clientID;

                        Console.WriteLine("tcp received: " + data + " , will send back id " + clientID);

                        byte[] response = Encoding.ASCII.GetBytes(clientID.ToString());
                        stream.Write(response, 0, response.Length);
                    }
                    stream.Close();
                    connection.Close();
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                by.tcpListener.Stop();
            }
        }

        private static void IdentifyClient(string fromTCPMessage, ConnectionMapping connectionMapping, Connection[] storeToConnections, TcpClient connection, out IPEndPoint clientEndpoint, out int clientID)
        {
            //clientEndpoint = (IPEndPoint)connection.Client.RemoteEndPoint;
            //var splitIPAndPort = fromTCPMessage.Split('|');
            var connnectedToIPAddress = ((IPEndPoint)connection.Client.RemoteEndPoint).Address;
            var dataContainingListenPort = int.Parse(fromTCPMessage);
            //clientEndpoint = new IPEndPoint(IPAddress.Parse(splitIPAndPort[0]), int.Parse(splitIPAndPort[1]));
            clientEndpoint = new IPEndPoint(connnectedToIPAddress, dataContainingListenPort);

            Console.WriteLine("try to identify client with endpoint " + clientEndpoint);
            clientID = -1;
            //know this guy already
            if (connectionMapping.EndPointToClientIdentification.ContainsKey(clientEndpoint))
                clientID = connectionMapping.EndPointToClientIdentification[clientEndpoint];
            else  //create new ID
            {
                int connectionI;
                //find a free one
                for (connectionI = 0; connectionI <= storeToConnections.Length
                    && connectionMapping.ClientIdentificationToEndpoint.ContainsKey(connectionI); connectionI++)
                {
                }
                if (connectionI < storeToConnections.Length) //there was a free slot
                {
                    clientID = connectionI;
                    connectionMapping.ClientIdentificationToEndpoint.Add(connectionI, clientEndpoint);
                    connectionMapping.EndPointToClientIdentification.Add(clientEndpoint, connectionI);
                }
            }
        }
    }

    public class SignalUpdater
    {
        public static void StartThreadSyncSignalsToAll(Connection with, OutgoingSignal[] signals, Func<bool> cancel, params ConnectionData[] all)
        {
            Action sync = () => SyncSignalsToAll(with, signals, cancel, all);
            var thread = new Thread(new ThreadStart(sync));
            thread.Start();
        }


        public static void SyncSignalsToAll(Connection with,  OutgoingSignal[] signals, Func<bool> cancel, params ConnectionData[] all)
        {
            try
            {
                while (!cancel())
                {
                    foreach (var to in all)
                    {
                        for(int signalI = 0; signalI < signals.Length; signalI ++)
                        {
                            if (signals[signalI].dataDirty)
                            {
                                IPEndPoint toSendTo = to.thisListensTo;
                                if (to.thisListensTo.Address == IPAddress.Any) //can not send to any, send to serverIP instead
                                {
                                    toSendTo = new IPEndPoint(IPAddress.Parse(to.serverIp), to.thisListensTo.Port);
                                }
                                Console.WriteLine("data is dirty. send it to " + to.thisListensTo);
                                byte[] toSend = Encoding.ASCII.GetBytes(SignalCompressor.Compress(signals[signalI].data));
                                with.udpClient.Send(toSend, toSend.Length, toSendTo);
                                signals[signalI].dataDirty = false;
                            }
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
            finally
            {
            }
        }

        public static void StartThreadReceiveSignals(Connection connection, ConnectionData connectionData, IncomingSignal[] signals, Func<bool> cancel, Action<string> report)
        {
            Action receive = () => ReceiveSignals(connection, connectionData, signals, cancel, report);
            var thread = new Thread(new ThreadStart(receive));
            thread.Start();
        }

        public static void ReceiveSignals(Connection connection, ConnectionData connectionData, IncomingSignal[] signals, Func<bool> cancel, Action<string> report)
        {
            try
            {
                IPEndPoint receiveFrom = new IPEndPoint(connectionData.thisListensTo.Address, connectionData.thisListensTo.Port);
                while (!cancel())
                {
                    //Console.WriteLine("I (" + connectionData.listenToEndpoint + ") connect to " + receiveFrom);
                    //connection.udpClient.Connect(receiveFrom);
                    var bytes = connection.udpClient.Receive(ref receiveFrom);
                    Console.WriteLine("I (" + connectionData.thisListensTo + ") received sth from (?)" + receiveFrom);
                    //connection.udpClient.Close();
                    //if (receiveFrom != connectionData.listenToEndpoint)
                    //    continue;
                    Console.WriteLine("receive from (?)" + connectionData.thisListensTo);
                    Console.WriteLine("parse " + bytes.ToString() + " # " + bytes.Length  + " from " + receiveFrom);
                    var parsedString = Encoding.ASCII.GetString(bytes);
                    Console.WriteLine("report " + parsedString);
                    report(parsedString);
                    var package = SignalCompressor.Decompress(parsedString);
                    signals[package.id].data = package;
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                connection.udpClient.Close();
            }
        }
    }

    public class NetSignalStarter
    {
        public static void StartServer(ref Connection serverConnection, ref ConnectionData serverData, Func<bool> cancel, ref ConnectionMapping connectionMapping, Connection[] connections, ConnectionData[] connectionDatas, OutgoingSignal[] outgoingSignals, IncomingSignal[] incomingSignals)
        {
            //serverData.listenPort = 3000;
            //serverData.serverIp = null;
            ConnectionUpdater.InitializeMultiConnection(ref serverConnection, ref serverData);

            connectionMapping.ClientIdentificationToEndpoint = new Dictionary<int, IPEndPoint>();
            connectionMapping.EndPointToClientIdentification = new Dictionary<IPEndPoint, int>();

            Console.WriteLine(serverConnection.udpClient);

            SignalUpdater.StartThreadReceiveSignals(serverConnection, serverData, incomingSignals, cancel, (string s) => Console.WriteLine(s));

            ConnectionUpdater.StartThreadAcceptTCPConnections(connectionMapping, serverConnection, connections, connectionDatas, cancel, () => { });

            SignalUpdater.StartThreadSyncSignalsToAll(serverConnection, outgoingSignals, cancel, connectionDatas);
        }

        public static void StartClient(ref Connection clientCon, ref ConnectionData clientData,ref ConnectionData toServer, Func<bool> cancel, IncomingSignal[] incomingSignals, OutgoingSignal[] outgoingSignals)
        {
            /*clientData.listenPort = 3001;
            clientData.sendToPort = 3000;
            clientData.serverIp = "127.0.0.1";*/
            ConnectionUpdater.InitializeSingleConnection(ref clientCon, ref clientData);

            SignalUpdater.StartThreadReceiveSignals(clientCon, clientData, incomingSignals, cancel, (string s) => Console.WriteLine(s));

            SignalUpdater.StartThreadSyncSignalsToAll(clientCon, outgoingSignals, cancel, toServer);

            //var datagram = Encoding.ASCII.GetBytes("hellosent");
            //clientCon.udpClient.Send(datagram, datagram.Length);
        }

        /*
         * TEST for immediate:
         *
         * using NetSignal;
        OutgoingSignal[] signalSentFromServer = new OutgoingSignal[2];
        IncomingSignal[] signalSeenFromClient = new IncomingSignal[2];
        var cancel = false;
        var shouldPrint = false;
        ConnectionData[] consFromServer = new ConnectionData[1];
        Connection[] conFromServer = new Connection[1];
        Connection server = new Connection();
        ConnectionData serverD = new ConnectionData();
        var connectionMapping = new ConnectionMapping();
        NetSignalStarter.StartServer(ref server, ref serverD, () => cancel, ref connectionMapping, conFromServer, consFromServer, signalSentFromServer);
        Connection client = new Connection();
        ConnectionData clientD = new ConnectionData();
        NetSignalStarter.StartClient(ref client, ref clientD, () => cancel, signalSeenFromClient);
        System.Net.Sockets.UdpClient
        try to identify client with endpoint 127.0.0.1:55030
        tcp received: 127.0.0.1|55030 , will send back id 0
        i am client 0
        receive127.0.0.1:3123
        var datagram = Encoding.ASCII.GetBytes("hellosent");
        server.udpClient.Send(datagram, datagram.Length, "127.0.0.1", 53512);
        server.udpClient.Send(datagram, datagram.Length, "127.0.0.1", 55030);
        */

        public async static void Test()
        {
            OutgoingSignal[] signalSentFromServer = new OutgoingSignal[2];
            IncomingSignal[] signalSeenFromClient = new IncomingSignal[2];
            OutgoingSignal[] signalsSentFromClient = new OutgoingSignal[2];
            IncomingSignal[] signalsSeenFromServer = new IncomingSignal[2];
            var cancel = false;
            var shouldPrint = false;
            ConnectionData[] consFromServer = new ConnectionData[1];
            Connection[] conFromServer = new Connection[1];
            //NetSignalStarter.TestIncomingOnClient(() => cancel, () => shouldPrint, signalSentFromServer, signalSeenFromClient, signalsSentFromClient, signalsSeenFromServer, conFromServer, consFromServer);
            //await Task.Delay(5000);
            NetSignalStarter.TestDuplex(() => cancel, () => shouldPrint, signalSentFromServer, signalSeenFromClient, signalsSentFromClient, signalsSeenFromServer, conFromServer, consFromServer);

            //TODOS:
            //move from synchronous to ASYNC send and receive.
            //do not always open and close new tcp connection?
            //implement sync and receive signals RELIABLE version (over tcp)
            //refactor into separate files
            //implement websocket for matchmaking (to find ip to connect to server), set up with strato (?) 
            //import to unity
            //battletest: make scriptable objects that have Incoming- and Outgoing Signals, write Mono Updaters that assign Signals to specific game objects (mainly: Bird slots, state enums for UI)
        }



        public async static void TestDuplex(
        Func<bool> cancel,
        Func<bool> shouldReport,
        OutgoingSignal[] signalsSentFromServer,
        IncomingSignal[] signalsSeenFromClient,
        OutgoingSignal[] signalsSentFromClient,
        IncomingSignal[] signalsSeenFromServer,
        Connection[] clientConnectionsSeenFromServer,
        ConnectionData[] clientConnectionDatasSeenFromServer
        )
        {

            Connection server = new Connection();
            ConnectionData serverData = new ConnectionData();
            ConnectionMapping mapping = new ConnectionMapping();

            Connection client0 = new Connection();
            ConnectionData clientData0 = new ConnectionData();

            serverData.listenPort = 3000;
            serverData.serverIp = "127.0.0.1";

            clientData0.listenPort = 3001;
            clientData0.serverIp = "127.0.0.1";
            clientData0.sendToPort = 3000;




            StartServer(ref server, ref serverData, cancel, ref mapping, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer,
                signalsSentFromServer, signalsSeenFromServer);

            StartClient(ref client0, ref clientData0, ref serverData, cancel, signalsSeenFromClient, signalsSentFromClient);

            //Server and Client are not listening/sending to the right endpoint?!
            //server listens to Ip.any, 3000 , initialized with UdpClient(...), whereas client doesnt listen, is init with UdpClient() and then .Connect(...) . what exactly is the differnece?

            if (!cancel())
            {
                //if(shouldReport())
                {
                    LogClientToServerCommunication(signalsSentFromClient, signalsSeenFromServer);
                }
                await Task.Delay(1000);
            }
            if (!cancel())
            {
                var a = new FloatDataPackage();
                a.data = 13.331f;
                a.id = 0;
                a.timeStamp = DateTime.UtcNow;
                signalsSentFromClient[0].data = a;
                await Task.Delay(1000);
            }

            if (!cancel())
            {
                LogClientToServerCommunication(signalsSentFromClient, signalsSeenFromServer);
                await Task.Delay(1000);
            }



            if (!cancel())
            {
                //if(shouldReport())
                {
                    LogServerToClientCommunication(signalsSentFromServer, signalsSeenFromClient);
                }
                await Task.Delay(1000);
            }
            if (!cancel())
            {
                var a = new FloatDataPackage();
                a.data = 13.331f;
                a.id = 0;
                a.timeStamp = DateTime.UtcNow;
                signalsSentFromServer[0].data = a;
                await Task.Delay(1000);
            }

            if (!cancel())
            {
                //if(shouldReport())
                LogServerToClientCommunication(signalsSentFromServer, signalsSeenFromClient);
                await Task.Delay(1000);
            }

        }


        public async static void TestIncomingOnServer(
        Func<bool> cancel,
        Func<bool> shouldReport,
        OutgoingSignal[] signalsSentFromServer,
        IncomingSignal[] signalsSeenFromClient,
        OutgoingSignal[] signalsSentFromClient,
        IncomingSignal[] signalsSeenFromServer,
        Connection[] clientConnectionsSeenFromServer,
        ConnectionData[] clientConnectionDatasSeenFromServer
        )
        {

            Connection server = new Connection();
            ConnectionData serverData = new ConnectionData();
            ConnectionMapping mapping = new ConnectionMapping();

            Connection client0 = new Connection();
            ConnectionData clientData0 = new ConnectionData();

            serverData.listenPort = 3000;
            serverData.serverIp = "127.0.0.1";

            clientData0.listenPort = 3001;
            clientData0.serverIp = "127.0.0.1";
            clientData0.sendToPort = 3000;




            StartServer(ref server, ref serverData, cancel, ref mapping, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer,
                signalsSentFromServer, signalsSeenFromServer);

            StartClient(ref client0, ref clientData0, ref serverData, cancel, signalsSeenFromClient, signalsSentFromClient);

            //Server and Client are not listening/sending to the right endpoint?!
            //server listens to Ip.any, 3000 , initialized with UdpClient(...), whereas client doesnt listen, is init with UdpClient() and then .Connect(...) . what exactly is the differnece?

            if (!cancel())
            {
                //if(shouldReport())
                {
                    LogClientToServerCommunication(signalsSentFromClient, signalsSeenFromServer);
                }
                await Task.Delay(1000);
            }
            if (!cancel())
            {
                var a = new FloatDataPackage();
                a.data = 13.331f;
                a.id = 0;
                a.timeStamp = DateTime.UtcNow;
                signalsSentFromClient[0].data = a;
                await Task.Delay(1000);
            }
         
            if (!cancel())
            {
                LogClientToServerCommunication(signalsSentFromClient, signalsSeenFromServer);
                await Task.Delay(1000);
            }

        }


        public async static void TestIncomingOnClient(
        Func<bool> cancel,
        Func<bool> shouldReport,
        OutgoingSignal[] signalsSentFromServer,
        IncomingSignal[] signalsSeenFromClient0,
        OutgoingSignal[] signalsSentFromClient,
        IncomingSignal[] signalsSeenFromServer,
        Connection[] clientConnectionsSeenFromServer,
        ConnectionData[] clientConnectionDatasSeenFromServer
        )
        {
            Connection server = new Connection();
            ConnectionData serverData = new ConnectionData();
            ConnectionMapping mapping = new ConnectionMapping();

            Connection client0 = new Connection();
            ConnectionData clientData0 = new ConnectionData();

            serverData.listenPort = 3000;
            serverData.serverIp = "127.0.0.1";

            clientData0.listenPort = 3001;
            clientData0.serverIp = "127.0.0.1";
            clientData0.sendToPort = 3000;

            
            

            StartServer(ref server, ref serverData, cancel, ref mapping, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer,
                signalsSentFromServer, signalsSeenFromServer);

            StartClient(ref client0, ref clientData0, ref serverData, cancel, signalsSeenFromClient0, signalsSentFromClient);

            //Server and Client are not listening/sending to the right endpoint?!
            //server listens to Ip.any, 3000 , initialized with UdpClient(...), whereas client doesnt listen, is init with UdpClient() and then .Connect(...) . what exactly is the differnece?

            if (!cancel())
            {
                //if(shouldReport())
                {
                    LogServerToClientCommunication(signalsSentFromServer, signalsSeenFromClient0);
                }
                await Task.Delay(1000);
            }
            if (!cancel())
            {
                var a = new FloatDataPackage();
                a.data = 13.331f;
                a.id = 0;
                a.timeStamp = DateTime.UtcNow;
                signalsSentFromServer[0].data = a;
                await Task.Delay(1000);
            }
            
            if (!cancel())
            {
                //if(shouldReport())
                LogServerToClientCommunication(signalsSentFromServer, signalsSeenFromClient0);
                await Task.Delay(1000);
            }
        }



        private static void LogClientToServerCommunication(OutgoingSignal[] signalsSentFromClient, IncomingSignal[] signalsSeenFromServer)
        {
            Console.WriteLine("INCOMING SERVER DATA");
            foreach (var incoming in signalsSeenFromServer)
            {
                Console.Write(incoming.ToString() + "   |   ");
            }

            Console.WriteLine();

            Console.WriteLine("OUTGOING CLIENT DATA");
            foreach (var outgoing in signalsSentFromClient)
            {
                Console.Write(outgoing.ToString() + "   |   ");
            }
            Console.WriteLine();
        }

        private static void LogServerToClientCommunication(OutgoingSignal[] signalsSentFromServer, IncomingSignal[] signalsSeenFromClient0)
        {
            Console.WriteLine("OUTGOING SERVER DATA");
            foreach (var outgoing in signalsSentFromServer)
            {
                Console.Write(outgoing.ToString() + "   |   ");
            }
            Console.WriteLine();

            Console.WriteLine("INCOMING CLIENT DATA");
            foreach (var incoming in signalsSeenFromClient0)
            {
                Console.Write(incoming.ToString() + "   |   ");
            }
            Console.WriteLine();
        }
    }
}
