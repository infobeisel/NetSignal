using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;


namespace NetSignal
{

    public class NetSignalStarter
    {
        public async static Task<Tuple<ConnectionAPIs, ConnectionMetaData, ConnectionMapping>> StartServer(bool shouldLog,
            ConnectionAPIs[] serverConnection, ConnectionMetaData[] serverData, ConnectionState[] serverState, Func<bool> cancel, ConnectionMapping connectionMapping, ConnectionAPIs[] connections,
            ConnectionMetaData[] connectionDatas, ConnectionState[] connectionStates,
            OutgoingSignal[][] unreliableOutgoingSignals, IncomingSignal[][] unreliableIncomingSignals,
            OutgoingSignal[][] reliableOutgoingSignals, IncomingSignal[][] reliableIncomingSignals)
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

            _ = Task.Run(() =>
            {
                UnreliableSignalUpdater.ReceiveSignals(serverConnection[0], serverData[0], serverState[0], unreliableIncomingSignals, cancel,
                (string r) => { if (shouldLog) Logging.Write("server receive: " + r); }, connectionDatas);
            });

            ReliableSignalUpdater.ReceiveSignalsReliablyFromAll(reliableIncomingSignals, cancel, (string s) => { }, connections, connectionDatas, connectionStates);

            

            Logging.Write("StartServer: start accept tcp connections");
            ConnectionUpdater.StartProcessTCPConnections(connectionMapping, serverConnection[0], serverState[0], connections, connectionDatas, connectionStates, cancel, () => { });

            Logging.Write("StartServer: start sync signals");
            
            UnreliableSignalUpdater.SyncSignalsToAll(serverConnection[0], serverData[0], serverState[0], unreliableOutgoingSignals,
            (string r) => { if (shouldLog) Logging.Write("server send: " + r); }, cancel, connections, connectionDatas, connectionStates);
            
            ReliableSignalUpdater.SyncSignalsToAllReliably(reliableOutgoingSignals, cancel,connections, connectionDatas, connectionStates);

            ConnectionUpdater.AwaitAndPerformTearDownClientUDP(serverConnection[0], cancel, serverState[0]);
            ConnectionUpdater.AwaitAndPerformTearDownTCPListenerAndUdpToClients(serverConnection[0], cancel, serverState[0], connections, connectionStates, connectionDatas, connectionMapping);

            _ = Task.Run(() =>
            {
                SignalUpdaterUtil.SyncIncomingToOutgoingSignals(unreliableIncomingSignals, unreliableOutgoingSignals, cancel);
            });

            _ = Task.Run(() =>
            {
                SignalUpdaterUtil.SyncIncomingToOutgoingSignals(reliableIncomingSignals, reliableOutgoingSignals, cancel);
            });

            return new Tuple<ConnectionAPIs, ConnectionMetaData, ConnectionMapping>(serverConnection[0], serverData[0], connectionMapping);
        }

