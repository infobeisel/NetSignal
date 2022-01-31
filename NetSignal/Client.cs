using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Runtime.InteropServices;
using System.Net.Http;
using System.Xml.Serialization;
using System.Data.SQLite;
using System.Data;

namespace NetSignal
{

    public class Util
    {
        public static void FlushBytes(byte[] bytes)
        {
            for (var byteI = 0; byteI < bytes.Length; byteI++) bytes[byteI] = 0;
        }

        public static StateOfConnection CompareExchange(ref int loc, StateOfConnection val, StateOfConnection comp)
        {
            return (StateOfConnection) System.Threading.Interlocked.CompareExchange(ref loc, (int)val, (int)comp);
        }

        public static StateOfConnection Exchange(ref int loc, StateOfConnection val)
        {
            return (StateOfConnection)System.Threading.Interlocked.Exchange(ref loc, (int)val);
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
        private const int byteCount = 256;

        public int tcpWriteStateName;
        public int tcpReadStateName;
        public DateTime tcpKeepAlive;//maybe, to keep the tcp connection open.

        public int udpStateName;

        public byte [] tcpWriteBytes;
        public byte [] udpWriteBytes;
        public byte [] tcpReadBytes;
        public byte [] udpReadBytes;

        public ConnectionState()
        {
            udpStateName = (int)StateOfConnection.Uninitialized;
            tcpWriteStateName = (int)StateOfConnection.Uninitialized;
            tcpReadStateName = (int)StateOfConnection.Uninitialized;
            tcpKeepAlive = new DateTime(0);

            tcpWriteBytes = new byte[byteCount];
            udpWriteBytes = new byte[byteCount];
            tcpReadBytes = new byte[byteCount];
            udpReadBytes = new byte[byteCount];
        }
    }

    //data necessary to run a connection, does (should!) not change during lifetime
    public struct ConnectionMetaData
    {

        public string matchmakingServerIp;
        public int matchmakingServerPort;

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

        public HttpListener httpListener;
        public  HttpClient httpClient;
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
        
        public static void InitializeMultiConnection(ref ConnectionAPIs connectors, ref ConnectionMetaData connectionData, ConnectionState connectionState, ConnectionAPIs [] connections, ConnectionMetaData [] connectionDatas, ConnectionState [] toConnections)
        {
            connectors = new ConnectionAPIs();

            connectionData.thisListensTo = new IPEndPoint(IPAddress.Any, connectionData.listenPort);
            //connectionData.listenToEndpoint = new IPEndPoint(IPAddress.Parse(connectionData.serverIp), connectionData.listenPort);
            connectors.udpClient = new UdpClient(connectionData.thisListensTo);
            Util.Exchange(ref  connectionState.udpStateName, StateOfConnection.ReadyToOperate);
            
            //connectors.udpClient = new UdpClient();
            //connectors.udpClient.Connect(connectionData.listenToEndpoint);//make the udp client react only to incoming messages that come from the given port
            Logging.Write("server: udpclient local: " + (IPEndPoint)connectors.udpClient.Client.LocalEndPoint);
            //            Logging.Write("server: udpclient remote: " + (IPEndPoint)connectors.udpClient.Client.RemoteEndPoint);

            connectors.tcpListener = new TcpListener(IPAddress.Any, connectionData.listenPort);
            connectors.tcpClient = null;

            for(int connectionI = 0; connectionI < toConnections.Length; connectionI ++)
            {
                connections[connectionI].tcpClient = null;
                connections[connectionI].tcpListener = null;
                connections[connectionI].tcpStream = null;
                connections[connectionI].udpClient = null;

                connectionDatas[connectionI].clientID = -1;
                connectionDatas[connectionI].thisListensTo = null;

                toConnections[connectionI] = new ConnectionState();
            }

        }


