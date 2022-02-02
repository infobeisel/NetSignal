using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSignal;
namespace DedicatedServer
{
    class Program
    {
        static void Main(string[] args)
        {

            OutgoingSignal[] unreliableSignalSentFromServer = new OutgoingSignal[2];
            IncomingSignal[] unreliableSignalsSeenFromServer = new IncomingSignal[2];
            OutgoingSignal[] reliableSignalSentFromServer = new OutgoingSignal[2];
            IncomingSignal[] reliableSignalsSeenFromServer = new IncomingSignal[2];
            var cancel = false;
            ConnectionMetaData[] connectionMetaDatasSeenFromServer = new ConnectionMetaData[1];
            ConnectionAPIs[] connectionApisSeenFromServer = new ConnectionAPIs[1];
            ConnectionState[] connectionStatesSeenFromServer = new ConnectionState[1];


            //only for symmetry, this is always supposed to be an array of size one, 
            ConnectionAPIs[] server = new ConnectionAPIs[1] { new ConnectionAPIs() };
            ConnectionMetaData[] serverData = new ConnectionMetaData[1] { new ConnectionMetaData() };
            ConnectionState[] serverState = new ConnectionState[1] { new ConnectionState() };
            ConnectionMapping mapping = new ConnectionMapping();

            serverData[0].iListenToPort = int.Parse(args[0]);
            serverData[0].myIp = "127.0.0.1";

            /*
             * 
             * NetSignalStarter.StartServer(server, serverData, serverState, () => cancel, mapping, connectionApisSeenFromServer,
                connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer, unreliableSignalSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalSentFromServer, reliableSignalsSeenFromServer);
                */
            throw new Exception();

            while (true)
                Thread.Sleep(1000);

        }
    }
}