        //please provide array with one element for server*
        public async static Task<Tuple<ConnectionAPIs,ConnectionMetaData>> StartClient(Func<bool> shouldReport,  int clientIndex, ConnectionAPIs [] clientCon, ConnectionMetaData [] clientData, ConnectionState [] clientState, 
             ConnectionMetaData [] serverData, 
            Func<bool> cancel,
            OutgoingSignal[][] unreliableOutgoingSignals, IncomingSignal[][] unreliableIncomingSignals,
            OutgoingSignal[][] reliableOutgoingSignals, IncomingSignal[][] reliableIncomingSignals)
        {

            
            
            try
            {
                Logging.Write("StartClient: init single connection");
                var returnTuple = await ConnectionUpdater.InitializeSingleConnection(clientCon[clientIndex], clientData[clientIndex], clientState[clientIndex], serverData[0]);
                clientCon[clientIndex] = returnTuple.Item1;
                clientData[clientIndex] = returnTuple.Item2;

            } catch (SocketException e)
            {
                Logging.Write("couldnt establish connection: " + e.Message);
            }

            //successfully connected
            if (StateOfConnection.Uninitialized != (StateOfConnection)clientState[clientIndex].tcpReadStateName)
            {
                Logging.Write("StartClient: start receive signals");


                _ = Task.Run(() =>
                {
                    UnreliableSignalUpdater.ReceiveSignals(clientCon[clientIndex], clientData[clientIndex], clientState[clientIndex], unreliableIncomingSignals, cancel,
                    (string r) => { if (shouldReport()) Logging.Write("client " + clientIndex + " receive: " + r); });
                });
                
                ReliableSignalUpdater.ReceiveSignalsReliablyFromAll(reliableIncomingSignals, cancel, (string s) => { }, 
                    new[] { clientCon[clientIndex] }, new[] { clientData[clientIndex] }, new[] { clientState[clientIndex] });

                Logging.Write("StartClient: start sync signals to server");

                
                UnreliableSignalUpdater.SyncSignalsToAll(clientCon[clientIndex], clientData[clientIndex], clientState[clientIndex], unreliableOutgoingSignals,
                (string r) => { if (shouldReport()) Logging.Write("client " + clientIndex + " send: " + r); }, cancel, null, serverData, null);

                ReliableSignalUpdater.SyncSignalsToAllReliably(reliableOutgoingSignals, cancel, clientCon, clientData, clientState);

                _ = Task.Run(() =>
                {
                    ConnectionUpdater.PeriodicallySendKeepAlive(clientCon[clientIndex], clientData[clientIndex], clientState[clientIndex], serverData,
                    (string r) => { if (shouldReport()) Logging.Write("client " + clientIndex + " send: " + r); }, cancel);
                });

                ConnectionUpdater.AwaitAndPerformTearDownClientTCP(clientCon[clientIndex], cancel, clientState[clientIndex]);
                ConnectionUpdater.AwaitAndPerformTearDownClientUDP(clientCon[clientIndex], cancel, clientState[clientIndex]);
            }

            return new Tuple<ConnectionAPIs, ConnectionMetaData>(clientCon[clientIndex], clientData[clientIndex]);
        }

        public async static void TestMatchMaking()
        {
            var teard = false;
            var con = new ConnectionAPIs();
            var d = new ConnectionMetaData();
            var s = new ConnectionState();
            d.matchmakingServerPort = 5432;
            d.matchmakingServerIp = "http://127.0.0.1";

            MatchmakingConnectionUpdater.InitializeMatchMakingServer(ref con, ref d, ref s, () => teard);

            await Task.Delay(1000);

            var clientCon = new ConnectionAPIs();
            var clientD = new ConnectionMetaData();
            var clientS = new ConnectionState();
            clientD.matchmakingServerPort = 5432;
            clientD.matchmakingServerIp = "http://127.0.0.1";
            MatchmakingConnectionUpdater.ServerList l = new MatchmakingConnectionUpdater.ServerList();
            l.list = new List<MatchmakingConnectionUpdater.ServerListElementResponse>();
            MatchmakingConnectionUpdater.InitializeMatchMakingClient(ref clientCon, ref clientD, ref clientS, () => teard);
            
            l = await MatchmakingConnectionUpdater.GatherServerList(clientCon, clientD, clientS);

            await Task.Delay(5555);
            teard = true;
        }


        public async static void TestMatchMakingClientToSetUpRemoteServer()
        {

            bool teard = false;
            var clientCon = new ConnectionAPIs();
            var clientD = new ConnectionMetaData();
            var clientS = new ConnectionState();
            clientD.matchmakingServerPort = 5432;
            clientD.matchmakingServerIp = "http://85.214.239.45";
            MatchmakingConnectionUpdater.ServerList l = new MatchmakingConnectionUpdater.ServerList();
            l.list = new List<MatchmakingConnectionUpdater.ServerListElementResponse>();
            MatchmakingConnectionUpdater.InitializeMatchMakingClient(ref clientCon, ref clientD, ref clientS, () => teard);

            l = await MatchmakingConnectionUpdater.GatherServerList(clientCon, clientD, clientS);

            Logging.Write(l.ToString());

            await Task.Delay(5555);
            teard = true;
        }

