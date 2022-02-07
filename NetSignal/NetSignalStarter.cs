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
                UnreliableSignalUpdater.ReceiveSignals(serverConnection[0], serverData[0], serverState[0], unreliableIncomingSignals, cancel,
                (string r) => { if (shouldLog) Logging.Write("server receive: " + r); }, connectionDatas);
            });

            ReliableSignalUpdater.ReceiveSignalsReliablyFromAll(reliableIncomingSignals, cancel, (string s) => { }, connections, connectionDatas, connectionStates);

            

            Logging.Write("StartServer: start accept tcp connections");
            ConnectionUpdater.StartProcessTCPConnections(serverConnection[0], serverState[0], connections, connectionDatas, connectionStates, cancel, () => { });

            Logging.Write("StartServer: start sync signals");
            
            UnreliableSignalUpdater.SyncSignalsToAll(serverConnection[0], serverData[0], serverState[0], unreliableOutgoingSignals,
            (string r) => { if (shouldLog) Logging.Write("server send: " + r); }, cancel, connections, connectionDatas, connectionStates);
            
            ReliableSignalUpdater.SyncSignalsToAllReliably(reliableOutgoingSignals, cancel,connections, connectionDatas, connectionStates);

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
                MatchmakingConnectionUpdater.PeriodicallySendKeepAlive(serverConnection[0], serverData[0], serverState[0], cancel, 5000);
            });

            return new Tuple<ConnectionAPIs, ConnectionMetaData>(serverConnection[0], serverData[0]);
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



    }
}
