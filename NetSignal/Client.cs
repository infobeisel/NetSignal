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

    public class Util
    {
        public static void FlushBytes(byte[] bytes)
        {
            for (var byteI = 0; byteI < bytes.Length; byteI++) bytes[byteI] = 0;
        }
    }

    public class Logging
    {
        private static object lockObject = new object();
        public static void Write(string msg)
        {
            lock(lockObject)
            {
                Console.WriteLine(msg);
            }
        }
        public static void Write(Exception e)
        {
            lock (lockObject)
            {
                Console.WriteLine(e);
            }
        }
    }

    public enum StateOfConnection
    {
        Uninitialized = 0,
        ReadyToOperate = 1, //initialized and waiting to be picked up by a task
        BeingOperated = 2, //a task is already caring for me
    }

    //state of a connection, changes during lifetime of a connection
    public class ConnectionState
    {
        public StateOfConnection tcpStateName;
        public DateTime tcpKeepAlive;//maybe, to keep the tcp connection open.

        public StateOfConnection udpStateName;
    }

    //data necessary to run a connection, does (should!) not change during lifetime
    public struct ConnectionMetaData
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

    //objects that contain the actual transport implementation, provided by .net
    public struct ConnectionAPIs
    {
        public UdpClient udpClient;
        public TcpClient tcpClient;
        public NetworkStream tcpStream;
        public TcpListener tcpListener;
        public object udpClientLock;
        public object tcpClientLock;
    }

    public struct ConnectionMapping
    {
        //maps ip endpoints to index that identifies a client over the session.
        public Dictionary<IPEndPoint, int> EndPointToClientIdentification;

        public Dictionary<int, IPEndPoint> ClientIdentificationToEndpoint;
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
        private FloatDataPackage dataMember;
        public FloatDataPackage data
        {
            internal set
            {
                if (!dataMember.data.Equals(value.data))
                {
                    dataHasBeenUpdated = true;
                }
                dataMember = value;
            }
            get
            {
                return dataMember;
            }
        }

        public bool dataHasBeenUpdated;

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

    


    //contains the NetSignal protocol rules for identifying the right type of an incoming udp or tcp message
    public class MessageDeMultiplexer
    {
        public async static Task Divide(byte [] message, Func<Task> handleFloatSignal, Func<Task> handleTCPConnectionRequest, Func<Task> handleTCPAliveSignal)
        {
            switch (message[0])
            {
                case 0: //normal float signal
                    await handleFloatSignal();
                    break;
                case 1: //tcp connection request
                    await handleTCPConnectionRequest();
                    break;
                case 2: //tcp alive signal
                    await handleTCPAliveSignal();
                    break;
                default:
                    Logging.Write("found invalid message type: " + message);
                    break;
            }
        }


        public async static Task MarkFloatSignal(byte[] message, Func<Task> handleFloatSignal)
        {
            message[0] = 0;
            await handleFloatSignal();
        }

        public async static Task MarkTCPConnectionRequest(byte[] message, Func<Task> handleRequest)
        {
            message[0] = 1;
            await handleRequest();
        }

        public async static Task MarkTCPKeepAlive(byte[] message, Func<Task> handleRequest)
        {
            message[0] = 2;
            await handleRequest();
        }

    }

    public class ConnectionUpdater
    {
        private const int connectionInitialsBytesCount = 64;

        [ThreadStaticAttribute]
        private static byte[] connectionInitialsBytesServerRead_ = null;
        private static byte[] connectionInitialsBytesServerRead
        {
            get
            {
                if (connectionInitialsBytesServerRead_ == null)
                    connectionInitialsBytesServerRead_ = new byte[connectionInitialsBytesCount];
                return connectionInitialsBytesServerRead_;
            }
        }

        [ThreadStaticAttribute]
        private static byte[] connectionInitialsBytesClientRead_ = null;
        private static byte[] connectionInitialsBytesClientRead
        {
            get
            {
                if (connectionInitialsBytesClientRead_ == null)
                    connectionInitialsBytesClientRead_ = new byte[connectionInitialsBytesCount];
                return connectionInitialsBytesClientRead_;
            }
        }

        [ThreadStaticAttribute]
        private static byte[] connectionInitialsBytesServerWrite_ = null;
        private static byte[] connectionInitialsBytesServerWrite
        {
            get
            {
                if (connectionInitialsBytesServerWrite_ == null)
                    connectionInitialsBytesServerWrite_ = new byte[connectionInitialsBytesCount];
                return connectionInitialsBytesServerWrite_;
            }
        }

        [ThreadStaticAttribute]
        private static byte[] connectionInitialsBytesClientWrite_ = null;
        private static byte[] connectionInitialsBytesClientWrite
        {
            get
            {
                if (connectionInitialsBytesClientWrite_ == null)
                    connectionInitialsBytesClientWrite_ = new byte[connectionInitialsBytesCount];
                return connectionInitialsBytesClientWrite_;
            }
        }


        public static void InitializeMultiConnection(ref ConnectionAPIs connectors, ref ConnectionMetaData connectionData, ConnectionState connectionState)
        {
            connectors = new ConnectionAPIs();

            connectionData.thisListensTo = new IPEndPoint(IPAddress.Any, connectionData.listenPort);
            //connectionData.listenToEndpoint = new IPEndPoint(IPAddress.Parse(connectionData.serverIp), connectionData.listenPort);
            connectors.udpClient = new UdpClient(connectionData.thisListensTo);
            connectionState.udpStateName = StateOfConnection.ReadyToOperate;
            //connectors.udpClient = new UdpClient();
            //connectors.udpClient.Connect(connectionData.listenToEndpoint);//make the udp client react only to incoming messages that come from the given port
            Logging.Write("server: udpclient local: " + (IPEndPoint)connectors.udpClient.Client.LocalEndPoint);
            //            Logging.Write("server: udpclient remote: " + (IPEndPoint)connectors.udpClient.Client.RemoteEndPoint);

            connectors.tcpListener = new TcpListener(IPAddress.Any, connectionData.listenPort);
            connectors.tcpClient = null;
        }


        public async static Task<ConnectionAPIs> SetupClientTCP(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState)
        {
            connectionState.tcpStateName = StateOfConnection.Uninitialized;
            connection.tcpClient = new TcpClient();


            Logging.Write("client connects to tcp" + connectionData.serverIp + ":" + connectionData.sendToPort + " " + connectionData.toSendToServer);
            await connection.tcpClient.ConnectAsync(connectionData.toSendToServer.Address, connectionData.toSendToServer.Port);

            Logging.Write("client connected");
            NetworkStream stream = connection.tcpClient.GetStream();
            connection.tcpStream = stream;
            connectionState.tcpStateName = StateOfConnection.ReadyToOperate;
            connectionState.tcpKeepAlive = DateTime.UtcNow;
            return connection;
        }


        public async static void AwaitAndPerformTearDownTCPListener(ConnectionAPIs connection, Func<bool> shouldTearDown, ConnectionState currentState, ConnectionAPIs [] clientCons, ConnectionState[] clientStates, ConnectionMetaData [] clientDatas, ConnectionMapping connectionmapping)
        {
            while (!shouldTearDown())
            {
                await Task.Delay(1000);
            }
            /*Logging.Write("going to clean up tcp client");
            while(currentState.tcpStateName != StateOfConnection.ReadyToOperate)
            {
                await Task.Delay(1000);
            }*/
            Logging.Write("clean up tcp listener and clients");
            currentState.tcpStateName = StateOfConnection.Uninitialized;
            connection.tcpStream?.Close();
            connection.tcpListener?.Stop();

            
            for (int conI = 0; conI < clientCons.Length; conI++)
            {
                clientCons[conI].tcpStream?.Close();
                clientCons[conI].tcpStream = null;

                clientCons[conI].tcpClient?.Close();
                clientCons[conI].tcpClient = null;

                clientCons[conI].tcpListener = null;

                clientDatas[conI].clientID = -1;
                clientDatas[conI].thisListensTo = null;

                clientStates[conI].tcpStateName = StateOfConnection.Uninitialized;
            }
            connectionmapping.ClientIdentificationToEndpoint.Clear();
            connectionmapping.EndPointToClientIdentification.Clear();

        }

        public async static void AwaitAndPerformTearDownClientTCP(ConnectionAPIs connection, Func<bool> shouldTearDown, ConnectionState currentState)
        {
            while (!shouldTearDown())
            {
                await Task.Delay(1000);
            }
            /*Logging.Write("going to clean up tcp client");
            while(currentState.tcpStateName != StateOfConnection.ReadyToOperate)
            {
                await Task.Delay(1000);
            }*/
            Logging.Write("clean up tcp client");
            currentState.tcpStateName = StateOfConnection.Uninitialized;
            connection.tcpStream.Close();
            connection.tcpClient.Close();

        }



        public async static void AwaitAndPerformTearDownClientUDP(ConnectionAPIs connection, Func<bool> shouldTearDown, ConnectionState currentState)
        {
            
            while (!shouldTearDown())
            {
                await Task.Delay(1000);
            }
            /*while (currentState.udpStateName != StateOfConnection.ReadyToOperate)
            {
                await Task.Delay(1000);
            }*/
            Logging.Write("clean up udp client");
            currentState.udpStateName= StateOfConnection.Uninitialized;
            connection.udpClient.Close();
        }

        public async static Task<Tuple<ConnectionAPIs, ConnectionMetaData>> InitializeSingleConnection(ConnectionAPIs connectors, ConnectionMetaData connectionData, ConnectionState connectionState)
        {
            connectors = new ConnectionAPIs();
            try
            {
                //connectionData.listenToEndpoint = new IPEndPoint(IPAddress.Parse(connectionData.serverIp), connectionData.listenPort);
                connectionData.thisListensTo = new IPEndPoint(IPAddress.Any, connectionData.listenPort);

                //connectionData.toSendToThis = new IPEndPoint(IPAddress.Parse(connectionData.serverIp), connectionData.sendToPort);
                connectionData.toSendToServer = new IPEndPoint(IPAddress.Parse(connectionData.serverIp), connectionData.sendToPort);

                connectors.udpClient = new UdpClient(connectionData.thisListensTo);
                connectionState.udpStateName = StateOfConnection.ReadyToOperate;
                Logging.Write("client connects to udp" + connectionData.listenPort);
                //connectors.udpClient = new UdpClient(connectionData.listenToEndpoint);
                //connectors.udpClient.Connect(connectionData.listenToEndpoint);//make the udp client react only to incoming messages that come from the given port

                connectors.tcpListener = null;

                connectors = await SetupClientTCP(connectors, connectionData, connectionState);

                connectionData = await ExchangeConnectionInitials(connectors, connectionData);

            }
            catch (SocketException e)
            {
                Logging.Write(e.Message);
            }
            return new Tuple<ConnectionAPIs, ConnectionMetaData>(connectors, connectionData);
        }

        private static async Task<ConnectionMetaData> ExchangeConnectionInitials(ConnectionAPIs connectors, ConnectionMetaData connectionData)
        {
            //byte[] data = Encoding.ASCII.GetBytes(connectionData.listenPort.ToString());//only send the port we are listening to over udp
            Util.FlushBytes(connectionInitialsBytesClientWrite);
            await MessageDeMultiplexer.MarkTCPConnectionRequest(connectionInitialsBytesClientWrite,
                async () =>
                {
                    var portString = connectionData.listenPort.ToString();
                    
                    Encoding.ASCII.GetBytes(portString, 0, portString.Length, connectionInitialsBytesClientWrite, 1);

                    Logging.Write("client write my port");
                    await connectors.tcpStream.WriteAsync(connectionInitialsBytesClientWrite, 0, connectionInitialsBytesClientWrite.Length);
                    Logging.Write("client written");

                    string response = null;
                    Util.FlushBytes(connectionInitialsBytesClientRead);
                    var byteCount = await connectors.tcpStream.ReadAsync(connectionInitialsBytesClientRead, 0, connectionInitialsBytesClientRead.Length);

                    await MessageDeMultiplexer.Divide(connectionInitialsBytesClientRead, async () => { Logging.Write("handle float!? unexpected reply to client's tcp connection request"); },
                        async () =>
                        {
                            Logging.Write("client read client id from "+ Encoding.ASCII.GetString(connectionInitialsBytesClientRead, 0, byteCount ));
                            response = Encoding.ASCII.GetString(connectionInitialsBytesClientRead, 1, byteCount - 1);
                            var myClientID = int.Parse(response);
                            connectionData.clientID = myClientID;
                            Logging.Write("i am client " + myClientID);
                        },
                        async () => { Logging.Write("handle tcp keepalive!? unexpected reply to client's tcp connection request"); });
                });
            return connectionData;
        }


        public static async void StartProcessTCPConnections(ConnectionMapping connectionMapping, ConnectionAPIs by, ConnectionAPIs[] storeToConnections, ConnectionMetaData[] storeToConnectionDatas,ConnectionState[] storeToConnectionStates,  Func<bool> cancel, Action report)
        {
            //init to null
            for (int conI = 0; conI < storeToConnections.Length; conI++)
            {
                storeToConnections[conI].tcpClient = null;
                storeToConnections[conI].tcpListener = null;
                storeToConnections[conI].tcpStream = null;
                storeToConnections[conI].udpClient = null;

                storeToConnectionDatas[conI].clientID = -1;
                storeToConnectionDatas[conI].thisListensTo = null;

                storeToConnectionStates[conI] = new ConnectionState();
                storeToConnectionStates[conI].udpStateName = StateOfConnection.Uninitialized;
                storeToConnectionStates[conI].tcpStateName = StateOfConnection.Uninitialized;
                storeToConnectionStates[conI].tcpKeepAlive = new DateTime(0);

                
            }

            try
            {
                by.tcpListener.Start();
                string data = null;
                while (!cancel())
                {
                    //waiting for connection
                    Logging.Write("AcceptTCPConnections: wait for tcp client");
                    TcpClient connection = null;
                    try
                    {
                        connection = await by.tcpListener.AcceptTcpClientAsync();
                    } catch (Exception e)
                    {
                        Logging.Write("StartProcessTCPConnections: tcp listener socket got closed, (unfortunately) this is intended behaviour, stop listening.");
                        continue;
                    }
                    
                    data = null;
                    NetworkStream stream = connection.GetStream();

                    //so far is an anonymous client, need to identify!

                    Logging.Write("AcceptTCPConnections: read one chunk stream");
                    int readByteCount = 0;
                    try
                    {
                        Util.FlushBytes(connectionInitialsBytesServerRead);
                        readByteCount = await stream.ReadAsync(connectionInitialsBytesServerRead, 0, connectionInitialsBytesServerRead.Length);
                    } catch (Exception e)
                    {
                        Logging.Write("StartProcessTCPConnections: tcp listener stream got closed, (unfortunately) this is intended behaviour, stop reading.");
                        continue;
                    }

                    if ( readByteCount != 0)
                    {
                        await MessageDeMultiplexer.Divide(connectionInitialsBytesServerRead,
                            async () => { Logging.Write("tcp float signals not yet implemented and should NOT occur here!?"); },
                            async () => {
                                //TODO here we need user defined filtering to deserialize into actual IncomingSignals!
                                //expect ud endpoint
                                data = Encoding.ASCII.GetString(connectionInitialsBytesServerRead, 1, readByteCount-1);

                                //incoming connection, try to identify
                                IPEndPoint clientEndpoint;
                                int clientID;
                                IdentifyClient(data, connectionMapping, storeToConnections, connection, out clientEndpoint, out clientID);

                                if (clientID == -1)
                                {
                                    //sth went wrong
                                    connection.Close(); // TODO WHAT TO DO HERE?
                                    
                                } else
                                {
                                    //remember the client
                                    storeToConnections[clientID].tcpClient = connection;
                                    storeToConnections[clientID].tcpStream = stream;
                                    storeToConnectionDatas[clientID].thisListensTo = clientEndpoint;
                                    storeToConnectionDatas[clientID].clientID = clientID;

                                    storeToConnectionStates[clientID].tcpKeepAlive = DateTime.UtcNow;
                                    storeToConnectionStates[clientID].tcpStateName = StateOfConnection.ReadyToOperate;

                                    Logging.Write("tcp received: " + data + " , will send back id " + clientID);

                                    Util.FlushBytes(connectionInitialsBytesServerWrite);
                                    await MessageDeMultiplexer.MarkTCPConnectionRequest(connectionInitialsBytesServerWrite, async () =>
                                    {
                                        var clientIdStr = clientID.ToString();
                                        
                                        Encoding.ASCII.GetBytes(clientIdStr, 0, clientIdStr.Length, connectionInitialsBytesServerWrite, 1);
                                        try
                                        {
                                            await stream.WriteAsync(connectionInitialsBytesServerWrite, 0, connectionInitialsBytesServerWrite.Length);
                                        }
                                        catch (Exception e)
                                        {
                                            Logging.Write("StartProcessTCPConnections: tcp listener stream got closed, (unfortunately) this is intended behaviour, stop writing.");
                                        }

                                    });
                                    
                                }
                            },
                            async  () => { Logging.Write("tcp keepalive receive not yet implemented and should NOT occur here!?"); }
                            );
                    }

                    
                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }
            finally
            {
                
            }
        }

        

        private static void IdentifyClient(string fromTCPMessage, ConnectionMapping connectionMapping, ConnectionAPIs[] storeToConnections, TcpClient connection, out IPEndPoint clientEndpoint, out int clientID)
        {
            //clientEndpoint = (IPEndPoint)connection.Client.RemoteEndPoint;
            //var splitIPAndPort = fromTCPMessage.Split('|');
            var connnectedToIPAddress = ((IPEndPoint)connection.Client.RemoteEndPoint).Address;
            var dataContainingListenPort = int.Parse(fromTCPMessage);
            //clientEndpoint = new IPEndPoint(IPAddress.Parse(splitIPAndPort[0]), int.Parse(splitIPAndPort[1]));
            clientEndpoint = new IPEndPoint(connnectedToIPAddress, dataContainingListenPort);

            Logging.Write("try to identify client with endpoint " + clientEndpoint);
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
        private const int signalBytesCount = 256;

        [ThreadStaticAttribute]
        private static byte[] signalBytesServerRead_ = null;
        public static byte[] signalBytesServerRead
        {
            get
            {
                if (signalBytesServerRead_ == null)
                    signalBytesServerRead_ = new byte[signalBytesCount];
                return signalBytesServerRead_;
            }
        }
        private static byte[] signalBytesServerWrite_ = null;
        public static byte[] signalBytesServerWrite
        {
            get
            {
                if (signalBytesServerWrite_ == null)
                    signalBytesServerWrite_ = new byte[signalBytesCount];
                return signalBytesServerWrite_;
            }
        }

        private static byte[] signalBytesClientWrite_= null;
        public static byte[] signalBytesClientWrite
        {
            get
            {
                if (signalBytesClientWrite_ == null)
                    signalBytesClientWrite_ = new byte[signalBytesCount];
                return signalBytesClientWrite_;
            }
        }

        private static byte[] signalBytesClientRead_ = null;
        public static byte[] signalBytesClientRead
        {
            get
            {
                if (signalBytesClientRead_ == null)
                    signalBytesClientRead_ = new byte[signalBytesCount];
                return signalBytesClientRead_;
            }
        }
        //uses tcp to sync signals reliably
        public async static void SyncSignalsToAllReliably(ConnectionAPIs with, OutgoingSignal [] signals, Func<bool> cancel, params ConnectionAPIs [] toAllCons)
        {

        }
        //uses udp to sync signals unreliably
        public async static void SyncSignalsToAll(ConnectionAPIs with,  OutgoingSignal[] signals, byte[] usingBytes, Func<bool> cancel, params ConnectionMetaData[] all)
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
                                Logging.Write("data is dirty. send it to " + to.thisListensTo);
                                //byte[] toSend = Encoding.ASCII.GetBytes(SignalCompressor.Compress(signals[signalI].data));
                                var dataStr = SignalCompressor.Compress(signals[signalI].data);
                                Util.FlushBytes(usingBytes);
                                await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () => {
                                    
                                    Encoding.ASCII.GetBytes(dataStr, 0, dataStr.Length, usingBytes, 1);
                                    try {
                                        await with.udpClient.SendAsync(usingBytes, usingBytes.Length, toSendTo);
                                    }
                                    catch (SocketException e)
                                    {
                                        Logging.Write("SyncSignalsToAll: udp client socket got closed, (unfortunately) this is intended behaviour, stop sending.");
                                    }
                                    signals[signalI].dataDirty = false;
                                });
                                
                            }
                        }
                        await Task.Delay(1);
                    }
                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }
            finally
            {
            }
        }


        public async static void ReceiveSignals(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState, IncomingSignal[] signals, byte[] usingBytes, Func<bool> cancel, Action<string> report)
        {
            try
            {
                connectionState.udpStateName = StateOfConnection.BeingOperated;
                IPEndPoint receiveFrom = new IPEndPoint(connectionData.thisListensTo.Address, connectionData.thisListensTo.Port);
                while (!cancel())
                {
                    //Logging.Write("I (" + connectionData.listenToEndpoint + ") connect to " + receiveFrom);
                    //connection.udpClient.Connect(receiveFrom);
                    //var bytes = connection.udpClient.Receive(ref receiveFrom);


                    //dont know a better way: receive async does not accept cancellation tokens, so need to let it fail here (because some other disposed the udpclient)
                    UdpReceiveResult receiveResult;
                    try
                    {
                        receiveResult = await connection.udpClient.ReceiveAsync();
                    } catch (ObjectDisposedException e)
                    {
                        Logging.Write("ReceiveSignals: udp socket has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                        continue;
                    }
                    
                    //var receivePending = connection.udpClient.ReceiveAsync();
                    

                    receiveFrom = receiveResult.RemoteEndPoint;
                    var bytes = receiveResult.Buffer;
                    

                    await MessageDeMultiplexer.Divide(bytes, async () => {
                        Logging.Write("I (" + connectionData.thisListensTo + ") received sth from (?)" + receiveFrom);
                        
                        Logging.Write("receive from (?)" + connectionData.thisListensTo);
                        Logging.Write("parse " + bytes.ToString() + " # " + bytes.Length + " from " + receiveFrom);
                        var parsedString = Encoding.ASCII.GetString(bytes,1, bytes.Length-1);
                        Logging.Write("report " + parsedString);
                        report(parsedString);
                        var package = SignalCompressor.Decompress(parsedString);
                        signals[package.id].data = package;
                    },
                        async () => { Logging.Write("ReceiveSignals: unexpected package connection request!?"); },
                        async () => { Logging.Write("ReceiveSignals: unexpected package tcp keepalive!?"); });

                    
                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }
            finally
            {
                //connectionState.udpStateName = StateOfConnection.ReadyToOperate;
            }
        }
    }

    public class NetSignalStarter
    {
        public async static Task<Tuple<ConnectionAPIs,ConnectionMetaData,ConnectionMapping>> StartServer(ConnectionAPIs serverConnection, ConnectionMetaData serverData, ConnectionState serverState, Func<bool> cancel, ConnectionMapping connectionMapping, ConnectionAPIs[] connections, ConnectionMetaData[] connectionDatas, ConnectionState[] connectionStates, OutgoingSignal[] outgoingSignals, IncomingSignal[] incomingSignals)
        {
            //serverData.listenPort = 3000;
            //serverData.serverIp = null;
            Logging.Write("StartServer: init multi connection");
            ConnectionUpdater.InitializeMultiConnection(ref serverConnection, ref serverData, serverState);

            connectionMapping.ClientIdentificationToEndpoint = new Dictionary<int, IPEndPoint>();
            connectionMapping.EndPointToClientIdentification = new Dictionary<IPEndPoint, int>();

            Logging.Write(serverConnection.udpClient.ToString());

            Logging.Write("StartServer: start receive signals");
            //SignalUpdater.StartThreadReceiveSignals(serverConnection, serverData, incomingSignals, cancel, (string s) => Logging.Write(s));
            SignalUpdater.ReceiveSignals(serverConnection, serverData, serverState, incomingSignals,SignalUpdater.signalBytesServerRead, cancel, (string s) => Logging.Write(s));

            Logging.Write("StartServer: start accept tcp connections");
            //ConnectionUpdater.StartThreadAcceptTCPConnections(connectionMapping, serverConnection, connections, connectionDatas, cancel, () => { });
            ConnectionUpdater.StartProcessTCPConnections(connectionMapping, serverConnection, connections, connectionDatas, connectionStates, cancel, () => { });

            Logging.Write("StartServer: start sync signals");
            //SignalUpdater.StartThreadSyncSignalsToAll(serverConnection, outgoingSignals, cancel, connectionDatas);
            SignalUpdater.SyncSignalsToAll(serverConnection, outgoingSignals, SignalUpdater.signalBytesServerWrite, cancel, connectionDatas);

            ConnectionUpdater.AwaitAndPerformTearDownClientUDP(serverConnection, cancel, serverState);
            ConnectionUpdater.AwaitAndPerformTearDownTCPListener(serverConnection, cancel, serverState, connections, connectionStates, connectionDatas, connectionMapping);

            return new Tuple<ConnectionAPIs, ConnectionMetaData, ConnectionMapping>(serverConnection, serverData, connectionMapping);
        }

        public async static Task<Tuple<ConnectionAPIs,ConnectionMetaData>> StartClient(ConnectionAPIs clientCon, ConnectionMetaData clientData, ConnectionState clientState, ConnectionMetaData toServer, Func<bool> cancel, IncomingSignal[] incomingSignals, OutgoingSignal[] outgoingSignals)
        {
            /*clientData.listenPort = 3001;
            clientData.sendToPort = 3000;
            clientData.serverIp = "127.0.0.1";*/
            Logging.Write("StartClient: init single connection");
            var returnTuple = await ConnectionUpdater.InitializeSingleConnection(clientCon, clientData, clientState);
            clientCon = returnTuple.Item1;
            clientData = returnTuple.Item2;

            Logging.Write("StartClient: start receive signals");
            //SignalUpdater.StartThreadReceiveSignals(clientCon, clientData, incomingSignals, cancel, (string s) => Logging.Write(s));
            SignalUpdater.ReceiveSignals(clientCon, clientData,clientState, incomingSignals, SignalUpdater.signalBytesClientRead, cancel, (string s) => Logging.Write(s));

            Logging.Write("StartClient: start sync signals to server");
            //SignalUpdater.StartThreadSyncSignalsToAll(clientCon, outgoingSignals, cancel, toServer);
            SignalUpdater.SyncSignalsToAll(clientCon, outgoingSignals, SignalUpdater.signalBytesClientWrite, cancel, toServer);


            ConnectionUpdater.AwaitAndPerformTearDownClientTCP(clientCon, cancel, clientState);
            ConnectionUpdater.AwaitAndPerformTearDownClientUDP(clientCon, cancel, clientState);

            //var datagram = Encoding.ASCII.GetBytes("hellosent");
            //clientCon.udpClient.Send(datagram, datagram.Length);

            return new Tuple<ConnectionAPIs, ConnectionMetaData>(clientCon, clientData);
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
            ConnectionMetaData[] connectionMetaDatasSeenFromServer = new ConnectionMetaData[1];
            ConnectionAPIs[] connectionApisSeenFromServer = new ConnectionAPIs[1];
            ConnectionState[] connectionStatesSeenFromServer = new ConnectionState[1];
            //NetSignalStarter.TestIncomingOnClient(() => cancel, () => shouldPrint, signalSentFromServer, signalSeenFromClient, signalsSentFromClient, signalsSeenFromServer, conFromServer, consFromServer);
            //await Task.Delay(5000);
            await NetSignalStarter.TestDuplex(() => cancel, () => shouldPrint, signalSentFromServer, signalSeenFromClient, signalsSentFromClient, signalsSeenFromServer, connectionApisSeenFromServer, connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer);

            cancel = true;
            //TODOS:
            //move from synchronous to ASYNC send and receive. (CHECK)
            //do not always open and close new tcp connection? 
            // - close tcp listener and clients on server side (check)
            // - close tcp client on client side (check)
            // - also close udp on client and server side if necessary (check)
            //implement sync and receive signals RELIABLE version (over tcp)
            //refactor into separate files
            //implement websocket for matchmaking (to find ip to connect to server), set up with strato (?) 
            //import to unity
            //battletest: make scriptable objects that have Incoming- and Outgoing Signals, write Mono Updaters that assign Signals to specific game objects (mainly: Bird slots, state enums for UI)
        }



        public async static Task TestDuplex(
        Func<bool> cancel,
        Func<bool> shouldReport,
        OutgoingSignal[] signalsSentFromServer,
        IncomingSignal[] signalsSeenFromClient,
        OutgoingSignal[] signalsSentFromClient,
        IncomingSignal[] signalsSeenFromServer,
        ConnectionAPIs[] clientConnectionsSeenFromServer,
        ConnectionMetaData[] clientConnectionDatasSeenFromServer,
        ConnectionState[] clientConnectionStatesSeenFromServer
        )
        {

            ConnectionAPIs server = new ConnectionAPIs();
            ConnectionMetaData serverData = new ConnectionMetaData();
            ConnectionState  serverState = new ConnectionState();
            ConnectionMapping mapping = new ConnectionMapping();

            ConnectionAPIs client0 = new ConnectionAPIs();
            ConnectionMetaData clientData0 = new ConnectionMetaData();
            ConnectionState clientState = new ConnectionState();

            serverData.listenPort = 3000;
            serverData.serverIp = "127.0.0.1";

            clientData0.listenPort = 3001;
            clientData0.serverIp = "127.0.0.1";
            clientData0.sendToPort = 3000;



            Logging.Write("TestDuplex: start server");
            var updatedServerTuple = await StartServer(server, serverData, serverState, cancel,mapping, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer, clientConnectionStatesSeenFromServer, 
                signalsSentFromServer, signalsSeenFromServer);
            server = updatedServerTuple.Item1;
            serverData = updatedServerTuple.Item2;
            mapping = updatedServerTuple.Item3;

            await Task.Delay(1000);
            Logging.Write("TestDuplex: start client");
            var updatedTuple = await StartClient(client0, clientData0, clientState, serverData, cancel, signalsSeenFromClient, signalsSentFromClient);
            client0 = updatedTuple.Item1;
            clientData0 = updatedTuple.Item2;

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


        private static void LogClientToServerCommunication(OutgoingSignal[] signalsSentFromClient, IncomingSignal[] signalsSeenFromServer)
        {
            Logging.Write("INCOMING SERVER DATA");
            foreach (var incoming in signalsSeenFromServer)
            {
                Console.Write(incoming.ToString() + "   |   ");
            }

            Logging.Write("");

            Logging.Write("OUTGOING CLIENT DATA");
            foreach (var outgoing in signalsSentFromClient)
            {
                Console.Write(outgoing.ToString() + "   |   ");
            }
            Logging.Write("");
        }

        private static void LogServerToClientCommunication(OutgoingSignal[] signalsSentFromServer, IncomingSignal[] signalsSeenFromClient0)
        {
            Logging.Write("OUTGOING SERVER DATA");
            foreach (var outgoing in signalsSentFromServer)
            {
                Console.Write(outgoing.ToString() + "   |   ");
            }
            Logging.Write("");

            Logging.Write("INCOMING CLIENT DATA");
            foreach (var incoming in signalsSeenFromClient0)
            {
                Console.Write(incoming.ToString() + "   |   ");
            }
            Logging.Write("");
        }
    }
}