        public async static Task Test()
        {
          
            var cancel = false;
            var shouldPrint = false;

            int clientCount = 4;

            ConnectionMetaData[] connectionMetaDatasSeenFromServer = new ConnectionMetaData[clientCount];
            ConnectionAPIs[] connectionApisSeenFromServer = new ConnectionAPIs[clientCount];
            ConnectionState[] connectionStatesSeenFromServer = new ConnectionState[clientCount];
            
            


            //only for symmetry, this is always supposed to be an array of size one, 
            ConnectionAPIs [] server = new ConnectionAPIs[1] {new ConnectionAPIs() };
            ConnectionMetaData [] serverData = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState [] serverState = new ConnectionState[1] { new ConnectionState() };
            ConnectionMapping mapping = new ConnectionMapping();

            serverData[0].iListenToPort = 5000;
            serverData[0].myIp = "127.0.0.1";
            //serverData[0].myIp = "85.214.239.45";

            //this can and will be array of size N
            ConnectionAPIs [] clients = new ConnectionAPIs[clientCount] ;
            ConnectionMetaData [] clientDatas = new ConnectionMetaData[clientCount] ;
            ConnectionState [] clientState = new ConnectionState[clientCount];

            for (int i = 0; i < clientCount; i++)
            {
                clients[i] = new ConnectionAPIs();
                clientDatas[i] = new ConnectionMetaData();
                clientState[i] = new ConnectionState();
                clientDatas[i].iListenToPort = 5000 + 1 + i;
                clientDatas[i].myIp = "127.0.0.1";
            }

            
            await NetSignalStarter.TestDuplex(() => cancel, () => shouldPrint, 
                connectionApisSeenFromServer, connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer,
                server, serverData, serverState, mapping, clients, clientDatas, clientState);
                

           

            /*await TestClientsToRemoteDedicatedServer(() => cancel, () => shouldPrint,
                server, serverData, serverState, clients, clientDatas, clientState);*/
             

            cancel = true;
            //TODOS:
            //move from synchronous to ASYNC send and receive. (CHECK)
            //do not always open and close new tcp connection? 
            // - close tcp listener and clients on server side (check)
            // - close tcp client on client side (check)
            // - also close udp on client and server side if necessary (check)
            //implement sync and receive signals RELIABLE version (over tcp) (CHECK)
            //implement websocket for matchmaking (to find ip to connect to server), set up with strato !!
            // - partial check, set up matchmaking server on strato and test initial server list request
            // - implement security features (https)
            //TODO go on here
            /*
             * matchmaking server listens to http, following endpoints:
             * x dedicated server register (will be done manually)
             * - dedicated server update free slots and keepalive
             * - client ask for server list (paged subset) (partial check)
             * 
            matchmakingServerData.matchmakingServerIp = "127.0.0.1";
            matchmakingServerData.matchmakingServerPort = 80;
            */
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


        
        public async static Task TestClientsToRemoteDedicatedServer(
        Func<bool> cancel,
        Func<bool> shouldReport,
     
        ConnectionAPIs[] serverInstanceAPI,
        ConnectionMetaData[] serverInstanceData,
        ConnectionState[] serverInstanceState,
        ConnectionAPIs[] clientInstancesAPI,
        ConnectionMetaData[] clientInstancesData,
        ConnectionState[] clientInstancesState
        )
        {

            //every client has representation of other client's signals -> 3D

            IncomingSignal[][][] clientUnreliableIncoming = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] clientUnreliableOutgoing = new OutgoingSignal[clientInstancesAPI.Length][][];

            IncomingSignal[][][] clientReliableIncoming = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] clientReliableOutgoing = new OutgoingSignal[clientInstancesAPI.Length][][];

