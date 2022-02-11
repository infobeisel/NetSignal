using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;


namespace NetSignal
{

    public class NetSignalStarter
    {
        public async static Task<Tuple<ConnectionAPIs, ConnectionMetaData>> StartServer(bool shouldLog,
            ConnectionAPIs[] serverConnection, ConnectionMetaData[] serverData, ConnectionState[] serverState, Func<bool> cancel, ConnectionAPIs[] connections,
            ConnectionMetaData[] connectionDatas, ConnectionState[] connectionStates,
            OutgoingSignal[][] unreliableOutgoingSignals, IncomingSignal[][] unreliableIncomingSignals,
            OutgoingSignal[][] reliableOutgoingSignals, IncomingSignal[][] reliableIncomingSignals)
        {

            

            //serverData.listenPort = 3000;
            //serverData.serverIp = null;
            Logging.Write("StartServer: init multi connection");
            ConnectionUpdater.InitializeMultiConnection(ref serverConnection[0], ref serverData[0], serverState[0], connections, connectionDatas, connectionStates);

            Logging.Write(serverConnection[0].udpClient.ToString());

            Logging.Write("StartServer: start receive signals");

            _ = Task.Run(() =>
            {
                UnreliableSignalUpdater.ReceiveSignals(0, serverConnection, serverData, serverState, unreliableIncomingSignals, cancel,
                (string r) => { if (shouldLog) Logging.Write("server receive: " + r); }, connectionDatas);
            });

            ReliableSignalUpdater.ReceiveSignalsReliablyFromAllAndTrackIsConnected(reliableIncomingSignals, cancel,
                (string r) => { if (shouldLog) Logging.Write("server receive reliably: " + r); },
                System.Linq.Enumerable.Range(0,connectionDatas.Length),
                connections, connectionDatas, connectionStates);

            

            Logging.Write("StartServer: start accept tcp connections");
            ConnectionUpdater.StartProcessTCPConnections(serverConnection[0], serverState[0], connections, connectionDatas, connectionStates, cancel, () => { });

            Logging.Write("StartServer: start sync signals");
            
            //TODO REFACTOR THOSE SYNC METHODS AND SEPARATE THEM INTO one for 1-n (server) and one for 1-1 (client) ?
            UnreliableSignalUpdater.SyncSignalsToAll(unreliableOutgoingSignals,
            (string r) => { if (shouldLog) Logging.Write("server send ur: " + r); }, cancel, connections, connectionDatas, connectionStates, System.Linq.Enumerable.Range(0,connections.Length));
            
            ReliableSignalUpdater.SyncSignalsToAllReliablyAndTrackIsConnected(reliableOutgoingSignals, cancel,
             (string r) => { if (shouldLog) Logging.Write("server send r: " + r); }, System.Linq.Enumerable.Range(0,connectionDatas.Length), connections, connectionDatas, connectionStates);

            ConnectionUpdater.AwaitAndPerformTearDownClientUDP(serverConnection[0], cancel, serverState[0]);
            ConnectionUpdater.AwaitAndPerformTearDownTCPListenerAndUdpToClients(serverConnection[0], cancel, serverState[0], connections, connectionStates, connectionDatas);

            _ = Task.Run(() =>
            {
                SignalUpdaterUtil.SyncIncomingToOutgoingSignals(unreliableIncomingSignals, unreliableOutgoingSignals, cancel);
            });

            _ = Task.Run(() =>
            {
                SignalUpdaterUtil.SyncIncomingToOutgoingSignals(reliableIncomingSignals, reliableOutgoingSignals, cancel);
            });

            MatchmakingConnectionUpdater.InitializeMatchMakingClient(ref serverConnection[0],ref serverData[0],ref serverState[0], cancel);
            _ = Task.Run(() =>
            {
                MatchmakingConnectionUpdater.PeriodicallySendKeepAlive(serverConnection[0], serverData[0], serverState[0], cancel, 5000,
                    (string r) => { if (shouldLog) Logging.Write("server keep alive: " + r); });
            });

            return new Tuple<ConnectionAPIs, ConnectionMetaData>(serverConnection[0], serverData[0]);
        }

