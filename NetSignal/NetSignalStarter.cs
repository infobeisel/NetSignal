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
            UnreliableSignalUpdater.ReceiveSignals(serverConnection[0], serverData[0], serverState[0], unreliableIncomingSignals, cancel, (string r) => { if (shouldLog) Logging.Write("server receive: " + r); }, connectionDatas);

            ReliableSignalUpdater.ReceiveSignalsReliably(reliableIncomingSignals, cancel, (string s) => { }, connections, connectionDatas, connectionStates);

            

            Logging.Write("StartServer: start accept tcp connections");
            //ConnectionUpdater.StartThreadAcceptTCPConnections(connectionMapping, serverConnection, connections, connectionDatas, cancel, () => { });
            ConnectionUpdater.StartProcessTCPConnections(connectionMapping, serverConnection[0], serverState[0], connections, connectionDatas, connectionStates, cancel, () => { });

            Logging.Write("StartServer: start sync signals");
            //SignalUpdater.StartThreadSyncSignalsToAll(serverConnection, outgoingSignals, cancel, connectionDatas);
            UnreliableSignalUpdater.SyncSignalsToAll(serverConnection[0], serverData[0], serverState[0], unreliableOutgoingSignals,(string r) => { if (shouldLog) Logging.Write("server send: " + r); },  cancel, connectionDatas);

            ReliableSignalUpdater.SyncSignalsToAllReliably(reliableOutgoingSignals, cancel,connections, connectionDatas, connectionStates);

            ConnectionUpdater.AwaitAndPerformTearDownClientUDP(serverConnection[0], cancel, serverState[0]);
            ConnectionUpdater.AwaitAndPerformTearDownTCPListener(serverConnection[0], cancel, serverState[0], connections, connectionStates, connectionDatas, connectionMapping);

            UnreliableSignalUpdater.SyncIncomingToOutgoingSignals(unreliableIncomingSignals, unreliableOutgoingSignals, cancel);
            UnreliableSignalUpdater.SyncIncomingToOutgoingSignals(reliableIncomingSignals, reliableOutgoingSignals, cancel);

            return new Tuple<ConnectionAPIs, ConnectionMetaData, ConnectionMapping>(serverConnection[0], serverData[0], connectionMapping);
        }

        //please provide array with one element for server*
        public async static Task<Tuple<ConnectionAPIs,ConnectionMetaData>> StartClient(int clientIndex, ConnectionAPIs [] clientCon, ConnectionMetaData [] clientData, ConnectionState [] clientState, 
             ConnectionMetaData [] serverData, 
            Func<bool> cancel,
            OutgoingSignal[][] unreliableOutgoingSignals, IncomingSignal[][] unreliableIncomingSignals,
            OutgoingSignal[][] reliableOutgoingSignals, IncomingSignal[][] reliableIncomingSignals)
        {

            bool shouldReport = false;
            
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
                
                UnreliableSignalUpdater.ReceiveSignals(clientCon[clientIndex], clientData[clientIndex], clientState[clientIndex], unreliableIncomingSignals, cancel,
                    (string r) => { if (shouldReport) Logging.Write("client receive: " + r); });


                ReliableSignalUpdater.ReceiveSignalsReliably(reliableIncomingSignals, cancel, (string s) => { }, 
                     new[] { clientCon[clientIndex] }, new[] { clientData[clientIndex] }, new[] { clientState[clientIndex] });

                Logging.Write("StartClient: start sync signals to server");
                
                UnreliableSignalUpdater.SyncSignalsToAll(clientCon[clientIndex], clientData[clientIndex], clientState[clientIndex], unreliableOutgoingSignals,
                    (string r) => { if (shouldReport) Logging.Write("client send: " + r); }, cancel, serverData);

                ReliableSignalUpdater.SyncSignalsToAllReliably(reliableOutgoingSignals, cancel, clientCon, clientData, clientState);

                ConnectionUpdater.PeriodicallySendKeepAlive(clientCon[clientIndex], clientData[clientIndex], clientState[clientIndex], serverData,
                    (string r) => { if (shouldReport) Logging.Write("client send: " + r); }, cancel);

                ConnectionUpdater.AwaitAndPerformTearDownClientTCP(clientCon[clientIndex], cancel, clientState[clientIndex]);
                ConnectionUpdater.AwaitAndPerformTearDownClientUDP(clientCon[clientIndex], cancel, clientState[clientIndex]);
            }

            return new Tuple<ConnectionAPIs, ConnectionMetaData>(clientCon[clientIndex], clientData[clientIndex]);
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
            ConnectionMetaData[] connectionMetaDatasSeenFromServer = new ConnectionMetaData[3];
            ConnectionAPIs[] connectionApisSeenFromServer = new ConnectionAPIs[3];
            ConnectionState[] connectionStatesSeenFromServer = new ConnectionState[3];


            //only for symmetry, this is always supposed to be an array of size one, 
            ConnectionAPIs [] server = new ConnectionAPIs[1] {new ConnectionAPIs() };
            ConnectionMetaData [] serverData = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState [] serverState = new ConnectionState[1] { new ConnectionState() };
            ConnectionMapping mapping = new ConnectionMapping();

            //this can and will be array of size N
            ConnectionAPIs [] clients = new ConnectionAPIs[3] { new ConnectionAPIs() , new ConnectionAPIs() , new ConnectionAPIs() };
            ConnectionMetaData [] clientDatas = new ConnectionMetaData[3] { new ConnectionMetaData() , new ConnectionMetaData() , new ConnectionMetaData() };
            ConnectionState [] clientState = new ConnectionState[3] { new ConnectionState() , new ConnectionState() , new ConnectionState() };

            

            /*serverData[0].iListenToPort = 5000;
            serverData[0].myIp = "127.0.0.1";
            clientDatas[0].iListenToPort = 5001;
            clientDatas[0].myIp = "127.0.0.1";
            clientDatas[1].iListenToPort = 5002;
            clientDatas[1].myIp = "127.0.0.1";
            clientDatas[2].iListenToPort = 5003;
            clientDatas[2].myIp = "127.0.0.1";
            await NetSignalStarter.TestDuplex(() => cancel, () => shouldPrint, 
                
                connectionApisSeenFromServer, connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer,
                server, serverData, serverState, mapping, clients, clientDatas, clientState);*/
                

            
            serverData[0].iListenToPort = 5000;
            serverData[0].myIp = "85.214.239.45";
            clientDatas[0].iListenToPort = 5001;
            clientDatas[0].myIp = "127.0.0.1";
            clientDatas[1].iListenToPort = 5002;
            clientDatas[1].myIp = "127.0.0.1";
            clientDatas[2].iListenToPort = 5003;
            clientDatas[2].myIp = "127.0.0.1";

            await TestClientsToRemoteDedicatedServer(() => cancel, () => shouldPrint,
                server, serverData, serverState, clients, clientDatas, clientState);
                

            cancel = true;
            //TODOS:
            //move from synchronous to ASYNC send and receive. (CHECK)
            //do not always open and close new tcp connection? 
            // - close tcp listener and clients on server side (check)
            // - close tcp client on client side (check)
            // - also close udp on client and server side if necessary (check)
            //implement sync and receive signals RELIABLE version (over tcp) (CHECK)
            //implement websocket for matchmaking (to find ip to connect to server), set up with strato (?) 
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



                var updatedTuple = await StartClient(clientI, clientInstancesAPI, clientInstancesData, clientInstancesState,  serverInstanceData, cancel,
                clientUnreliableOutgoing[clientI], clientUnreliableIncoming[clientI],
                clientReliableOutgoing[clientI], clientReliableIncoming[clientI]);
                clientInstancesAPI[clientI] = updatedTuple.Item1;
                clientInstancesData[clientI] = updatedTuple.Item2;

            }
            await Task.Delay(1000);



            var rng = new System.Random(645);
            for (int i = 0; i < 1000; i++)
            {
                Console.Clear();


                LogSignals("client0", "client0", clientUnreliableOutgoing[0][0], clientUnreliableIncoming[0][0]);
                LogSignals("client1", "client1", clientUnreliableOutgoing[0][0], clientUnreliableIncoming[0][0]);
                LogSignals("client2", "client2", clientUnreliableOutgoing[0][0], clientUnreliableIncoming[0][0]);

                var a = new FloatDataPackage();
                a.data = (float)rng.NextDouble();
                a.clientId = 0;
                a.index = 0;
                a.timeStamp = DateTime.UtcNow;
                clientUnreliableOutgoing[0][0][0].data = a;

                a.clientId = 1;
                a.index = 1;
                a.data = (float)rng.NextDouble();
                clientUnreliableOutgoing[1][1][0].data = a;

                a.clientId = 2;
                a.index = 2;
                a.data = (float)rng.NextDouble();
                clientUnreliableOutgoing[2][2][0].data = a;

                await Task.Delay(100);
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
            var updatedServerTuple = await StartServer(false, serverInstanceAPI, serverInstanceData, serverInstanceState, cancel, mapping, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer,
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



                var updatedTuple = await StartClient(clientI, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData, cancel,
                clientUnreliableOutgoing[clientI], clientUnreliableIncoming[clientI],
                clientReliableOutgoing[clientI], clientReliableIncoming[clientI]);
                clientInstancesAPI[clientI] = updatedTuple.Item1;
                clientInstancesData[clientI] = updatedTuple.Item2;

            }
            await Task.Delay(1000);



            //Server and Client are not listening/sending to the right endpoint?!
            //server listens to Ip.any, 3000 , initialized with UdpClient(...), whereas client doesnt listen, is init with UdpClient() and then .Connect(...) . what exactly is the differnece?

            if (!cancel())
            {
                //if(shouldReport())
                {
                    LogSignals("client0", "client1", clientUnreliableOutgoing[0][0], clientUnreliableIncoming[1][0]);
                }
                await Task.Delay(1000);
            }


            var rng = new System.Random(645);
            for (int i  = 0; i < 1000; i++)
            {
                Console.Clear();


                LogSignals("client0", "client0", clientUnreliableOutgoing[0][0], clientUnreliableIncoming[0][0]);
                LogSignals("client1", "client1", clientUnreliableOutgoing[0][0], clientUnreliableIncoming[0][0]);
                LogSignals("client2", "client2", clientUnreliableOutgoing[0][0], clientUnreliableIncoming[0][0]);

                var a = new FloatDataPackage();
                a.data = (float)rng.NextDouble();
                a.clientId = 0;
                a.index = 0;
                a.timeStamp = DateTime.UtcNow;
                clientUnreliableOutgoing[0][0][0].data = a;
                
                a.clientId = 1;
                a.index = 1;
                a.data = (float)rng.NextDouble();
                clientUnreliableOutgoing[1][1][0].data = a;
                
                a.clientId = 2;
                a.index = 2;
                a.data = (float)rng.NextDouble();
                clientUnreliableOutgoing[2][2][0].data = a;

                await Task.Delay(100);
            }
            
            if (!cancel())
            {
                LogSignals("client0", "client1", clientUnreliableOutgoing[0][0], clientUnreliableIncoming[1][0]);

                LogSignals("client0", "client2", clientUnreliableOutgoing[0][0], clientUnreliableIncoming[2][0]);
                
                await Task.Delay(1000);
            }
            
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