            for (int clientI = 0; clientI < clientInstancesAPI.Length; clientI++)
            {
                clientUnreliableIncoming[clientI] = new IncomingSignal[clientInstancesAPI.Length][];
                clientReliableIncoming[clientI] = new IncomingSignal[clientInstancesAPI.Length][];
                clientUnreliableOutgoing[clientI] = new OutgoingSignal[clientInstancesAPI.Length][];
                clientReliableOutgoing[clientI] = new OutgoingSignal[clientInstancesAPI.Length][];

                for (int otherClientI = 0; otherClientI < clientInstancesAPI.Length; otherClientI++)
                {
                    clientUnreliableIncoming[clientI][otherClientI] = new IncomingSignal[5];
                    clientReliableIncoming[clientI][otherClientI] = new IncomingSignal[5];
                    clientUnreliableOutgoing[clientI][otherClientI] = new OutgoingSignal[5];
                    clientReliableOutgoing[clientI][otherClientI] = new OutgoingSignal[5];
                }



                var updatedTuple = await StartClient(shouldReport, clientI, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData, cancel,
                clientUnreliableOutgoing[clientI], clientUnreliableIncoming[clientI],
                clientReliableOutgoing[clientI], clientReliableIncoming[clientI]);
                clientInstancesAPI[clientI] = updatedTuple.Item1;
                clientInstancesData[clientI] = updatedTuple.Item2;

            }
            await Task.Delay(1000);
            await SyncLogCheckWithPlayer0And1(clientUnreliableIncoming, clientUnreliableOutgoing);

        }

        private static async Task SyncLogCheckWithPlayer0And1(IncomingSignal[][][] clientIncoming, OutgoingSignal[][][] clientOutgoing)
        {
            bool wasSame = false;


            var rng = new System.Random(645);

            var avgPing = 0.0;
            for (int i = 0; i < 1000; i++)
            {

                wasSame = clientOutgoing[0][0][0].Equals(clientIncoming[1][0][0]);

                if (wasSame)
                {
                    //Logging.Write(clientUnreliableOutgoing[0][0][0] + " vs " + clientUnreliableIncoming[1][0][0].ToString());
                    avgPing += (clientIncoming[1][0][0].cameIn - clientIncoming[1][0][0].data.timeStamp).TotalMilliseconds;
                }


                if (wasSame)
                {
                    var a = new FloatDataPackage();
                    a.data = (float)rng.NextDouble();
                    a.clientId = 0;
                    a.index = 0;
                    a.timeStamp = DateTime.UtcNow;
                    clientOutgoing[0][0][0].data = a;
                }

                await Task.Delay(40);

                if(i % 10 == 0)
                {
                    Logging.Write("avg ping: " + (avgPing / 10.0).ToString("000.000") + " ms");
                    avgPing = 0.0;
                }
            }
        }

        public async static Task TestDuplex(
        Func<bool> cancel,
        Func<bool> shouldReport,
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


            IncomingSignal[][] unreliableSignalsSeenFromServer = new IncomingSignal[clientInstancesAPI.Length][];
            OutgoingSignal[][] unreliableSignalsSentFromServer = new OutgoingSignal[clientInstancesAPI.Length][];

            IncomingSignal[][] reliableSignalsSeenFromServer = new IncomingSignal[clientInstancesAPI.Length][];
            OutgoingSignal[][] reliableSignalsSentFromServer = new OutgoingSignal[clientInstancesAPI.Length][];

            for (int i = 0; i < clientInstancesAPI.Length; i++)
            {
                unreliableSignalsSeenFromServer[i] = new IncomingSignal[5];
                reliableSignalsSeenFromServer[i] = new IncomingSignal[5];
                unreliableSignalsSentFromServer[i] = new OutgoingSignal[5];
                reliableSignalsSentFromServer[i] = new OutgoingSignal[5];
            
            }


            Logging.Write("TestDuplex: start server");
            //TODO server needs to have [clientInstancesAPI.Length, 5] signals!
            var updatedServerTuple = await StartServer(shouldReport(), serverInstanceAPI, serverInstanceData, serverInstanceState, cancel, mapping, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer,
                clientConnectionStatesSeenFromServer, 
                unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer);
            serverInstanceAPI[0] = updatedServerTuple.Item1;
            serverInstanceData[0] = updatedServerTuple.Item2;
            mapping = updatedServerTuple.Item3;

            await Task.Delay(1000);
            Logging.Write("TestDuplex: start clients");


            //every client has representation of other client's signals -> 3D

            IncomingSignal[][][] clientUnreliableIncoming = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] clientUnreliableOutgoing = new OutgoingSignal[clientInstancesAPI.Length][][];

            IncomingSignal[][][] clientReliableIncoming = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] clientReliableOutgoing = new OutgoingSignal[clientInstancesAPI.Length][][];

            for (int clientI = 0; clientI < clientInstancesAPI.Length; clientI++)
            {
                clientUnreliableIncoming[clientI] = new IncomingSignal[clientInstancesAPI.Length][];
                clientReliableIncoming[clientI] = new IncomingSignal[clientInstancesAPI.Length][];
                clientUnreliableOutgoing[clientI] = new OutgoingSignal[clientInstancesAPI.Length][];
                clientReliableOutgoing[clientI] = new OutgoingSignal[clientInstancesAPI.Length][];

                for (int otherClientI = 0; otherClientI < clientInstancesAPI.Length; otherClientI++)
                {
                    clientUnreliableIncoming[clientI][otherClientI] = new IncomingSignal[5];
                    clientReliableIncoming[clientI][otherClientI] = new IncomingSignal[5];
                    clientUnreliableOutgoing[clientI][otherClientI] = new OutgoingSignal[5];
                    clientReliableOutgoing[clientI][otherClientI] = new OutgoingSignal[5];
                }



                var updatedTuple = await StartClient(shouldReport, clientI, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData, cancel,
                clientUnreliableOutgoing[clientI], clientUnreliableIncoming[clientI],
                clientReliableOutgoing[clientI], clientReliableIncoming[clientI]);
                clientInstancesAPI[clientI] = updatedTuple.Item1;
                clientInstancesData[clientI] = updatedTuple.Item2;

            }
            await Task.Delay(1000);

            
            await SyncLogCheckWithPlayer0And1(clientReliableIncoming, clientReliableOutgoing);
            /*
            var rng = new System.Random(645);
            for (int i = 0; i < 1000; i++)
            {
                Console.Clear();

                for (int clientId = 0; clientId < clientInstancesAPI.Length; clientId++)
                {
                    LogSignals("client" + 0, "client" + clientId, clientUnreliableOutgoing[0][0], clientUnreliableIncoming[clientId][0]);

                }


                var a = new FloatDataPackage();
                a.data = (float)rng.NextDouble();
                a.clientId = 0;
                a.index = 0;
                a.timeStamp = DateTime.UtcNow;
                clientUnreliableOutgoing[0][0][0].data = a;

                
                await Task.Delay(100);
            }*/

        }


        private static void LogSignals(string outName, string inName, OutgoingSignal[] outs, IncomingSignal[] ins)
        {
            Logging.Write(inName);
            foreach (var incoming in ins)
            {
                Console.Write(incoming.ToString() + "|");
            }

            Logging.Write("");

            Logging.Write(outName);
            foreach (var outgoing in outs)
            {
                Console.Write(outgoing.ToString() + "|");
            }
            Logging.Write("");

            for(int i = 0; i < ins.Length; i++)
            {
                Logging.Write("Test Above: " + (outs[i].Equals(ins[i])));
            }
            
        }

        private static void LogServerToClientCommunication(OutgoingSignal[] signalsSentFromServer, IncomingSignal[] signalsSeenFromClient0)
        {
            Logging.Write("OUTGOING SERVER DATA");
            foreach (var outgoing in signalsSentFromServer)
            {
                Console.Write(outgoing.ToString() + "|");
            }
            Logging.Write("");

            Logging.Write("INCOMING CLIENT DATA");
            foreach (var incoming in signalsSeenFromClient0)
            {
                Console.Write(incoming.ToString() + "|");
            }
            Logging.Write("");

            for (int i = 0; i < signalsSentFromServer.Length; i++)
            {
                Logging.Write("Test Above: " + (signalsSentFromServer[i].Equals(signalsSeenFromClient0[i])));
            }
        }
    }
}
