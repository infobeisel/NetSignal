using NetSignal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestNetSignalClient
{
    class Program
    {
        static void Main(string[] args)
        {

            OutgoingSignal[] unreliableSignalSentFromServer = new OutgoingSignal[1];
            IncomingSignal[] unreliableSignalSeenFromClient = new IncomingSignal[1];
            OutgoingSignal[] unreliableSignalsSentFromClient = new OutgoingSignal[1];
            IncomingSignal[] unreliableSignalsSeenFromServer = new IncomingSignal[1];
            OutgoingSignal[] reliableSignalSentFromServer = new OutgoingSignal[1];
            IncomingSignal[] reliableSignalSeenFromClient = new IncomingSignal[1];
            OutgoingSignal[] reliableSignalsSentFromClient = new OutgoingSignal[1];
            IncomingSignal[] reliableSignalsSeenFromServer = new IncomingSignal[1];
            var cancel = false;
            var shouldPrint = false;
            ConnectionMetaData[] connectionMetaDatasSeenFromServer = new ConnectionMetaData[1];
            ConnectionAPIs[] connectionApisSeenFromServer = new ConnectionAPIs[1];
            ConnectionState[] connectionStatesSeenFromServer = new ConnectionState[1];


            //only for symmetry, this is always supposed to be an array of size one, 
            ConnectionAPIs[] server = new ConnectionAPIs[1] { new ConnectionAPIs() };
            ConnectionMetaData[] serverData = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState[] serverState = new ConnectionState[1] { new ConnectionState() };
            ConnectionMapping mapping = new ConnectionMapping();

            //this can and will be array of size N
            ConnectionAPIs[] clients = new ConnectionAPIs[1] { new ConnectionAPIs() };
            ConnectionMetaData[] clientDatas = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState[] clientState = new ConnectionState[1] { new ConnectionState() };

            serverData[0].iListenToPort = 5000;
            serverData[0].myIp = "85.214.239.45";
            

            clientDatas[0].iListenToPort= 5001;
            clientDatas[0].myIp = "";


            //NetSignalStarter.TestIncomingOnClient(() => cancel, () => shouldPrint, signalSentFromServer, signalSeenFromClient, signalsSentFromClient, signalsSeenFromServer, conFromServer, consFromServer);
            //await Task.Delay(5000);
            /*await NetSignalStarter.TestDuplex(() => cancel, () => shouldPrint, 
                unreliableSignalSentFromServer, unreliableSignalSeenFromClient, unreliableSignalsSentFromClient, unreliableSignalsSeenFromServer,
                reliableSignalSentFromServer, reliableSignalSeenFromClient, reliableSignalsSentFromClient, reliableSignalsSeenFromServer,
                connectionApisSeenFromServer, connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer,
                server, serverData, serverState, mapping, clients, clientDatas, clientState);
                */

            NetSignalStarter.TestClientsToRemoteDedicatedServer(() => cancel, () => shouldPrint,
                unreliableSignalSeenFromClient, unreliableSignalsSentFromClient,
                reliableSignalSeenFromClient, reliableSignalsSentFromClient,

                server, serverData, serverState, clients, clientDatas, clientState).Wait();

            Task.Delay(10000);
            cancel = true;
        }
    }
}
