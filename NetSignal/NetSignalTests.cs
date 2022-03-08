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

            int clientCount = 4;

            ConnectionMetaData[] connectionMetaDatasSeenFromServer = new ConnectionMetaData[clientCount];
            ConnectionAPIs[] connectionApisSeenFromServer = new ConnectionAPIs[clientCount];
            ConnectionState[] connectionStatesSeenFromServer = new ConnectionState[clientCount];




            //only for symmetry, this is always supposed to be an array of size one, 
            ConnectionAPIs[] server = new ConnectionAPIs[1] { new ConnectionAPIs() };
            ConnectionMetaData[] serverData = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState[] serverState = new ConnectionState[1] { new ConnectionState() };

            serverData[0].iListenToPort = 5000;
            //serverData[0].myIp = "127.0.0.1";
            serverData[0].myIp = "85.214.239.45";
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
            //await TestDuplex(() => cancel, () => shouldPrint, connectionApisSeenFromServer, connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer,
            //server, serverData, serverState, clients, clientDatas, clientState);
            //await TestClientsToRemoteDedicatedServer(5002, () => cancel, () => shouldPrint,
            //server, serverData, serverState, clients, clientDatas, clientState);

            TestCompressor();

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
        


        public async static Task TestClientsToRemoteDedicatedServer(
            int countUpFromPort, 
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
            TimeControl timeControlServer = new TimeControl(false, DateTime.UtcNow.Ticks, 60, 30);

            IncomingSignal[][][][] clientUnreliableIncoming, clientReliableIncoming;
            OutgoingSignal[][][][] clientUnreliableOutgoing, clientReliableOutgoing;
            ConstructSignals(timeControlServer, clientInstancesAPI, out clientUnreliableIncoming, out clientUnreliableOutgoing, out clientReliableIncoming, out clientReliableOutgoing);

            List<TimeControl> clientTimeControls = new List<TimeControl>();
            for (int i = 0; i < clientInstancesData.Length; i++)
            {
                TimeControl timeControlClient = new TimeControl(false, DateTime.UtcNow.Ticks, 60, 30);
                clientTimeControls.Add(timeControlClient);
            }
            int clientI = -1;
            for (int i = 0; i < 1; i++)
            {
                
                clientI =  await NetSignalStarter.StartClient(countUpFromPort, shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData,
                    //clientI == clientToLeaveAndJoin ? cancelTestClient : cancel,
                    cancel);
                if (clientI == -1)
                {
                    Logging.Write("client connection not successful: too many players");
                    continue;
                }

                NetSignalStarter.StartClientSignalSyncing(clientI, shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState,
                    clientUnreliableOutgoing[clientI], clientUnreliableIncoming[clientI],
                clientReliableOutgoing[clientI], clientReliableIncoming[clientI], cancel, serverInstanceData, clientTimeControls[clientI]);
                //clientUnreliableOutgoing, clientUnreliableIncoming,
                //clientReliableOutgoing, clientReliableIncoming);
                //clientInstancesAPI[clientI] = updatedClientTuple.Item1;
                // clientInstancesData[clientI] = updatedClientTuple.Item2;
                await Task.Delay(1000);

            }

            await Task.Delay(1000);

            

            await Task.Delay(1000);


            //await SyncLogCheckWithPlayer0And1(clientI, clientReliableIncoming, clientReliableOutgoing[clientI], clientReliableIncoming,  clientTimeControls[0], clientTimeControls[1], clientI, clientI == 0 ? 1 : 0);
            await SyncLogCheckWithPlayer0And1(clientI, clientUnreliableIncoming, clientUnreliableOutgoing[clientI], clientReliableIncoming,  clientTimeControls[0], clientTimeControls[1], clientI, clientI == 0 ? 1 : 0);


            await Task.Delay(100000);

        }


        private static  void TestCompressor()
        {
            System.Random rng = new Random();
            OutgoingSignal[] signls = new OutgoingSignal[10];
            for (int i = 0; i < signls.Length; i++)
            {
                signls[i].data = new DataPackage();
                signls[i].dataDirty = false;
            }
            {
                var s = new DataPackage();
                s.WriteFloat((float)rng.NextDouble());
                s.timeStamp = DateTime.UtcNow;
                s.index = 0;
                s.clientId = 5;
                signls[0].data = s;

            }

            for (int i = 0; i < 6; i++)
            {
                var s = new DataPackage();
                s.WriteFloat((float)rng.NextDouble());
                s.timeStamp = DateTime.UtcNow;
                s.index = i;
                s.clientId = 5;
                signls[i].data = s;
            }

            for (int i = 9; i < 10; i++)
            {
                var s = new DataPackage();
                s.WriteFloat((float)rng.NextDouble());
                s.timeStamp = DateTime.UtcNow;
                s.index = i;
                s.clientId = 5;
                signls[i].data = s;
            }

            byte[] byts = new byte[256];
            Util.FlushBytes(byts);

            SignalCompressor.Compress(signls, 0, byts, 0, (string s) => Logging.Write(s));

            IncomingSignal[][][] incomingSignals = new IncomingSignal[6][][];
            for (int i = 0; i < incomingSignals.Length; i++)
            {
                incomingSignals[i] = new IncomingSignal[1][];
                for (int j = 0; j < incomingSignals[i].Length; j++)
                {
                    incomingSignals[i][j] = new IncomingSignal[10];
                }
            }
            SignalCompressor.Decompress((string s) => Logging.Write(s), byts, 0, 0, incomingSignals, (int a, int b, int c) => { });

            for (int i = 0; i < 10; i++)
            {
                float outg = signls[i].data.AsFloat();
                float inc = incomingSignals[5][0][i].data.AsFloat();
                Console.WriteLine("signal " + i + " :  " + outg + "  vs " + inc + "  , " + (outg == inc));
            }
        }

        private static async Task SyncLogCheckWithPlayer0And1( int sendFrom , IncomingSignal[][][][] clientIncoming, OutgoingSignal [][][] clientOutgoing, IncomingSignal [][][][] alsoIncoming,  TimeControl timeControl0,  TimeControl timeControl1, int outgoingClientId = 0, int incomingClientId = 1)
        {
            bool wasSame = false;


            var rng = new System.Random(645);

            var avgPing = 0.0;

            float trueVal = 0.0f;
            float sent = 0.0f;
            float incoming = 0.0f;
            
            for (int i = 0; i < 100000; i++)
            {

                
                //wasSame = clientOutgoing[outgoingClientId][outgoingClientId][histIndex][1].Equals(clientIncoming[incomingClientId][outgoingClientId][histIndex][1]);

                //trueVal =   100.0f *  (float)Math.Sin(((float)i / 1000.0f) * Math.PI);
                trueVal += 0.001f;
                if (i % 1 == 0)
                {
                    sent = trueVal;
                    for (int j = 1; j < clientOutgoing[outgoingClientId][outgoingClientId].Length; j++)
                    {
                        var a = new DataPackage();
                        a.WriteFloat(sent);
                        a.clientId = outgoingClientId;
                        a.index = j;
                        a.timeStamp = new DateTime(timeControl0.CurrentTimeTicks);
                        clientOutgoing[outgoingClientId][outgoingClientId][j].data = a;
                       // Console.WriteLine("wrote  signal " + j);
                    }
                    
                }

                

                
                await Task.Delay(timeControl0.updateTimeStepMs);

                if (i % 1 == 0)
                {
                    var histIndex = SignalUpdaterUtil.CurrentHistoryIndex(timeControl1);
                    //var regressed = SignalUpdaterUtil.Regress(clientIncoming[incomingClientId], incomingClientId, 1, timeControl1, DateTime.UtcNow.Ticks);
                    var regressedI = ConnectionUpdater.findLatest(clientIncoming[outgoingClientId][incomingClientId], 1);
                    var regressed = clientIncoming[outgoingClientId][incomingClientId][regressedI][1].data.AsFloat();
                    //Console.WriteLine("true: " + trueVal.ToString("0.000") + " regressed: " + regressed.ToString("0.000") + ", err: " + (100.0f * (regressed - trueVal) / trueVal).ToString("0.000"));
                    SignalUpdaterUtil.LogIncoming(clientIncoming[outgoingClientId],  incomingClientId, regressedI, (string s) => Console.WriteLine(s), (string s) => Console.Write(s));
                    SignalUpdaterUtil.LogIncoming(alsoIncoming[outgoingClientId],  incomingClientId, regressedI, (string s) => Console.WriteLine(s), (string s) => Console.Write(s));
                    //Console.WriteLine(" val from client  " + incomingClientId + " : " + regressed.ToString("0.000"));
                    //TODO val is not working , why : ((( trying to sync over tcp in case udp does not work, but it doesnt work yet

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

            
            
            TimeControl timeControlServer = new TimeControl(false, DateTime.UtcNow.Ticks, 60, 30);

            IncomingSignal[][][] unreliableSignalsSeenFromServer = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] unreliableSignalsSentFromServer = new OutgoingSignal[clientInstancesAPI.Length][][];

            IncomingSignal[][][] reliableSignalsSeenFromServer = new IncomingSignal[clientInstancesAPI.Length][][];
            OutgoingSignal[][][] reliableSignalsSentFromServer = new OutgoingSignal[clientInstancesAPI.Length][][];

            for (int i = 0; i < clientInstancesAPI.Length; i++)
            {
                unreliableSignalsSeenFromServer[i] = new IncomingSignal[timeControlServer.historySize][];
                reliableSignalsSeenFromServer[i] = new IncomingSignal[timeControlServer.historySize][];

                unreliableSignalsSentFromServer[i] = new OutgoingSignal[clientInstancesAPI.Length][]; SignalFactory.ConstructOutgoingSignalArray(5);
                reliableSignalsSentFromServer[i] = new OutgoingSignal[clientInstancesAPI.Length][];  SignalFactory.ConstructOutgoingSignalArray(5+5);


                for (int hisI = 0; hisI < timeControlServer.historySize; hisI++)
                {
                    unreliableSignalsSeenFromServer[i][hisI] = SignalFactory.ConstructIncomingSignalArray(5);
                    reliableSignalsSeenFromServer[i][hisI] = SignalFactory.ConstructIncomingSignalArray(5 + 5);
                  

                }
                for (int toCi = 0; toCi < clientInstancesAPI.Length; toCi++)
                {
                    unreliableSignalsSentFromServer[i][toCi] = SignalFactory.ConstructOutgoingSignalArray(5);
                    reliableSignalsSentFromServer[i][toCi] = SignalFactory.ConstructOutgoingSignalArray(5 + 5);
                }
            }


            Logging.Write("TestDuplex: start server");


            var updatedServerTuple = await NetSignalStarter.StartServer(shouldReport(), serverInstanceAPI, serverInstanceData, serverInstanceState, cancel, clientConnectionsSeenFromServer, clientConnectionDatasSeenFromServer,
                clientConnectionStatesSeenFromServer,
                unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer, timeControlServer);
            serverInstanceAPI[0] = updatedServerTuple.Item1;
            serverInstanceData[0] = updatedServerTuple.Item2;


            await Task.Delay(1000);
            Logging.Write("TestDuplex: start clients");


            //every client has representation of other client's signals -> 3D

            IncomingSignal[][][][] clientUnreliableIncoming, clientReliableIncoming;
            OutgoingSignal[][][][] clientUnreliableOutgoing, clientReliableOutgoing;
            ConstructSignals(timeControlServer, clientInstancesAPI, out clientUnreliableIncoming, out clientUnreliableOutgoing, out clientReliableIncoming, out clientReliableOutgoing);

            List<TimeControl> clientTimeControls = new List<TimeControl>();
            
            for (int i = 0; i < clientInstancesAPI.Length ; i++)
            {
                TimeControl timeControlClient = new TimeControl(false, DateTime.UtcNow.Ticks, 60, 30);
                clientTimeControls.Add(timeControlClient);
                int clientI = await NetSignalStarter.StartClient(5001, shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState, serverInstanceData,
                    //clientI == clientToLeaveAndJoin ? cancelTestClient : cancel,
                    cancel);
                if(clientI == -1) {
                    Logging.Write("client connection not successful: too many players");
                    continue;
                }
                    
                NetSignalStarter.StartClientSignalSyncing(clientI, shouldReport, clientInstancesAPI, clientInstancesData, clientInstancesState,
                    clientUnreliableOutgoing[clientI], clientUnreliableIncoming[clientI],
                clientReliableOutgoing[clientI], clientReliableIncoming[clientI], cancel, serverInstanceData, timeControlClient);
                //clientUnreliableOutgoing, clientUnreliableIncoming,
                //clientReliableOutgoing, clientReliableIncoming);
                //clientInstancesAPI[clientI] = updatedClientTuple.Item1;
                // clientInstancesData[clientI] = updatedClientTuple.Item2;
                await Task.Delay(1000);

            }
            await Task.Delay(1000);


            //await SyncLogCheckWithPlayer0And1(clientReliableIncoming, clientReliableOutgoing, clientTimeControls[0], clientTimeControls[1]);
            //await SyncLogCheckWithPlayer0And1(clientUnreliableIncoming, clientUnreliableOutgoing, clientTimeControls[0], clientTimeControls[1]);

            
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

                    clientUnreliableOutgoing[i][otherClientI] =  new OutgoingSignal[clientInstancesAPI.Length][]; 
                    clientReliableOutgoing[i][otherClientI] =  new OutgoingSignal[clientInstancesAPI.Length][]; 

                    for (int hisI = 0; hisI < timeControl.historySize; hisI++)
                    {
                        clientUnreliableIncoming[i][otherClientI][hisI] = SignalFactory.ConstructIncomingSignalArray(9);
                        clientReliableIncoming[i][otherClientI][hisI] = SignalFactory.ConstructIncomingSignalArray(9+9);
                    }
                    for (int toCi = 0; toCi < clientInstancesAPI.Length; toCi++)
                    {
                        clientUnreliableOutgoing[i][otherClientI][toCi] = SignalFactory.ConstructOutgoingSignalArray(9);
                        clientReliableOutgoing[i][otherClientI][toCi] = SignalFactory.ConstructOutgoingSignalArray(9 + 9);
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
