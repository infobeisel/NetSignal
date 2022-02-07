using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace NetSignal
{
    public class NetSignalTests
    {

        public async static Task Test()
        {

            var cancel = false;
            var shouldPrint = false;

            int clientCount = 4;

            ConnectionMetaData[] connectionMetaDatasSeenFromServer = new ConnectionMetaData[clientCount];
            ConnectionAPIs[] connectionApisSeenFromServer = new ConnectionAPIs[clientCount];
            ConnectionState[] connectionStatesSeenFromServer = new ConnectionState[clientCount];




            //only for symmetry, this is always supposed to be an array of size one, 
            ConnectionAPIs[] server = new ConnectionAPIs[1] { new ConnectionAPIs() };
            ConnectionMetaData[] serverData = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState[] serverState = new ConnectionState[1] { new ConnectionState() };

            serverData[0].iListenToPort = 5000;
            serverData[0].myIp = "127.0.0.1";
            //serverData[0].myIp = "85.214.239.45";
            serverData[0].matchmakingServerIp = "http://127.0.0.1";
            serverData[0].matchmakingServerPort = 5432;

            //this can and will be array of size N
            ConnectionAPIs[] clients = new ConnectionAPIs[clientCount];
            ConnectionMetaData[] clientDatas = new ConnectionMetaData[clientCount];
            ConnectionState[] clientState = new ConnectionState[clientCount];

            for (int i = 0; i < clientCount; i++)
            {
                clients[i] = new ConnectionAPIs();
                clientDatas[i] = new ConnectionMetaData();
                clientState[i] = new ConnectionState();
                clientDatas[i].iListenToPort = 5000 + 1 + i;
                clientDatas[i].myIp = "127.0.0.1";
            }


            await TestDuplex(() => cancel, () => shouldPrint,
                connectionApisSeenFromServer, connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer,
                server, serverData, serverState,  clients, clientDatas, clientState);



            /*
            await TestClientsToRemoteDedicatedServer(() => cancel, () => shouldPrint,
                server, serverData, serverState, clients, clientDatas, clientState);*/
            cancel = true;
            //TODOS:
            //implement websocket for matchmaking (to find ip to connect to server), set up with strato !!
            // - partial check, set up matchmaking server on strato and test initial server list request
            // - implement security features (https) IMPORTANT
            /*
             * matchmaking server listens to http, following endpoints:
             * x dedicated server register (will be done manually)
             * - dedicated server update free slots and keepalive
             * - client ask for server list (paged subset) (partial check)
             * 
            */
            
            //battletest: make scriptable objects that have Incoming- and Outgoing Signals, write Mono Updaters that assign Signals to specific game objects (mainly: Bird slots, state enums for UI)

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
                    clientUnreliableIncoming[clientI][otherClientI] = SignalFactory.ConstructIncomingSignalArray(5);
                    clientReliableIncoming[clientI][otherClientI] = SignalFactory.ConstructIncomingSignalArray(5);
                    clientUnreliableOutgoing[clientI][otherClientI] = SignalFactory.ConstructOutgoingSignalArray(5);
                    clientReliableOutgoing[clientI][otherClientI] = SignalFactory.ConstructOutgoingSignalArray(5);
                }



                var updatedTuple = await NetSignalStarter.StartClient(shouldReport, clientI, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData, cancel,
                clientUnreliableOutgoing[clientI], clientUnreliableIncoming[clientI],
                clientReliableOutgoing[clientI], clientReliableIncoming[clientI]);
                clientInstancesAPI[clientI] = updatedTuple.Item1;
                clientInstancesData[clientI] = updatedTuple.Item2;

            }
            await Task.Delay(1000);
            //await SyncLogCheckWithPlayer0And1(clientUnreliableIncoming, clientUnreliableOutgoing);
            await SyncLogCheckWithPlayer0And1(clientReliableIncoming, clientReliableOutgoing);

        }

        private static async Task SyncLogCheckWithPlayer0And1(IncomingSignal[][][] clientIncoming, OutgoingSignal[][][] clientOutgoing)
        {
            bool wasSame = false;


            var rng = new System.Random(645);

            var avgPing = 0.0;
            for (int i = 0; i < 200; i++)
            {

                wasSame = clientOutgoing[0][0][0].Equals(clientIncoming[1][0][0]);

                if (wasSame)
                {
                    //Logging.Write(clientUnreliableOutgoing[0][0][0] + " vs " + clientUnreliableIncoming[1][0][0].ToString());
                    avgPing += (clientIncoming[1][0][0].cameIn - clientIncoming[1][0][0].data.timeStamp).TotalMilliseconds;
                }


                if (wasSame)
                {
                    var a = new DataPackage();
                    a.WriteFloat((float)rng.NextDouble());
                    a.clientId = 0;
                    a.index = 0;
                    a.timeStamp = DateTime.UtcNow;
                    clientOutgoing[0][0][0].data = a;
                }

                await Task.Delay(40);

                if (i % 10 == 0)
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
                unreliableSignalsSeenFromServer[i] = SignalFactory.ConstructIncomingSignalArray(5);
                reliableSignalsSeenFromServer[i] = SignalFactory.ConstructIncomingSignalArray(5);
                unreliableSignalsSentFromServer[i] = SignalFactory.ConstructOutgoingSignalArray(5);
                reliableSignalsSentFromServer[i] = SignalFactory.ConstructOutgoingSignalArray(5);

            }


            Logging.Write("TestDuplex: start server");
            //TODO server needs to have [clientInstancesAPI.Length, 5] signals!
            var updatedServerTuple = await NetSignalStarter.StartServer(shouldReport(), serverInstanceAPI, serverInstanceData, serverInstanceState, cancel, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer,
                clientConnectionStatesSeenFromServer,
                unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer);
            serverInstanceAPI[0] = updatedServerTuple.Item1;
            serverInstanceData[0] = updatedServerTuple.Item2;
            

            await Task.Delay(1000);
            Logging.Write("TestDuplex: start clients");


            //every client has representation of other client's signals -> 3D

            IncomingSignal[][][] clientUnreliableIncoming = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] clientUnreliableOutgoing = new OutgoingSignal[clientInstancesAPI.Length][][];

            IncomingSignal[][][] clientReliableIncoming = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] clientReliableOutgoing = new OutgoingSignal[clientInstancesAPI.Length][][];


            var cancelClient2 = false;
            Func<bool> cancelTestClient = () => cancelClient2;

            for (int clientI = 0; clientI < clientInstancesAPI.Length; clientI++)
            {
                clientUnreliableIncoming[clientI] = new IncomingSignal[clientInstancesAPI.Length][];
                clientReliableIncoming[clientI] = new IncomingSignal[clientInstancesAPI.Length][];
                clientUnreliableOutgoing[clientI] = new OutgoingSignal[clientInstancesAPI.Length][];
                clientReliableOutgoing[clientI] = new OutgoingSignal[clientInstancesAPI.Length][];

                for (int otherClientI = 0; otherClientI < clientInstancesAPI.Length; otherClientI++)
                {
                    clientUnreliableIncoming[clientI][otherClientI] = SignalFactory.ConstructIncomingSignalArray(5);
                    clientReliableIncoming[clientI][otherClientI] = SignalFactory.ConstructIncomingSignalArray(5);
                    clientUnreliableOutgoing[clientI][otherClientI] = SignalFactory.ConstructOutgoingSignalArray(5);
                    clientReliableOutgoing[clientI][otherClientI] = SignalFactory.ConstructOutgoingSignalArray(5);
                }



                


            }


            for (int clientI = 0; clientI < clientInstancesAPI.Length; clientI++)
            {
                var updatedClientTuple = await NetSignalStarter.StartClient(shouldReport, clientI, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData,
                    clientI == 1 ? cancelTestClient : cancel,
                clientUnreliableOutgoing[clientI], clientUnreliableIncoming[clientI],
                clientReliableOutgoing[clientI], clientReliableIncoming[clientI]);
                clientInstancesAPI[clientI] = updatedClientTuple.Item1;
                clientInstancesData[clientI] = updatedClientTuple.Item2;

            }
            await Task.Delay(1000);


            await SyncLogCheckWithPlayer0And1(clientReliableIncoming, clientReliableOutgoing);

            cancelClient2 = true;

            await Task.Delay(1000);

            await SyncLogCheckWithPlayer0And1(clientReliableIncoming, clientReliableOutgoing);

            await Task.Delay(1000);
            cancelClient2 = false ;
            await Task.Delay(1000);

            var updatedTuple = await NetSignalStarter.StartClient(shouldReport, 1, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData,
                    cancelTestClient ,
                clientUnreliableOutgoing[1], clientUnreliableIncoming[1],
                clientReliableOutgoing[1], clientReliableIncoming[1]);
            clientInstancesAPI[1] = updatedTuple.Item1;
            clientInstancesData[1] = updatedTuple.Item2;

            await Task.Delay(1000);

            await SyncLogCheckWithPlayer0And1(clientReliableIncoming, clientReliableOutgoing);

            await Task.Delay(1000);

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

            for (int i = 0; i < ins.Length; i++)
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
    }
}