        public async static Task<ConnectionAPIs> SetupClientTCP(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState)
        {
            Util.Exchange(ref connectionState.tcpWriteStateName, StateOfConnection.Uninitialized);
            Util.Exchange(ref connectionState.tcpReadStateName, StateOfConnection.Uninitialized);
            
            connection.tcpClient = new TcpClient();


            Logging.Write("client connects to tcp" + connectionData.serverIp + ":" + connectionData.sendToPort + " " + connectionData.toSendToServer);
            await connection.tcpClient.ConnectAsync(connectionData.toSendToServer.Address, connectionData.toSendToServer.Port);

            Logging.Write("client connected");
            NetworkStream stream = connection.tcpClient.GetStream();
            connection.tcpStream = stream;
            Util.Exchange(ref connectionState.tcpWriteStateName, StateOfConnection.ReadyToOperate);
            Util.Exchange(ref connectionState.tcpReadStateName, StateOfConnection.ReadyToOperate);
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

            Util.Exchange(ref currentState.tcpWriteStateName, StateOfConnection.Uninitialized);
            Util.Exchange(ref currentState.tcpReadStateName, StateOfConnection.Uninitialized);

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

                Util.Exchange(ref clientStates[conI].tcpWriteStateName, StateOfConnection.Uninitialized);
                Util.Exchange(ref clientStates[conI].tcpReadStateName, StateOfConnection.Uninitialized);
                
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

            Util.Exchange(ref currentState.tcpWriteStateName, StateOfConnection.Uninitialized);
            Util.Exchange(ref currentState.tcpReadStateName, StateOfConnection.Uninitialized);
            
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
            
            Util.Exchange(ref currentState.udpStateName, StateOfConnection.Uninitialized);


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
                Util.Exchange(ref connectionState.udpStateName, StateOfConnection.ReadyToOperate);
                Logging.Write("client connects to udp" + connectionData.listenPort);
                //connectors.udpClient = new UdpClient(connectionData.listenToEndpoint);
                //connectors.udpClient.Connect(connectionData.listenToEndpoint);//make the udp client react only to incoming messages that come from the given port

                connectors.tcpListener = null;

                connectors = await SetupClientTCP(connectors, connectionData, connectionState);

                connectionData = await ExchangeConnectionInitials(connectors, connectionData, connectionState);

            }
            catch (SocketException e)
            {
                Logging.Write(e.Message);
            }
            return new Tuple<ConnectionAPIs, ConnectionMetaData>(connectors, connectionData);
        }

        private static async Task<ConnectionMetaData> ExchangeConnectionInitials(ConnectionAPIs connectors, ConnectionMetaData connectionData, ConnectionState connectionState)
        {
            //byte[] data = Encoding.ASCII.GetBytes(connectionData.listenPort.ToString());//only send the port we are listening to over udp
            Util.FlushBytes(connectionState.tcpWriteBytes);
            await MessageDeMultiplexer.MarkTCPConnectionRequest(connectionState.tcpWriteBytes,
                async () =>
                {
                    var portString = connectionData.listenPort.ToString();
                    
                    Encoding.ASCII.GetBytes(portString, 0, portString.Length, connectionState.tcpWriteBytes, 1);

                    Logging.Write("client write my port");
                    await connectors.tcpStream.WriteAsync(connectionState.tcpWriteBytes, 0, connectionState.tcpWriteBytes.Length);
                    Logging.Write("client written");

                    string response = null;
                    Util.FlushBytes(connectionState.tcpReadBytes);
                    var byteCount = await connectors.tcpStream.ReadAsync(connectionState.tcpReadBytes, 0, connectionState.tcpReadBytes.Length);

                    await MessageDeMultiplexer.Divide(connectionState.tcpReadBytes, async () => { Logging.Write("handle float!? unexpected reply to client's tcp connection request"); },
                        async () =>
                        {
                            Logging.Write("client read client id from "+ Encoding.ASCII.GetString(connectionState.tcpReadBytes, 0, byteCount ));
                            response = Encoding.ASCII.GetString(connectionState.tcpReadBytes, 1, byteCount - 1);
                            var myClientID = int.Parse(response);
                            connectionData.clientID = myClientID;
                            Logging.Write("i am client " + myClientID);
                        },
                        async () => { Logging.Write("handle tcp keepalive!? unexpected reply to client's tcp connection request"); });
                });
            return connectionData;
        }


        public static async void StartProcessTCPConnections(ConnectionMapping connectionMapping, ConnectionAPIs by, ConnectionState byState, ConnectionAPIs[] storeToConnections, ConnectionMetaData[] storeToConnectionDatas,ConnectionState[] storeToConnectionStates,  Func<bool> cancel, Action report)
        {

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
                        Util.FlushBytes(byState.tcpReadBytes);
                        readByteCount = await stream.ReadAsync(byState.tcpReadBytes, 0, byState.tcpReadBytes.Length);
                    } catch (Exception e)
                    {
                        Logging.Write("StartProcessTCPConnections: tcp listener stream got closed, (unfortunately) this is intended behaviour, stop reading.");
                        continue;
                    }

