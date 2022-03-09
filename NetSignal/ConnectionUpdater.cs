using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSignal
{
    public class ConnectionUpdater
    {
        public const int ReservedUnreliableSignalCount = 1;
        public const int ReservedReliableSignalCount = 1;

        public static void InitializeMultiConnection(ref ConnectionAPIs connectors, ref ConnectionMetaData connectionData, ConnectionState connectionState, ConnectionAPIs [] connections, ConnectionMetaData [] connectionDatas, ConnectionState [] toConnections)
        {
            connectors = new ConnectionAPIs();

            
            
            connectors.udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, connectionData.iListenToPort));
          
            
            Util.Exchange(ref  connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate);
            Util.Exchange(ref connectionState.udpReadStateName, StateOfConnection.ReadyToOperate);
            connectionState.isConnectionActive = true;

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
            fromStates[streamI].isConnectionActive = false;

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

        public async static Task<Tuple<ConnectionAPIs, ConnectionMetaData>> InitializeSingleConnection(ConnectionAPIs connectors, ConnectionMetaData connectionData, ConnectionState connectionState, ConnectionMetaData toServer)
        {
            connectors = new ConnectionAPIs();


            bool managedToOpen = false;
            int portTrials = 100;
            int startPort = connectionData.iListenToPort;
            for (int i = 0; i < portTrials && !managedToOpen; i++)
            {
                try
                {
                    connectors.udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, startPort + i));
                    connectionData.iListenToPort = startPort + i;
                    managedToOpen = true;
                } catch( SocketException e)
                {
                    Logging.Write("port not free or other error: " + e.Message);
                }
                
            }
            Logging.Write("managed to open at " + managedToOpen + " " + connectionData.iListenToPort);


            Util.Exchange(ref connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate);
            Util.Exchange(ref connectionState.udpReadStateName, StateOfConnection.ReadyToOperate);
            Logging.Write("client connects to udp" + new IPEndPoint(IPAddress.Any, connectionData.iListenToPort));

            connectors.tcpListener = null;

            connectors = await SetupClientTCP(connectors, connectionState, toServer);

            await ExchangeConnectionInitials(connectors, connectionData, connectionState);

            return new Tuple<ConnectionAPIs, ConnectionMetaData>(connectors, connectionData);
        }

        private static async Task ExchangeConnectionInitials(ConnectionAPIs connectors, ConnectionMetaData connectionData, ConnectionState connectionState)
        {
            //byte[] data = Encoding.ASCII.GetBytes(connectionData.listenPort.ToString());//only send the port we are listening to over udp
            Util.FlushBytes(connectionState.tcpWriteBytes);
            await MessageDeMultiplexer.MarkSignal(SignalType.TCPConnectionRequest, connectionState.tcpWriteBytes,
                async () =>
                {
                    DataPackage containingPort = new DataPackage();
                    containingPort.WriteConnectionRequest(connectionData.iListenToPort);

                    var usingBytes = connectionState.tcpWriteBytes;
                    Util.FlushBytes(usingBytes);
                    await MessageDeMultiplexer.MarkSignal(SignalType.TCPConnectionRequest, usingBytes, async () =>
                    {
                        SignalCompressor.Compress(containingPort, usingBytes, 1);



                        Logging.Write("client write my port");
                        await connectors.tcpStream.WriteAsync(usingBytes, 0, usingBytes.Length);
                        Logging.Write("client written");

                        string response = null;
                        Util.FlushBytes(connectionState.tcpReadBytes);
                        var byteCount = await connectors.tcpStream.ReadAsync(connectionState.tcpReadBytes, 0, connectionState.tcpReadBytes.Length);

                        await MessageDeMultiplexer.Divide(connectionState.tcpReadBytes, async () => { Logging.Write("handle float!? unexpected reply to client's tcp connection request"); },
                            async () =>
                            {
                                /*Logging.Write("client read client id from " + Encoding.ASCII.GetString(connectionState.tcpReadBytes, 0, byteCount));
                                response = Encoding.ASCII.GetString(connectionState.tcpReadBytes, 1, byteCount - 1);
                                var myClientID = int.Parse(response);*/

                                var containingClientId = SignalCompressor.DecompressDataPackage(connectionState.tcpReadBytes, 1);

                                connectionState.clientID = containingClientId.AsInt();
                                connectionState.isConnectionActive = connectionState.clientID == -1 ? false : true;
                                Logging.Write("i am client " + containingClientId.AsInt());
                            },
                            async () => { Logging.Write("handle tcp keepalive!? unexpected reply to client's tcp connection request"); });
                    });
                });
        }


        public static async void StartProcessTCPConnections( ConnectionAPIs by, ConnectionState byState, ConnectionAPIs[] storeToConnections, ConnectionMetaData[] storeToConnectionDatas,ConnectionState[] storeToConnectionStates,  Func<bool> cancel, Action report)
        {

            try
            {
                by.tcpListener.Start();
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

                                var package = SignalCompressor.DecompressDataPackage(byState.tcpReadBytes, 1);

                                //incoming connection, try to identify
                                IPEndPoint clientEndpoint;
                                int clientID;
                                IdentifyClient(package.AsInt(), storeToConnections,storeToConnectionStates,  connection, out clientEndpoint, out clientID);

                                if (clientID == -1)
                                {
                                    //sth went wrong
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

                                    Logging.Write("tcp received: " + package + " , will send back id " + clientID);

                                }

                                //respond to client in any case. if clientID == -1, the client knows that it cannot join the server

                                DataPackage containingClientId = new DataPackage();
                                containingClientId.WriteConnectionRequest(clientID);

                                var usingBytes = byState.tcpWriteBytes;
                                Util.FlushBytes(usingBytes);
                                await MessageDeMultiplexer.MarkSignal(SignalType.TCPConnectionRequest, usingBytes, async () =>
                                {
                                    SignalCompressor.Compress(containingClientId, usingBytes, 1);
                                    try
                                    {
                                        await stream.WriteAsync(usingBytes, 0, usingBytes.Length);
                                        storeToConnectionStates[clientID].isConnectionActive = clientID == -1 ? false : true;
                                    }
                                    catch (Exception e)
                                    {
                                        Logging.Write("StartProcessTCPConnections: tcp listener stream got closed, (unfortunately) this is intended behaviour, stop writing.");
                                    }

                                });

                                if(clientID != -1) {
                                    Util.Exchange(ref storeToConnectionStates[clientID].tcpWriteStateName, StateOfConnection.ReadyToOperate);
                                    Util.Exchange(ref storeToConnectionStates[clientID].tcpReadStateName, StateOfConnection.ReadyToOperate);
                                }
                                

                                Logging.Write("connections: ");
                                foreach(var c in storeToConnectionDatas)
                                {
                                    Logging.Write(c.ToString());
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



        public async static void PeriodicallySendKeepAlive(OutgoingSignal[][] reliable, OutgoingSignal[][] unreliable, IEnumerable<int> indices, Func<bool> cancel, TimeControl timeControl, int periodMs = 1000)
        {
            while(!cancel())
            {
                foreach (var ind in indices)
                {
                    reliable[ind][0].WriteTcpAlive();
                    unreliable[ind][0].WriteUdpAlive();
                }
                await Task.Delay(periodMs);
            }
            
        }
        

        public async static void DetectUdpComNotWorking(int clientId , IncomingSignal[][] reliable, IncomingSignal[][] unreliable, Func<bool> cancel, TimeControl timeControl, Action callBackUdpNotWorking, int periodMs = 1000)
        {
            int timesTimestampsDontMatch = 0;

            while (!cancel())
            {
                var reliableKeepAliveI = Util.findLatestHistIndex(reliable, 0);
                var unreliableKeepAliveI = Util.findLatestHistIndex(unreliable, 0);

                Logging.Write("ms between keepalives tcp and upd: " +  reliable[reliableKeepAliveI][0].data.timeStamp  + " , " 
                    + unreliable[unreliableKeepAliveI][0].data.timeStamp);
                
                if((reliable[reliableKeepAliveI][0].data.timeStamp - unreliable[unreliableKeepAliveI][0].data.timeStamp).TotalMilliseconds > 1000)
                {
                    Logging.Write("dont match " + timesTimestampsDontMatch);
                    timesTimestampsDontMatch++;
                }
                else {
                    timesTimestampsDontMatch--;
                    timesTimestampsDontMatch = Math.Max(0, timesTimestampsDontMatch);
                }

                //we need to do sth!
                if(timesTimestampsDontMatch > 3)
                {
                    callBackUdpNotWorking();

                    break;
                }
                
                await Task.Delay(periodMs);
            }


        }

     

        private static void IdentifyClient(int clientHintsUdpPort, ConnectionAPIs[] storeToConnections,ConnectionState [] storeToConnectionStates,  TcpClient connection, out IPEndPoint clientEndpoint, out int clientID)
        {
            //clientEndpoint = (IPEndPoint)connection.Client.RemoteEndPoint;
            //var splitIPAndPort = fromTCPMessage.Split('|');
            var connnectedToIPAddress = ((IPEndPoint)connection.Client.RemoteEndPoint).Address;
            //var dataContainingListenPort = ((IPEndPoint)connection.Client.RemoteEndPoint).Port;
            var clientListensToUdpPort = clientHintsUdpPort;
            
            clientEndpoint = new IPEndPoint(connnectedToIPAddress, clientListensToUdpPort);

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
