using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSignal;
namespace DedicatedServer
{
    class DedicatedServer
    {
        static void Main(string[] args)
        {


            var cancel = false;
            var shouldPrint = false;
            ConnectionMetaData[] connectionMetaDatasSeenFromServer = new ConnectionMetaData[3];
            ConnectionAPIs[] connectionApisSeenFromServer = new ConnectionAPIs[3];
            ConnectionState[] connectionStatesSeenFromServer = new ConnectionState[3];


            //only for symmetry, this is always supposed to be an array of size one, 
            ConnectionAPIs[] server = new ConnectionAPIs[1] { new ConnectionAPIs() };
            ConnectionMetaData[] serverData = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState[] serverState = new ConnectionState[1] { new ConnectionState() };
            ConnectionMapping mapping = new ConnectionMapping();

            //this can and will be array of size N
            ConnectionAPIs[] clients = new ConnectionAPIs[3] { new ConnectionAPIs(), new ConnectionAPIs(), new ConnectionAPIs() };
            ConnectionMetaData[] clientDatas = new ConnectionMetaData[3] { new ConnectionMetaData(), new ConnectionMetaData(), new ConnectionMetaData() };
            ConnectionState[] clientState = new ConnectionState[3] { new ConnectionState(), new ConnectionState(), new ConnectionState() };



            IncomingSignal[][] unreliableSignalsSeenFromServer = new IncomingSignal[clients.Length][];
            OutgoingSignal[][] unreliableSignalsSentFromServer = new OutgoingSignal[clients.Length][];

            IncomingSignal[][] reliableSignalsSeenFromServer = new IncomingSignal[clients.Length][];
            OutgoingSignal[][] reliableSignalsSentFromServer = new OutgoingSignal[clients.Length][];

            for (int i = 0; i < clients.Length; i++)
            {
                unreliableSignalsSeenFromServer[i] = new IncomingSignal[5];
                reliableSignalsSeenFromServer[i] = new IncomingSignal[5];
                unreliableSignalsSentFromServer[i] = new OutgoingSignal[5];
                reliableSignalsSentFromServer[i] = new OutgoingSignal[5];

            }


            serverData[0].iListenToPort = int.Parse(args[0]);
            serverData[0].myIp = "127.0.0.1";

            
             NetSignalStarter.StartServer(server, serverData, serverState, () => cancel, mapping, connectionApisSeenFromServer,
                connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer, unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer).Wait();

            while (true)
            {
                Task.Delay(1000).Wait();
            }
            

        }
    }
}
