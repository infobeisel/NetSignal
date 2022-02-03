using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSignal
{
    public class ConnectionUpdater
    {
        
        public static void InitializeMultiConnection(ref ConnectionAPIs connectors, ref ConnectionMetaData connectionData, ConnectionState connectionState, ConnectionAPIs [] connections, ConnectionMetaData [] connectionDatas, ConnectionState [] toConnections)
        {
            connectors = new ConnectionAPIs();

            
            
            connectors.udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, connectionData.iListenToPort));
            Util.Exchange(ref  connectionState.udpStateName, StateOfConnection.ReadyToOperate);
            
            //connectors.udpClient = new UdpClient();
            //connectors.udpClient.Connect(connectionData.listenToEndpoint);//make the udp client react only to incoming messages that come from the given port
            Logging.Write("server: udpclient local: " + (IPEndPoint)connectors.udpClient.Client.LocalEndPoint);
            //            Logging.Write("server: udpclient remote: " + (IPEndPoint)connectors.udpClient.Client.RemoteEndPoint);

            connectors.tcpListener = new TcpListener(IPAddress.Any, connectionData.iListenToPort);
            connectors.tcpClient = null;

            for(int connectionI = 0; connectionI < toConnections.Length; connectionI ++)
            {
                connections[connectionI].tcpClient = null;
                connections[connectionI].tcpListener = null;
                connections[connectionI].tcpStream = null;
                connections[connectionI].udpClient = null;

                connectionDatas[connectionI].clientID = -1;

                toConnections[connectionI] = new ConnectionState();
            }

        }


        public async static Task<ConnectionAPIs> SetupClientTCP(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState, ConnectionMetaData connectTo)
        {
            Util.Exchange(ref connectionState.tcpWriteStateName, StateOfConnection.Uninitialized);
            Util.Exchange(ref connectionState.tcpReadStateName, StateOfConnection.Uninitialized);
            
            connection.tcpClient = new TcpClient();


            Logging.Write("client connects to tcp" + connectTo .myIp + ":" + connectTo.iListenToPort+ " " );
            
            await connection.tcpClient.ConnectAsync(IPAddress.Parse(connectTo.myIp), connectTo.iListenToPort);

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

        public async static Task<Tuple<ConnectionAPIs, ConnectionMetaData>> InitializeSingleConnection(ConnectionAPIs connectors, ConnectionMetaData connectionData, ConnectionState connectionState, ConnectionMetaData toServer)
        {
            connectors = new ConnectionAPIs();
                

            connectors.udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, connectionData.iListenToPort));
            Util.Exchange(ref connectionState.udpStateName, StateOfConnection.ReadyToOperate);
            Logging.Write("client connects to udp" + new IPEndPoint(IPAddress.Any, connectionData.iListenToPort));
            //connectors.udpClient = new UdpClient(connectionData.listenToEndpoint);
            //connectors.udpClient.Connect(connectionData.listenToEndpoint);//make the udp client react only to incoming messages that come from the given port

            connectors.tcpListener = null;

            connectors = await SetupClientTCP(connectors, connectionData, connectionState, toServer);

            connectionData = await ExchangeConnectionInitials(connectors, connectionData, connectionState);

            return new Tuple<ConnectionAPIs, ConnectionMetaData>(connectors, connectionData);
        }

        private static async Task<ConnectionMetaData> ExchangeConnectionInitials(ConnectionAPIs connectors, ConnectionMetaData connectionData, ConnectionState connectionState)
        {
            //byte[] data = Encoding.ASCII.GetBytes(connectionData.listenPort.ToString());//only send the port we are listening to over udp
            Util.FlushBytes(connectionState.tcpWriteBytes);
            await MessageDeMultiplexer.MarkTCPConnectionRequest(connectionState.tcpWriteBytes,
                async () =>
                {
                    var portString = connectionData.iListenToPort.ToString();
                    
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

                                    storeToConnectionDatas[clientID].clientID = clientID;
                                    Logging.Write("client told me he is " + (clientEndpoint).Address + " " + (clientEndpoint).Port);
                                    storeToConnectionDatas[clientID].iListenToPort = clientEndpoint.Port;
                                    storeToConnectionDatas[clientID].myIp =
                                    clientEndpoint.Address.ToString();

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

                
    }

}
