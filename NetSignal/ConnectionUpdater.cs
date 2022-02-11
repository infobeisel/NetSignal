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
            Util.Exchange(ref  connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate);
            Util.Exchange(ref connectionState.udpReadStateName, StateOfConnection.ReadyToOperate);


            Logging.Write("server: udpclient local: " + (IPEndPoint)connectors.udpClient.Client.LocalEndPoint);
            

            connectors.tcpListener = new TcpListener(IPAddress.Any, connectionData.iListenToPort);
            connectors.tcpClient = null;

            for(int connectionI = 0; connectionI < toConnections.Length; connectionI ++)
            {
                connections[connectionI].tcpClient = null;
                connections[connectionI].tcpListener = null;
                connections[connectionI].tcpStream = null;
                connections[connectionI].udpClient = new UdpClient();



                

                toConnections[connectionI] = new ConnectionState();
                Util.Exchange(ref toConnections[connectionI].udpWriteStateName, StateOfConnection.ReadyToOperate);
                Util.Exchange(ref toConnections[connectionI].udpReadStateName, StateOfConnection.ReadyToOperate);
            }

        }


        public async static Task<ConnectionAPIs> SetupClientTCP(ConnectionAPIs connection, ConnectionState connectionState, ConnectionMetaData connectTo)
        {
            Util.Exchange(ref connectionState.tcpWriteStateName, StateOfConnection.Uninitialized);
            Util.Exchange(ref connectionState.tcpReadStateName, StateOfConnection.Uninitialized);
            
            connection.tcpClient = new TcpClient();


            Logging.Write("client connects to tcp" + connectTo .myIp + ":" + connectTo.iListenToPort+ " " );
            
            await connection.tcpClient.ConnectAsync(IPAddress.Parse(connectTo.myIp), connectTo.iListenToPort);

            Logging.Write("client connected");
            NetworkStream stream = connection.tcpClient.GetStream();
            stream.ReadTimeout = 3000;
            stream.WriteTimeout = 3000;
            connection.tcpStream = stream;
            Util.Exchange(ref connectionState.tcpWriteStateName, StateOfConnection.ReadyToOperate);
            Util.Exchange(ref connectionState.tcpReadStateName, StateOfConnection.ReadyToOperate);
            connectionState.tcpKeepAlive = DateTime.UtcNow;
            return connection;
        }


        public async static void AwaitAndPerformTearDownTCPListenerAndUdpToClients(ConnectionAPIs connection, Func<bool> shouldTearDown, ConnectionState currentState, ConnectionAPIs [] clientCons, ConnectionState[] clientStates, ConnectionMetaData [] clientDatas)
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
                TearDownClientSeenFromServer(clientCons, clientStates, clientDatas, conI);
            }

        }

        public static void TearDownClientSeenFromServer(ConnectionAPIs[] clientCons, ConnectionState[] clientStates, ConnectionMetaData[] clientDatas, int conI)
        {
            clientStates[conI].isConnectionActive = false;
            Util.Exchange(ref clientStates[conI].tcpWriteStateName, StateOfConnection.Uninitialized);
            Util.Exchange(ref clientStates[conI].tcpReadStateName, StateOfConnection.Uninitialized);
            clientStates[conI].clientID = -1;

            clientCons[conI].tcpStream?.Close();
            clientCons[conI].tcpStream = null;

            clientCons[conI].tcpClient?.Close();
            clientCons[conI].tcpClient = null;

            clientCons[conI].tcpListener = null;

            clientCons[conI].udpClient?.Close();
            clientCons[conI].udpClient?.Dispose();
            clientCons[conI].udpClient = null;


            
        }

        public static void TearDownTcpOfClient(ConnectionAPIs[] fromStreams, ConnectionState[] fromStates, int streamI)
        {
            Util.Exchange(ref fromStates[streamI].tcpWriteStateName, StateOfConnection.Uninitialized);
            Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);

            fromStreams[streamI].tcpStream?.Close();
            fromStreams[streamI].tcpStream = null;

            fromStreams[streamI].tcpClient?.Close();
            fromStreams[streamI].tcpClient = null;
        }

        public async static void AwaitAndPerformTearDownClientTCP(ConnectionAPIs connection, Func<bool> shouldTearDown, ConnectionState currentState)
        {
            while (!shouldTearDown())
            {
                await Task.Delay(1000);
            }
            
            Logging.Write("clean up tcp client");
            currentState.isConnectionActive = false;
            Util.Exchange(ref currentState.tcpWriteStateName, StateOfConnection.Uninitialized);
            Util.Exchange(ref currentState.tcpReadStateName, StateOfConnection.Uninitialized);
            
            connection.tcpStream?.Close();
            connection.tcpClient?.Close();

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
            
            Util.Exchange(ref currentState.udpWriteStateName, StateOfConnection.Uninitialized);
            Util.Exchange(ref currentState.udpReadStateName, StateOfConnection.Uninitialized);


            connection.udpClient.Close();
        }

        public async static Task<ConnectionAPIs> InitializeSingleConnection(ConnectionAPIs connectors, ConnectionMetaData connectionData, ConnectionState connectionState, ConnectionMetaData toServer)
        {
            connectors = new ConnectionAPIs();
                

            connectors.udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, connectionData.iListenToPort));
            Util.Exchange(ref connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate);
            Util.Exchange(ref connectionState.udpReadStateName, StateOfConnection.ReadyToOperate);
            Logging.Write("client connects to udp" + new IPEndPoint(IPAddress.Any, connectionData.iListenToPort));

            connectors.tcpListener = null;

            connectors = await SetupClientTCP(connectors, connectionState, toServer);

            await ExchangeConnectionInitials(connectors, connectionData, connectionState);

            return connectors;
        }

        private static async Task ExchangeConnectionInitials(ConnectionAPIs connectors, ConnectionMetaData connectionData, ConnectionState connectionState)
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
                            connectionState.clientID = myClientID;
                            connectionState.isConnectionActive = true;
                            Logging.Write("i am client " + myClientID);
                        },
                        async () => { Logging.Write("handle tcp keepalive!? unexpected reply to client's tcp connection request"); },
                        async () => { Logging.Write("handle udp keepalive!? unexpected reply to client's tcp connection request"); });
                });
        }


        public static async void StartProcessTCPConnections( ConnectionAPIs by, ConnectionState byState, ConnectionAPIs[] storeToConnections, ConnectionMetaData[] storeToConnectionDatas,ConnectionState[] storeToConnectionStates,  Func<bool> cancel, Action report)
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
                        await Task.Delay(5000);
                        continue;
                    }
                    
                    data = null;
                    NetworkStream stream = connection.GetStream();
                    stream.ReadTimeout = 3000;
                    stream.WriteTimeout = 3000;

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
                        await Task.Delay(5000);
                        continue;
                    }

                    if ( readByteCount != 0)
                    {
                        await MessageDeMultiplexer.Divide(byState.tcpReadBytes,
                            async () => { Logging.Write("tcp float signals not yet implemented and should NOT occur here!?"); },
                            async () => {

                                data = Encoding.ASCII.GetString(byState.tcpReadBytes, 1, readByteCount-1);

                                //incoming connection, try to identify
                                IPEndPoint clientEndpoint;
                                int clientID;
                                IdentifyClient(data, storeToConnections,storeToConnectionStates,  connection, out clientEndpoint, out clientID);

                                if (clientID == -1)
                                {
                                    //sth went wrong
                                    connection.Close(); // TODO WHAT TO DO HERE?
                                    Logging.Write("sth went wrong: too many players?");
                                } else
                                {
                                    //remember the client
                                    storeToConnections[clientID].tcpClient = connection;
                                    storeToConnections[clientID].tcpStream = stream;

                                    
                                    Logging.Write("client told me he is " + (clientEndpoint).Address + " " + (clientEndpoint).Port);
                                    storeToConnectionDatas[clientID].iListenToPort = clientEndpoint.Port;
                                    storeToConnectionDatas[clientID].myIp =
                                    clientEndpoint.Address.ToString();

                                    storeToConnectionStates[clientID].tcpKeepAlive = DateTime.UtcNow;
                                    storeToConnectionStates[clientID].clientID = clientID;


                                    Logging.Write("tcp received: " + data + " , will send back id " + clientID);

                                    Util.FlushBytes(byState.tcpWriteBytes);
                                    await MessageDeMultiplexer.MarkTCPConnectionRequest(byState.tcpWriteBytes, async () =>
                                    {
                                        var clientIdStr = clientID.ToString();
                                        
                                        Encoding.ASCII.GetBytes(clientIdStr, 0, clientIdStr.Length, byState.tcpWriteBytes, 1);
                                        try
                                        {
                                            await stream.WriteAsync(byState.tcpWriteBytes, 0, byState.tcpWriteBytes.Length);
                                            storeToConnectionStates[clientID].isConnectionActive = true;
                                        }
                                        catch (Exception e)
                                        {
                                            Logging.Write("StartProcessTCPConnections: tcp listener stream got closed, (unfortunately) this is intended behaviour, stop writing.");
                                        }

                                    });

                                    Util.Exchange(ref storeToConnectionStates[clientID].tcpWriteStateName, StateOfConnection.ReadyToOperate);
                                    Util.Exchange(ref storeToConnectionStates[clientID].tcpReadStateName, StateOfConnection.ReadyToOperate);


                                    Logging.Write("connections: ");
                                    foreach(var c in storeToConnectionDatas)
                                    {
                                        Logging.Write(c.ToString());
                                    }



                                }
                            },
                            async  () => { Logging.Write("tcp keepalive receive not yet implemented and should NOT occur here!?"); },
                            async () => { Logging.Write("udp keepalive receive not yet implemented and should NOT occur here!?"); }
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



        public async static void PeriodicallySendKeepAlive(ConnectionAPIs with, ConnectionMetaData connection, ConnectionState connectionState, ConnectionMetaData [] toServers, Action<string> report, Func<bool> cancel, int msKeepAlivePeriod = 1000)
        {
            Logging.Write("PeriodicallySendKeepAlive on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            try
            {
                while (!cancel())
                {
                    var previousState = Util.CompareExchange(ref connectionState.udpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                    if (previousState == StateOfConnection.Uninitialized)
                        await Task.Delay(msKeepAlivePeriod);//pause

                    if (previousState != StateOfConnection.ReadyToOperate)
                    {
                        await Task.Delay(msKeepAlivePeriod);//pause
                        continue;
                    }
                    var package = new KeepAlivePackage();
                    package.clientId = connectionState.clientID;
                    package.timeStamp = DateTime.UtcNow;

                    var usingBytes = connectionState.udpWriteBytes;
                    Util.FlushBytes(usingBytes);


                    await MessageDeMultiplexer.MarkUdpKeepAlive(usingBytes, async () =>
                    {
                        SignalCompressor.Compress(package, usingBytes, 1);
                        foreach (var serverData in toServers)
                        {
                            IPEndPoint toSendTo = new IPEndPoint(IPAddress.Parse(serverData.myIp), serverData.iListenToPort);
                            
                            report("keepalive " + package + " to " + toSendTo);
                            try
                            {
                                await with.udpClient.SendAsync(usingBytes, usingBytes.Length, toSendTo);
                            }
                            catch (SocketException e)
                            {
                                Logging.Write("PeriodicallySendKeepAlive: udp client socket got closed, (unfortunately) this is intended behaviour, stop sending.");
                            }
                        }
                    });
                    Util.Exchange(ref connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate);

                    await Task.Delay(msKeepAlivePeriod);
                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }
        }

        private static void IdentifyClient(string fromTCPMessage, ConnectionAPIs[] storeToConnections,ConnectionState [] storeToConnectionStates,  TcpClient connection, out IPEndPoint clientEndpoint, out int clientID)
        {
            //clientEndpoint = (IPEndPoint)connection.Client.RemoteEndPoint;
            //var splitIPAndPort = fromTCPMessage.Split('|');
            var connnectedToIPAddress = ((IPEndPoint)connection.Client.RemoteEndPoint).Address;
            var dataContainingListenPort = int.Parse(fromTCPMessage);
            //clientEndpoint = new IPEndPoint(IPAddress.Parse(splitIPAndPort[0]), int.Parse(splitIPAndPort[1]));
            clientEndpoint = new IPEndPoint(connnectedToIPAddress, dataContainingListenPort);

            Logging.Write("try to identify client with endpoint " + clientEndpoint);
            clientID = -1;

            int firstFreeIndex = int.MaxValue;
            for(int connectionI = 0; connectionI < storeToConnections.Length; connectionI++)
            {
                if(StateOfConnection.Uninitialized == (StateOfConnection) storeToConnectionStates[connectionI].tcpReadStateName)
                {
                    firstFreeIndex = Math.Min(firstFreeIndex, connectionI);
                }
            }

            if(firstFreeIndex != int.MaxValue)
            {
                clientID = firstFreeIndex;

            }
        }

                
    }

}
