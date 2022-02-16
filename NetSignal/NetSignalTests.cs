using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace NetSignal
{
    public class NetSignalTests
    {

        public async static Task Test(params string [] args)
        {

            var cancel = false;
            var shouldPrint = false;

            int clientCount = 2;

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
            }


            TimeControl timeControl = new TimeControl(false, DateTime.UtcNow.Ticks, 60, 10);
            

            /*await TestDuplex(() => cancel, () => shouldPrint,
                 connectionApisSeenFromServer, connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer,
                 server, serverData, serverState,  clients, clientDatas, clientState);

            */


            /*await TestClientsToRemoteDedicatedServer(args.Length > 0 ? int.Parse(args[0])   : 5001, () => cancel, () => shouldPrint,
                  server, serverData, serverState, clients, clientDatas, clientState);*/
            /*await TestOneOfClientsToRemoteDedicatedServer(  args.Length > 0 ? int.Parse(args[0]) : 5001, () => cancel, () => shouldPrint,
                server, serverData, serverState, clients, clientDatas, clientState);*/
            /*await TestClientsToRemoteDedicatedServer(args.Length > 0 ? int.Parse(args[0]) : 5001, () => cancel, () => shouldPrint,
            server, serverData, serverState, clients, clientDatas, clientState);*/

            //args.Length > 0 ? int.Parse(args[0]) : 5001, 
            await TestDuplex(() => cancel, () => shouldPrint, timeControl, connectionApisSeenFromServer, connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer,
            server, serverData, serverState, clients, clientDatas, clientState);

            await Task.Delay(20000);

            cancel = true;
            //TODOS:
            //implement websocket for matchmaking (to find ip to connect to server), set up with strato !! (CHECK)
            // - partial check, set up matchmaking server on strato and test initial server list request(CHECK)
            // - implement security features (https) IMPORTANT
            /*
             * matchmaking server listens to http, following endpoints:
             * x dedicated server register (will be done manually)
             * - dedicated server update free slots and keepalive (partial check, misses free slots)
             * - client ask for server list (paged subset) (partial check)
             * 
             * discretized timestamp for signals
             * better compression: take multiple signals that have same timestamp 
             * delta compression: stop syncing signals when they dont change
             * protocol for player tries to join already full server
             * 
             *battletest: make scriptable objects that have Incoming- and Outgoing Signals, write Mono Updaters that assign Signals to specific game objects (mainly: Bird slots, state enums for UI) 
            */


        }



        public async static void TestMatchMakingClientToSetUpRemoteServer()
        {

            bool teard = false;
            var clientCon = new ConnectionAPIs();
            var clientD = new ConnectionMetaData();
            var clientS = new ConnectionState();
            clientD.matchmakingServerPort = 5432;
            clientD.matchmakingServerIp = "http://85.214.239.45";
            ServerList l = new ServerList();
            l.list = new List<ServerListElementResponse>();
            MatchmakingConnectionUpdater.InitializeMatchMakingClient(ref clientCon, ref clientD, ref clientS, () => teard);

            l = await MatchmakingConnectionUpdater.GatherServerList(clientCon, clientD, clientS);

            Logging.Write(l.ToString());

            await Task.Delay(5555);
            teard = true;
        }
        public async static Task TestOneOfClientsToRemoteDedicatedServer(
            int countUpFromPort,
        Func<bool> cancel,
        Func<bool> shouldReport,
        TimeControl timeControl,
        ConnectionAPIs[] serverInstanceAPI,
        ConnectionMetaData[] serverInstanceData,
        ConnectionState[] serverInstanceState,
        ConnectionAPIs[] clientInstancesAPI,
        ConnectionMetaData[] clientInstancesData,
        ConnectionState[] clientInstancesState
        )
        {

            //every client has representation of other client's signals -> 4D

            IncomingSignal[][][][] clientUnreliableIncoming, clientReliableIncoming;
            OutgoingSignal[][][][] clientUnreliableOutgoing, clientReliableOutgoing;
            ConstructSignals(timeControl, clientInstancesAPI, out clientUnreliableIncoming, out clientUnreliableOutgoing, out clientReliableIncoming, out clientReliableOutgoing);

            var clientId = await NetSignalStarter.StartClient(countUpFromPort , shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData, cancel);
            NetSignalStarter.StartClientSignalSyncing(clientId, shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState,
                clientUnreliableOutgoing[clientId], clientUnreliableIncoming[clientId],
            clientReliableOutgoing[clientId], clientReliableIncoming[clientId], cancel, serverInstanceData, timeControl);
            await SyncLogCheckWithPlayer0And1(clientUnreliableIncoming, clientUnreliableOutgoing, timeControl, clientId , clientId == 0 ? 1 : 0);
            //await SyncLogCheckWithPlayer0And1(clientInstancesData, clientReliableIncoming, clientReliableOutgoing);

        }


        public async static Task TestClientsToRemoteDedicatedServer(
            int countUpFromPort, 
        Func<bool> cancel,
        Func<bool> shouldReport,
        TimeControl timeControl,
        ConnectionAPIs[] serverInstanceAPI,
        ConnectionMetaData[] serverInstanceData,
        ConnectionState[] serverInstanceState,
        ConnectionAPIs[] clientInstancesAPI,
        ConnectionMetaData[] clientInstancesData,
        ConnectionState[] clientInstancesState
        )
        {

            //every client has representation of other client's signals -> 3D
            IncomingSignal[][][][] clientUnreliableIncoming, clientReliableIncoming;
            OutgoingSignal[][][][] clientUnreliableOutgoing, clientReliableOutgoing;
            ConstructSignals(timeControl, clientInstancesAPI, out clientUnreliableIncoming, out clientUnreliableOutgoing, out clientReliableIncoming, out clientReliableOutgoing);

            for(int clientInstI = 0; clientInstI < clientInstancesAPI.Length; clientInstI++)
            {
                var clientId = await NetSignalStarter.StartClient(countUpFromPort , shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData, cancel);
                    NetSignalStarter.StartClientSignalSyncing(clientId, shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState,
                        clientUnreliableOutgoing[clientId], clientUnreliableIncoming[clientId],
                    clientReliableOutgoing[clientId], clientReliableIncoming[clientId], cancel, serverInstanceData, timeControl);

            }

            //await SyncLogCheckWithPlayer0And1(clientUnreliableIncoming, clientUnreliableOutgoing);
            //await SyncLogCheckWithPlayer0And1(clientInstancesData, clientReliableIncoming, clientReliableOutgoing);

        }

        private static async Task SyncLogCheckWithPlayer0And1( IncomingSignal[][][][] clientIncoming, OutgoingSignal[][][][] clientOutgoing, TimeControl timeControl, int outgoingClientId = 0, int incomingClientId = 1)
        {
            bool wasSame = false;


            var rng = new System.Random(645);

            var avgPing = 0.0;
            for (int i = 0; i < 2000; i++)
            {

                var histIndex = SignalUpdaterUtil.CurrentHistoryIndex(timeControl);
                wasSame = clientOutgoing[outgoingClientId][outgoingClientId][histIndex][1].Equals(clientIncoming[incomingClientId][outgoingClientId][histIndex][1]);

                /*if (wasSame)
                {
                    avgPing += (clientIncoming[clientIdOfCon1][clientIdOfCon0][0].cameIn - clientIncoming[clientIdOfCon0][clientIdOfCon0][0].data.timeStamp).TotalMilliseconds;
                }*/


                /*if (wasSame)
                {*/
                if (i % 10 == 0)
                {
                    var a = new DataPackage();
                    a.WriteFloat((float)rng.NextDouble());
                    a.clientId = outgoingClientId;
                    a.index = 1;
                    a.timeStamp = DateTime.UtcNow;
                    clientOutgoing[outgoingClientId][outgoingClientId][histIndex][1].data = a;
                }
                //}

                await Task.Delay(40);

                /*if (i % 10 == 0)
                {
                    Logging.Write("avg ping: " + (avgPing / 10.0).ToString("000.000") + " ms");
                    avgPing = 0.0;
                }*/
            }
        }

        public async static Task TestDuplex(
        Func<bool> cancel,
        Func<bool> shouldReport,
        TimeControl timeControl,
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


            IncomingSignal[][][] unreliableSignalsSeenFromServer = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] unreliableSignalsSentFromServer = new OutgoingSignal[clientInstancesAPI.Length][][];

            IncomingSignal[][][] reliableSignalsSeenFromServer = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] reliableSignalsSentFromServer = new OutgoingSignal[clientInstancesAPI.Length][][];

            for (int i = 0; i < clientInstancesAPI.Length; i++)
            {
                unreliableSignalsSeenFromServer[i] = new IncomingSignal[timeControl.historySize][];
                reliableSignalsSeenFromServer[i] = new IncomingSignal[timeControl.historySize][];
                unreliableSignalsSentFromServer[i] = new OutgoingSignal[timeControl.historySize][];
                reliableSignalsSentFromServer[i] = new OutgoingSignal[timeControl.historySize][];


                for (int hisI = 0; hisI < timeControl.historySize; hisI++)
                {
                    unreliableSignalsSeenFromServer[i][hisI] = SignalFactory.ConstructIncomingSignalArray(5);
                    reliableSignalsSeenFromServer[i][hisI] = SignalFactory.ConstructIncomingSignalArray(5);
                    unreliableSignalsSentFromServer[i][hisI] = SignalFactory.ConstructOutgoingSignalArray(5);
                    reliableSignalsSentFromServer[i][hisI] = SignalFactory.ConstructOutgoingSignalArray(5);

                }
            }


            Logging.Write("TestDuplex: start server");

            var updatedServerTuple = await NetSignalStarter.StartServer(shouldReport(), serverInstanceAPI, serverInstanceData, serverInstanceState, cancel, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer,
                clientConnectionStatesSeenFromServer,
                unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer, timeControl);
            serverInstanceAPI[0] = updatedServerTuple.Item1;
            serverInstanceData[0] = updatedServerTuple.Item2;


            await Task.Delay(1000);
            Logging.Write("TestDuplex: start clients");


            //every client has representation of other client's signals -> 3D

            IncomingSignal[][][][] clientUnreliableIncoming, clientReliableIncoming;
            OutgoingSignal[][][][] clientUnreliableOutgoing, clientReliableOutgoing;
            ConstructSignals(timeControl, clientInstancesAPI, out clientUnreliableIncoming, out clientUnreliableOutgoing, out clientReliableIncoming, out clientReliableOutgoing);

            for (int i = 0; i < clientInstancesAPI.Length; i++)
            {
                int clientI = await NetSignalStarter.StartClient(5001, shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData,
                    //clientI == clientToLeaveAndJoin ? cancelTestClient : cancel,
                    cancel);
                NetSignalStarter.StartClientSignalSyncing(clientI, shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState,
                    clientUnreliableOutgoing[clientI], clientUnreliableIncoming[clientI],
                clientReliableOutgoing[clientI], clientReliableIncoming[clientI], cancel, serverInstanceData, timeControl);
                //clientUnreliableOutgoing, clientUnreliableIncoming,
                //clientReliableOutgoing, clientReliableIncoming);
                //clientInstancesAPI[clientI] = updatedClientTuple.Item1;
                // clientInstancesData[clientI] = updatedClientTuple.Item2;

            }
            await Task.Delay(1000);


            await SyncLogCheckWithPlayer0And1(clientReliableIncoming, clientReliableOutgoing, timeControl);

            
            await Task.Delay(1000);

            await SyncLogCheckWithPlayer0And1(clientReliableIncoming, clientReliableOutgoing, timeControl);

            

            await Task.Delay(1000);


        }

        private static void ConstructSignals(TimeControl timeControl, ConnectionAPIs[] clientInstancesAPI, out IncomingSignal[][][][] clientUnreliableIncoming, out OutgoingSignal[][][][] clientUnreliableOutgoing, out IncomingSignal[][][][] clientReliableIncoming, out OutgoingSignal[][][][] clientReliableOutgoing)
        {
            clientUnreliableIncoming = new IncomingSignal[clientInstancesAPI.Length][][][];
            clientUnreliableOutgoing = new OutgoingSignal[clientInstancesAPI.Length][][][];
            clientReliableIncoming = new IncomingSignal[clientInstancesAPI.Length][][][];
            clientReliableOutgoing = new OutgoingSignal[clientInstancesAPI.Length][][][];
            for (int i = 0; i < clientInstancesAPI.Length; i++)
            {
                clientUnreliableIncoming[i] = new IncomingSignal[clientInstancesAPI.Length][][];
                clientReliableIncoming[i] = new IncomingSignal[clientInstancesAPI.Length][][];
                clientUnreliableOutgoing[i] = new OutgoingSignal[clientInstancesAPI.Length][][];
                clientReliableOutgoing[i] = new OutgoingSignal[clientInstancesAPI.Length][][];

                for (int otherClientI = 0; otherClientI < clientInstancesAPI.Length; otherClientI++)
                {
                    clientUnreliableIncoming[i][otherClientI] = new IncomingSignal[timeControl.historySize][];
                    clientReliableIncoming[i][otherClientI] = new IncomingSignal[timeControl.historySize][];
                    clientUnreliableOutgoing[i][otherClientI] = new OutgoingSignal[timeControl.historySize][];
                    clientReliableOutgoing[i][otherClientI] = new OutgoingSignal[timeControl.historySize][];

                    for (int hisI = 0; hisI < timeControl.historySize; hisI++)
                    {
                        clientUnreliableIncoming[i][otherClientI][hisI] = SignalFactory.ConstructIncomingSignalArray(8);
                        clientReliableIncoming[i][otherClientI][hisI] = SignalFactory.ConstructIncomingSignalArray(2);
                        clientUnreliableOutgoing[i][otherClientI][hisI] = SignalFactory.ConstructOutgoingSignalArray(8);
                        clientReliableOutgoing[i][otherClientI][hisI] = SignalFactory.ConstructOutgoingSignalArray(2);
                    }
                }


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
            ServerList l = new ServerList();
            l.list = new List<ServerListElementResponse>();
            MatchmakingConnectionUpdater.InitializeMatchMakingClient(ref clientCon, ref clientD, ref clientS, () => teard);

            l = await MatchmakingConnectionUpdater.GatherServerList(clientCon, clientD, clientS);

            await Task.Delay(5555);
            teard = true;
        }
    }
}