        //please provide array with one element for server*
        public async static Task<int> StartClient(int udpPort, Func<bool> shouldReport, ConnectionAPIs[] storeToClientCon, ConnectionMetaData[] storeToClientData, ConnectionState[] storeToClientState,
             ConnectionMetaData[] serverData,
            Func<bool> cancel)
        {


            int clientI = -1;
            try
            {
                ConnectionAPIs connectionApi = new ConnectionAPIs();
                ConnectionMetaData connectionMetaData = new ConnectionMetaData();
                connectionMetaData.iListenToPort = udpPort;
                ConnectionState connectionState = new ConnectionState();
                Logging.Write("StartClient: init single connection");
                var returnTuple = await ConnectionUpdater.InitializeSingleConnection(connectionApi, connectionMetaData, connectionState, serverData[0]);
                clientI = connectionState.clientID;
                if (clientI >= 0 && clientI < storeToClientCon.Length)
                {
                    storeToClientCon[clientI] = returnTuple;
                    storeToClientState[clientI] = connectionState;
                }

            } catch (SocketException e)
            {
                Logging.Write("couldnt establish connection: " + e.Message);
            }
            return clientI;
        }
        public async static void StartClientSignalSyncing (int clientI, Func<bool> shouldReport, ConnectionAPIs[] storeToClientCon, ConnectionMetaData[] storeToClientData, ConnectionState[] storeToClientState, 
            OutgoingSignal[][] unreliableOutgoingSignals, IncomingSignal[][] unreliableIncomingSignals,
            OutgoingSignal[][] reliableOutgoingSignals, IncomingSignal[][] reliableIncomingSignals, Func<bool> cancel, ConnectionMetaData[] serverData)
        { 
            
            Logging.Write("StartClient: start receive signals");

            _ = Task.Run(() =>
            {
                UnreliableSignalUpdater.ReceiveSignals(clientI, storeToClientCon, storeToClientData, storeToClientState, unreliableIncomingSignals, cancel,
                (string r) => { if (shouldReport()) Logging.Write("client " + clientI + " receive: " + r); });
            });
                
            ReliableSignalUpdater.ReceiveSignalsReliablyFromAllAndTrackIsConnected(reliableIncomingSignals, cancel, (string s) => { }, new [] {clientI},
                 storeToClientCon , storeToClientData , storeToClientState );

            Logging.Write("StartClient: start sync signals to server");

            var replicatedServerDatas = new ConnectionMetaData[storeToClientCon.Length];
            for (int i = 0; i < replicatedServerDatas.Length; i++) replicatedServerDatas[i] = serverData[0];
            UnreliableSignalUpdater.SyncSignalsToAll(unreliableOutgoingSignals,
            (string r) => { if (shouldReport()) Logging.Write("client " + clientI + " send ur: " + r); }, cancel, storeToClientCon, replicatedServerDatas, storeToClientState, new int[] {clientI });

            ReliableSignalUpdater.SyncSignalsToAllReliablyAndTrackIsConnected(reliableOutgoingSignals, cancel,
                 (string r) => { if (shouldReport()) Logging.Write("client " + clientI + " send r: " + r); }, new[] { clientI },
                               storeToClientCon, storeToClientData, storeToClientState);
  //storeToClientCon, storeToClientData, storeToClientState);

            /*_ = Task.Run(() =>
            {
                ConnectionUpdater.PeriodicallySendKeepAlive(storeToClientCon[clientI], storeToClientData[clientI], storeToClientState[clientI], serverData,
                (string r) => { if (shouldReport()) Logging.Write("client " + clientI + " send: " + r); }, cancel);
            });*/

                

            ConnectionUpdater.AwaitAndPerformTearDownClientTCP(storeToClientCon[clientI], cancel, storeToClientState[clientI]);
            ConnectionUpdater.AwaitAndPerformTearDownClientUDP(storeToClientCon[clientI], cancel, storeToClientState[clientI]);
            
        }



    }
}