                    if ( readByteCount != 0)
                    {
                        await MessageDeMultiplexer.Divide(byState.tcpReadBytes,
                            async () => { Logging.Write("tcp float signals not yet implemented and should NOT occur here!?"); },
                            async () => {
                                //TODO here we need user defined filtering to deserialize into actual IncomingSignals!
                                //expect ud endpoint
                                data = Encoding.ASCII.GetString(byState.tcpReadBytes, 1, readByteCount-1);

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

                                    Util.Exchange(ref storeToConnectionStates[clientID].tcpWriteStateName, StateOfConnection.ReadyToOperate);
                                    Util.Exchange(ref storeToConnectionStates[clientID].tcpReadStateName, StateOfConnection.ReadyToOperate);

                                    Logging.Write("tcp received: " + data + " , will send back id " + clientID);

                                    Util.FlushBytes(byState.tcpWriteBytes);
                                    await MessageDeMultiplexer.MarkTCPConnectionRequest(byState.tcpWriteBytes, async () =>
                                    {
                                        var clientIdStr = clientID.ToString();
                                        
                                        Encoding.ASCII.GetBytes(clientIdStr, 0, clientIdStr.Length, byState.tcpWriteBytes, 1);
                                        try
                                        {
                                            await stream.WriteAsync(byState.tcpWriteBytes, 0, byState.tcpWriteBytes.Length);
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

        public static void InitializeMatchMakingServer(ref ConnectionAPIs connection, ref ConnectionMetaData data, ref ConnectionState state) 
        {
            try
            {
                
                connection.httpListener = new HttpListener();
                connection.httpListener.Prefixes.Add("http://*:" + 80.ToString() + "/");
                connection.httpListener.Start();
            }
            catch (Exception e)
            {
                Logging.Write(e.Message);
                return;
            }
        }


        public struct ServerListElementResponse
        {
            public string ip;
            public int port;
            public int currentPlayerCount;
            public int maxPlayerCount;
        }

        public static async void StartListenMatchmaking(ConnectionAPIs connection, ConnectionMetaData metaData, ConnectionState state)
        {
            //TODO StartListenMatchmaking again!

            var requestContext = await connection.httpListener.GetContextAsync();
            HttpListenerRequest request = requestContext.Request;
            HttpListenerResponse response = requestContext.Response;

            if (request.HttpMethod.Equals("PUT") && request.Url.LocalPath.Equals("/dedicatedkeepalive"))
            {
                var body = request.InputStream;
                var encoding = request.ContentEncoding;
                var reader = new System.IO.StreamReader(body, encoding);
                var data = await reader.ReadToEndAsync();
                /*
                 * TODO json
                 * var execution = JsonUtility.FromJson<InspectionExecution>(data);
                lock (inspectionExecutionLockObject)
                {
                    requestedInspectionExecution.executionStart = execution.executionStart;
                    requestedInspectionExecution.withGameObjectName = execution.withGameObjectName;
                    requestedInspectionExecution.withPosition = execution.withPosition;
                }
                */
                //Interlocked.Exchange<InspectionExecution>(ref requestedInspectionExecution, execution);
                body.Close();
                reader.Close();
            }

            if (request.HttpMethod.Equals("GET") && request.Url.LocalPath.Equals("/serverlist"))
            {
                //var responseString = "";//get from database

                /*
                var serverList = new List<ServerListElementResponse>();
                var element = new ServerListElementResponse();
                element.currentPlayerCount = 2;
                element.maxPlayerCount = 8;
                element.ip = "127.0.0.1";
                element.port = 3243;
                serverList.Add(element);
                */
                var connectionPath = "URI:file" + "DedicatedServerList";
                var con = new SQLiteConnection(connectionPath);
                con.Open();
                IDbCommand dbcmd;
                IDataReader reader;
                dbcmd = con.CreateCommand();
                /*string q_createTable =
                  "CREATE TABLE IF NOT EXISTS " + "my_table" + " (" +
                  "id" + " INTEGER PRIMARY KEY, " +
                  "val" + " INTEGER )";
                dbcmd.CommandText = q_createTable;*/
                dbcmd.CommandText = "FROM serverlisttable SELECT *";
                reader = dbcmd.ExecuteReader();
                while(reader.Read())
                {
                    var field0 = reader[0];
                    var field1 = reader[1];
                } // TODO: serialize results

                reader.Close();
                con.Close();

                var buffer = System.Text.Encoding.UTF8.GetBytes("SERVERLIST_PLACEHOLDER"); //TODO
                response.ContentLength64 = buffer.Length;
                using (System.IO.Stream outputStream = response.OutputStream)
                {
                    await outputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                response.Close();
            }
        }
    }


    public class SignalUpdater
    {

        private static AsyncCallback MakeHandleReceiveReliableSignal(IncomingSignal[] signals, ConnectionAPIs connection, ConnectionMetaData metaData, ConnectionState connectionState, Action<string> report)
        {
            return async (IAsyncResult ar) =>
            {
                try
                {
                    var byteCountRead = connection.tcpStream.EndRead(ar);
                    await WriteToIncomingSignals(metaData, signals, report, connectionState.tcpReadBytes);
                    
                    Util.Exchange(ref connectionState.tcpReadStateName, StateOfConnection.ReadyToOperate);


                }
                catch (ObjectDisposedException e)
                {
                    Logging.Write("MakeHandleReceiveReliableSignal: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (System.IO.IOException e)
                {
                    Logging.Write("MakeHandleReceiveReliableSignal: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (FormatException e)
                {
                    Logging.Write("MakeHandleReceiveReliableSignal: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
            };
        }
        
        //uses tcp to sync signals reliably
        public async static void ReceiveSignalsReliably(IncomingSignal[] signals, Func<bool> cancel, Action<string> report, ConnectionAPIs [] fromStreams, ConnectionMetaData [] fromDatas, ConnectionState [] fromStates )
        {
            
            try
            {
                while (!cancel())
                {
                    for( int streamI = 0; streamI < fromStreams.Length; streamI++)
                    {
                        var previousState = Util.CompareExchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                        //it was previously uninit, well then write uninit and leave
                        if(previousState == StateOfConnection.Uninitialized)
                        {
                            Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                            continue;
                        }
                        //it was previously busy, can not continue with that
                        if( previousState == StateOfConnection.BeingOperated)
                        {
                            continue;
                        }
                       
                        Logging.Write("ReceiveSignalsReliably: eligible for begin read?" + fromStates[streamI].tcpReadStateName.ToString());
                        try
                        {
                            Logging.Write("ReceiveSignalsReliably: begin read tcp stream of index " + streamI);
                            var usingBytes = fromStates[streamI].tcpReadBytes;
                            Util.FlushBytes(usingBytes);
                            Logging.Write("ReceiveSignalsReliably");
                            fromStreams[streamI].tcpStream.BeginRead(usingBytes, 0, usingBytes.Length, MakeHandleReceiveReliableSignal( signals,fromStreams[streamI], fromDatas[streamI], fromStates[streamI], report), null);
                        }
                        catch (ObjectDisposedException e)
                        {
                            Logging.Write("ReceiveSignals: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                            continue;
                        }
                    }
                    await Task.Delay(1);
                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }

        }
        //uses udp to sync signals unreliably
        public async static void SyncSignalsToAll(ConnectionAPIs with, ConnectionState connectionState, OutgoingSignal[] signals, Func<bool> cancel, params ConnectionMetaData[] all)
        {
            
            try
            {
                while (!cancel())
                {
                    foreach (var to in all)
                    {
                        for (int signalI = 0; signalI < signals.Length; signalI++)
                        {
                            if (signals[signalI].dataDirty )
                            {
                                var previousState = Util.CompareExchange(ref connectionState.udpStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                                //it was previously uninit, well then write uninit and leave
                                if (previousState == StateOfConnection.Uninitialized)
                                {
                                    Util.Exchange(ref connectionState.udpStateName, StateOfConnection.Uninitialized);
                                    continue;
                                }
                                //it was previously busy, can not continue with that
                                /*if (previousState == StateOfConnection.BeingOperated)
                                {
                                    continue;
                                }*/

                                IPEndPoint toSendTo = to.thisListensTo;
                                if (to.thisListensTo.Address == IPAddress.Any) //can not send to any, send to serverIP instead
                                {
                                    toSendTo = new IPEndPoint(IPAddress.Parse(to.serverIp), to.thisListensTo.Port);
                                }
                                Logging.Write("data is dirty. send it to " + to.thisListensTo);
                                //byte[] toSend = Encoding.ASCII.GetBytes(SignalCompressor.Compress(signals[signalI].data));
                                var dataStr = SignalCompressor.Compress(signals[signalI].data);
                                var usingBytes = connectionState.udpWriteBytes;
                                Util.FlushBytes(usingBytes);
                                await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () => {

                                    Encoding.ASCII.GetBytes(dataStr, 0, dataStr.Length, usingBytes, 1);
                                    try
                                    {
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

        //TODO? : make use of the 
        public async static void SyncSignalsToAllReliably(OutgoingSignal[] signals,Func<bool> cancel, ConnectionAPIs [] toConnections, ConnectionMetaData[] toConnectionsDatas, ConnectionState [] toConnectionStates)
        {
            try
            {
                while (!cancel())
                {
                    for(var connectionI = 0; connectionI  < toConnections.Length; connectionI++)
                    {
                        for (int signalI = 0; signalI < signals.Length; signalI++)
                        {
                            
                            if (signals[signalI].dataDirty )
                            {

                                var previousState = Util.CompareExchange(ref toConnectionStates[connectionI].tcpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                                //it was previously uninit, well then write uninit and leave
                                if (previousState == StateOfConnection.Uninitialized)
                                {
                                    Util.Exchange(ref toConnectionStates[connectionI].tcpWriteStateName, StateOfConnection.Uninitialized);
                                    continue;
                                }
                                //it was previously busy, can not continue with that
                                if (previousState == StateOfConnection.BeingOperated)
                                {
                                    continue;
                                }

                                Logging.Write("SyncSignalsToAllReliably: eligible for begin write?" + toConnectionStates[connectionI].tcpWriteStateName.ToString());
                                Logging.Write("data is dirty. send it reliably" );
                                var dataStr = SignalCompressor.Compress(signals[signalI].data);
                                var usingBytes = toConnectionStates[connectionI].tcpWriteBytes;                                
                                Util.FlushBytes(usingBytes);
                                await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () => {

                                    Encoding.ASCII.GetBytes(dataStr, 0, dataStr.Length, usingBytes, 1);
                                    try
                                    {
                                        await toConnections[connectionI].tcpStream.WriteAsync(usingBytes,0, usingBytes.Length);
                                    }
                                    catch (SocketException e)
                                    {
                                        Logging.Write("SyncSignalsToAll: tcp client socket got closed, (unfortunately) this is intended behaviour, stop sending.");
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



        public async static void ReceiveSignals(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState, IncomingSignal[] signals, Func<bool> cancel, Action<string> report)
        {
            //TODO currently unused
            var usingBytes = connectionState.udpReadBytes;
            try
            {
                


                IPEndPoint receiveFrom = new IPEndPoint(connectionData.thisListensTo.Address, connectionData.thisListensTo.Port);
                while (!cancel())
                {

                    var previousState = Util.CompareExchange(ref connectionState.udpStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                    //it was previously uninit, well then write uninit and leave
                    if (previousState == StateOfConnection.Uninitialized)
                    {
                        Util.Exchange(ref connectionState.udpStateName, StateOfConnection.Uninitialized);
                        await Task.Delay(1);
                        continue;
                    }
                    //it was previously busy, can not continue with that
                    /*if (previousState == StateOfConnection.BeingOperated)
                    {
                        continue;
                    }*/


                    //dont know a better way: receive async does not accept cancellation tokens, so need to let it fail here (because some other disposed the udpclient)
                    UdpReceiveResult receiveResult;
                    try
                    {
                        Logging.Write("ReceiveSignals");
                        receiveResult = await connection.udpClient.ReceiveAsync();
                    }
                    catch (ObjectDisposedException e)
                    {
                        Logging.Write("ReceiveSignals: udp socket has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                        continue;
                    }
                    receiveFrom = receiveResult.RemoteEndPoint;
                    var bytes = receiveResult.Buffer;

                    await WriteToIncomingSignals(connectionData, signals, report, bytes);
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

        private static async Task WriteToIncomingSignals(ConnectionMetaData connectionData, IncomingSignal[] signals, Action<string> report, byte[] bytes)
        {
            await MessageDeMultiplexer.Divide(bytes, async () =>
            {
                Logging.Write("I (" + connectionData.thisListensTo + ") received sth ");

                Logging.Write("receive from (?)" + connectionData.thisListensTo);
                Logging.Write("parse " + bytes.ToString() + " # " + bytes.Length );
                var parsedString = Encoding.ASCII.GetString(bytes, 1, bytes.Length - 1);
                Logging.Write("report " + parsedString);
                report(parsedString);
                var package = SignalCompressor.Decompress(parsedString);
                signals[package.id].data = package;
            },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package connection request!?"); },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package tcp keepalive!?"); });
        }
    }

    public class NetSignalStarter
    {
        public async static Task<Tuple<ConnectionAPIs,ConnectionMetaData,ConnectionMapping>> StartServer(
            ConnectionAPIs [] serverConnection, ConnectionMetaData [] serverData, ConnectionState [] serverState, Func<bool> cancel, ConnectionMapping connectionMapping, ConnectionAPIs[] connections, 
            ConnectionMetaData[] connectionDatas, ConnectionState[] connectionStates,
            OutgoingSignal[] unreliableOutgoingSignals, IncomingSignal[] unreliableIncomingSignals,
            OutgoingSignal[] reliableOutgoingSignals, IncomingSignal[] reliableIncomingSignals)
        {
            //serverData.listenPort = 3000;
            //serverData.serverIp = null;
            Logging.Write("StartServer: init multi connection");
            ConnectionUpdater.InitializeMultiConnection(ref serverConnection[0], ref serverData[0], serverState[0], connections, connectionDatas, connectionStates);

            connectionMapping.ClientIdentificationToEndpoint = new Dictionary<int, IPEndPoint>();
            connectionMapping.EndPointToClientIdentification = new Dictionary<IPEndPoint, int>();

            Logging.Write(serverConnection[0].udpClient.ToString());

            Logging.Write("StartServer: start receive signals");
            //SignalUpdater.StartThreadReceiveSignals(serverConnection, serverData, incomingSignals, cancel, (string s) => Logging.Write(s));
            SignalUpdater.ReceiveSignals(serverConnection[0], serverData[0], serverState[0], unreliableIncomingSignals, cancel, (string s) => Logging.Write(s));

            SignalUpdater.ReceiveSignalsReliably(reliableIncomingSignals, cancel, (string s) => Logging.Write(s), connections, connectionDatas, connectionStates);

            Logging.Write("StartServer: start accept tcp connections");
            //ConnectionUpdater.StartThreadAcceptTCPConnections(connectionMapping, serverConnection, connections, connectionDatas, cancel, () => { });
            ConnectionUpdater.StartProcessTCPConnections(connectionMapping, serverConnection[0], serverState[0], connections, connectionDatas, connectionStates, cancel, () => { });

            Logging.Write("StartServer: start sync signals");
            //SignalUpdater.StartThreadSyncSignalsToAll(serverConnection, outgoingSignals, cancel, connectionDatas);
            SignalUpdater.SyncSignalsToAll(serverConnection[0], serverState[0], unreliableOutgoingSignals, cancel, connectionDatas);

            SignalUpdater.SyncSignalsToAllReliably(reliableOutgoingSignals, cancel,connections, connectionDatas, connectionStates);

            ConnectionUpdater.AwaitAndPerformTearDownClientUDP(serverConnection[0], cancel, serverState[0]);
            ConnectionUpdater.AwaitAndPerformTearDownTCPListener(serverConnection[0], cancel, serverState[0], connections, connectionStates, connectionDatas, connectionMapping);

            return new Tuple<ConnectionAPIs, ConnectionMetaData, ConnectionMapping>(serverConnection[0], serverData[0], connectionMapping);
        }

        //please provide array with one element for server*
        public async static Task<Tuple<ConnectionAPIs,ConnectionMetaData>> StartClient(ConnectionAPIs [] clientCon, ConnectionMetaData [] clientData, ConnectionState [] clientState, 
            ConnectionAPIs [] server,  ConnectionMetaData [] serverData, ConnectionState [] serverState, 
            Func<bool> cancel,
            OutgoingSignal[] unreliableOutgoingSignals, IncomingSignal[] unreliableIncomingSignals,
            OutgoingSignal[] reliableOutgoingSignals, IncomingSignal[] reliableIncomingSignals)
        {
            /*clientData.listenPort = 3001;
            clientData.sendToPort = 3000;
            clientData.serverIp = "127.0.0.1";*/
            Logging.Write("StartClient: init single connection");
            var returnTuple = await ConnectionUpdater.InitializeSingleConnection(clientCon[0], clientData[0], clientState[0]);
            clientCon[0] = returnTuple.Item1;
            clientData[0] = returnTuple.Item2;

            Logging.Write("StartClient: start receive signals");
            //SignalUpdater.StartThreadReceiveSignals(clientCon, clientData, incomingSignals, cancel, (string s) => Logging.Write(s));
            SignalUpdater.ReceiveSignals(clientCon[0], clientData[0], clientState[0], unreliableIncomingSignals,  cancel, (string s) => Logging.Write(s));

            
            SignalUpdater.ReceiveSignalsReliably(reliableIncomingSignals, cancel, (string s) => Logging.Write(s), clientCon, clientData, clientState);

            Logging.Write("StartClient: start sync signals to server");
            //SignalUpdater.StartThreadSyncSignalsToAll(clientCon, outgoingSignals, cancel, toServer);
            SignalUpdater.SyncSignalsToAll(clientCon[0], clientState[0], unreliableOutgoingSignals, cancel, serverData);

            SignalUpdater.SyncSignalsToAllReliably(reliableOutgoingSignals, cancel, clientCon, clientData, clientState);


            ConnectionUpdater.AwaitAndPerformTearDownClientTCP(clientCon[0], cancel, clientState[0]);
            ConnectionUpdater.AwaitAndPerformTearDownClientUDP(clientCon[0], cancel, clientState[0]);

            //var datagram = Encoding.ASCII.GetBytes("hellosent");
            //clientCon.udpClient.Send(datagram, datagram.Length);

            return new Tuple<ConnectionAPIs, ConnectionMetaData>(clientCon[0], clientData[0]);
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
            OutgoingSignal[] unreliableSignalSentFromServer = new OutgoingSignal[2];
            IncomingSignal[] unreliableSignalSeenFromClient = new IncomingSignal[2];
            OutgoingSignal[] unreliableSignalsSentFromClient = new OutgoingSignal[2];
            IncomingSignal[] unreliableSignalsSeenFromServer = new IncomingSignal[2];
            OutgoingSignal[] reliableSignalSentFromServer = new OutgoingSignal[2];
            IncomingSignal[] reliableSignalSeenFromClient = new IncomingSignal[2];
            OutgoingSignal[] reliableSignalsSentFromClient = new OutgoingSignal[2];
            IncomingSignal[] reliableSignalsSeenFromServer = new IncomingSignal[2];
            var cancel = false;
            var shouldPrint = false;
            ConnectionMetaData[] connectionMetaDatasSeenFromServer = new ConnectionMetaData[1];
            ConnectionAPIs[] connectionApisSeenFromServer = new ConnectionAPIs[1];
            ConnectionState[] connectionStatesSeenFromServer = new ConnectionState[1];


            //only for symmetry, this is always supposed to be an array of size one, 
            ConnectionAPIs [] server = new ConnectionAPIs[1] {new ConnectionAPIs() };
            ConnectionMetaData [] serverData = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState [] serverState = new ConnectionState[1] { new ConnectionState() };
            ConnectionMapping mapping = new ConnectionMapping();

            //this can and will be array of size N
            ConnectionAPIs [] clients = new ConnectionAPIs[1] { new ConnectionAPIs() };
            ConnectionMetaData [] clientDatas = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState [] clientState = new ConnectionState[1] { new ConnectionState() };

            serverData[0].listenPort = 3000;
            serverData[0].serverIp = "127.0.0.1";

            //TODO go on here
            /*
             * matchmaking server listens to http, following endpoints:
             * x dedicated server register (will be done manually)
             * - dedicated server update free slots and keepalive
             * - client ask for server list (paged subset)
             * 
            matchmakingServerData.matchmakingServerIp = "127.0.0.1";
            matchmakingServerData.matchmakingServerPort = 80;
            */

            clientDatas[0].listenPort = 3001;
            clientDatas[0].serverIp = "127.0.0.1";
            clientDatas[0].sendToPort = 3000;


            //NetSignalStarter.TestIncomingOnClient(() => cancel, () => shouldPrint, signalSentFromServer, signalSeenFromClient, signalsSentFromClient, signalsSeenFromServer, conFromServer, consFromServer);
            //await Task.Delay(5000);
            await NetSignalStarter.TestDuplex(() => cancel, () => shouldPrint, 
                unreliableSignalSentFromServer, unreliableSignalSeenFromClient, unreliableSignalsSentFromClient, unreliableSignalsSeenFromServer,
                reliableSignalSentFromServer, reliableSignalSeenFromClient, reliableSignalsSentFromClient, reliableSignalsSeenFromServer,
                connectionApisSeenFromServer, connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer,
                server, serverData, serverState, mapping, clients, clientDatas, clientState);

            cancel = true;
            //TODOS:
            //move from synchronous to ASYNC send and receive. (CHECK)
            //do not always open and close new tcp connection? 
            // - close tcp listener and clients on server side (check)
            // - close tcp client on client side (check)
            // - also close udp on client and server side if necessary (check)
            //implement sync and receive signals RELIABLE version (over tcp) (CHECK)
            //implement websocket for matchmaking (to find ip to connect to server), set up with strato (?) 
            //refactor into separate files 
            //import to unity
            //battletest: make scriptable objects that have Incoming- and Outgoing Signals, write Mono Updaters that assign Signals to specific game objects (mainly: Bird slots, state enums for UI)

            /*
             * 
             * var getResponse = await httpClient.GetAsync("http://127.0.0.1:" + InitialPort + "/porttolistento");
                getResponse.EnsureSuccessStatusCode();
                return await getResponse.Content.ReadAsStringAsync();
             * 
             * 
             * prefixToListenTo = int.Parse(response);
                    httpListener = new HttpListener();
                    httpListener.Prefixes.Add("http://*:" + prefixToListenTo.ToString() + "/");
                    httpListener.Start();
                    httpListener.BeginGetContext(new AsyncCallback(HandleRequest), null);
                    message.str += "Live Variable Inspector ID: " + prefixToListenTo;
             * 
             * */

        }



        public async static Task TestDuplex(
        Func<bool> cancel,
        Func<bool> shouldReport,
        OutgoingSignal[] unreliableSignalsSentFromServer,
        IncomingSignal[] unreliableSignalsSeenFromClient,
        OutgoingSignal[] unreliableSignalsSentFromClient,
        IncomingSignal[] unreliableSignalsSeenFromServer,
        OutgoingSignal[] reliableSignalsSentFromServer,
        IncomingSignal[] reliableSignalsSeenFromClient,
        OutgoingSignal[] reliableSignalsSentFromClient,
        IncomingSignal[] reliableSignalsSeenFromServer,
        ConnectionAPIs[] clientConnectionsSeenFromServer,
        ConnectionMetaData[] clientConnectionDatasSeenFromServer,
        ConnectionState[] clientConnectionStatesSeenFromServer,

        ConnectionAPIs[] serverInstanceAPI,
        ConnectionMetaData[] serverInstanceData,
        ConnectionState[] serverInstanceState,
        ConnectionMapping mapping,

        ConnectionAPIs[] clientInstancesAPI,
        ConnectionMetaData[] clientInstancesData,
        ConnectionState[] clientInstancesState
        )
        {

            



            Logging.Write("TestDuplex: start server");
            var updatedServerTuple = await StartServer(serverInstanceAPI, serverInstanceData, serverInstanceState, cancel, mapping, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer,
                clientConnectionStatesSeenFromServer, 
                unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer);
            serverInstanceAPI[0] = updatedServerTuple.Item1;
            serverInstanceData[0] = updatedServerTuple.Item2;
            mapping = updatedServerTuple.Item3;

            await Task.Delay(1000);
            Logging.Write("TestDuplex: start client");
            var updatedTuple = await StartClient(clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceAPI, serverInstanceData, serverInstanceState, cancel, 
                unreliableSignalsSentFromClient, unreliableSignalsSeenFromClient,
                reliableSignalsSentFromClient, reliableSignalsSeenFromClient);
            clientInstancesAPI[0] = updatedTuple.Item1;
            clientInstancesData[0] = updatedTuple.Item2;

            //Server and Client are not listening/sending to the right endpoint?!
            //server listens to Ip.any, 3000 , initialized with UdpClient(...), whereas client doesnt listen, is init with UdpClient() and then .Connect(...) . what exactly is the differnece?

            if (!cancel())
            {
                //if(shouldReport())
                {
                    LogClientToServerCommunication(unreliableSignalsSentFromClient, unreliableSignalsSeenFromServer);
                }
                await Task.Delay(1000);
            }
            if (!cancel())
            {
                var a = new FloatDataPackage();
                a.data = 13.331f;
                a.id = 0;
                a.timeStamp = DateTime.UtcNow;
                unreliableSignalsSentFromClient[0].data = a;
                await Task.Delay(1000);
            }

            if (!cancel())
            {
                LogClientToServerCommunication(unreliableSignalsSentFromClient, unreliableSignalsSeenFromServer);
                await Task.Delay(1000);
            }



            if (!cancel())
            {
                //if(shouldReport())
                {
                    LogServerToClientCommunication(unreliableSignalsSentFromServer, unreliableSignalsSeenFromClient);
                }
                await Task.Delay(1000);
            }
            if (!cancel())
            {
                var a = new FloatDataPackage();
                a.data = 13.331f;
                a.id = 0;
                a.timeStamp = DateTime.UtcNow;
                unreliableSignalsSentFromServer[0].data = a;
                await Task.Delay(1000);
            }

            if (!cancel())
            {
                //if(shouldReport())
                LogServerToClientCommunication(unreliableSignalsSentFromServer, unreliableSignalsSeenFromClient);
                await Task.Delay(1000);
            }


            //reliable----------------------------------


            await Task.Delay(3000);
            Logging.Write("RELIABLE SIGNALS: ------------------------------------------------------------");


            if (!cancel())
            {
                //if(shouldReport())
                {
                    LogClientToServerCommunication(reliableSignalsSentFromClient, reliableSignalsSeenFromServer);
                }
                await Task.Delay(1000);
            }
            if (!cancel())
            {
                var a = new FloatDataPackage();
                a.data = 42f;
                a.id = 0;
                a.timeStamp = DateTime.UtcNow;
                reliableSignalsSentFromClient[0].data = a;
                await Task.Delay(1000);
            }

            if (!cancel())
            {
                LogClientToServerCommunication(reliableSignalsSentFromClient, reliableSignalsSeenFromServer);
                await Task.Delay(1000);
            }



            if (!cancel())
            {
                //if(shouldReport())
                {
                    LogServerToClientCommunication(reliableSignalsSentFromServer, reliableSignalsSeenFromClient);
                }
                await Task.Delay(1000);
            }
            if (!cancel())
            {
                var a = new FloatDataPackage();
                a.data = 42f;
                a.id = 0;
                a.timeStamp = DateTime.UtcNow;
                reliableSignalsSentFromServer[0].data = a;
                await Task.Delay(1000);
            }

            if (!cancel())
            {
                //if(shouldReport())
                LogServerToClientCommunication(reliableSignalsSentFromServer, reliableSignalsSeenFromClient);
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
