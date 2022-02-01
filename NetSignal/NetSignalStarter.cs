using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace NetSignal
{

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
            
            MatchmakingConnectionUpdater.GatherServerList(clientCon, clientD, clientS, l);

            await Task.Delay(5555);
            teard = true;
        }

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
