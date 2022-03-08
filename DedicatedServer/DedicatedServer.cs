using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSignal;
namespace DedicatedServer
{
    public class DedicatedServer
    {
        private const int historySize = 10;

        static void Main(string[] args)
        {
            bool cancel, shouldPrint;
            cancel = false;
            shouldPrint = true;
            ConnectionMetaData[] connectionMetaDatasSeenFromServer, serverData;
            ConnectionAPIs[] connectionApisSeenFromServer, server;
            ConnectionState[] connectionStatesSeenFromServer, serverState;
            IncomingSignal[][][] unreliableSignalsSeenFromServer, reliableSignalsSeenFromServer;
            OutgoingSignal[][][] unreliableSignalsSentFromServer, reliableSignalsSentFromServer;
            Initialize(args, cancel, shouldPrint, out connectionMetaDatasSeenFromServer, out connectionApisSeenFromServer, out connectionStatesSeenFromServer, out server, out serverData, out serverState, out unreliableSignalsSeenFromServer, out unreliableSignalsSentFromServer, out reliableSignalsSeenFromServer, out reliableSignalsSentFromServer, 9 , 9 + 9, historySize);
            TimeControl timeControl = new TimeControl(false, DateTime.UtcNow.Ticks, 60, historySize);
            
            NetSignalStarter.StartServer(shouldPrint, server, serverData, serverState, () => cancel, connectionApisSeenFromServer,
                connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer, unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer, timeControl).Wait();

            while (true)
            {
                Task.Delay(1000).Wait();
            }


        }

        public  static void Initialize(string[] args, bool cancel, bool shouldPrint, out ConnectionMetaData[] connectionMetaDatasSeenFromServer, out ConnectionAPIs[] connectionApisSeenFromServer, out ConnectionState[] connectionStatesSeenFromServer, out ConnectionAPIs[] server, out ConnectionMetaData[] serverData, out ConnectionState[] serverState, out IncomingSignal[][][] unreliableSignalsSeenFromServer, out OutgoingSignal[][][] unreliableSignalsSentFromServer, out IncomingSignal[][][] reliableSignalsSeenFromServer, out OutgoingSignal[][][] reliableSignalsSentFromServer, int unreliableCount, int reliableCount, int historyCount)
        {
            int maxPlayers = int.Parse(args[0]);

           
            connectionMetaDatasSeenFromServer = new ConnectionMetaData[maxPlayers];
            connectionApisSeenFromServer = new ConnectionAPIs[maxPlayers];
            connectionStatesSeenFromServer = new ConnectionState[maxPlayers];


            //only for symmetry, this is always supposed to be an array of size one, 
            server = new ConnectionAPIs[1] { new ConnectionAPIs() };
            serverData = new ConnectionMetaData[1] { new ConnectionMetaData() };
            serverState = new ConnectionState[1] { new ConnectionState() };



            //this can and will be array of size N
            ConnectionAPIs[] clients = new ConnectionAPIs[maxPlayers];
            ConnectionMetaData[] clientDatas = new ConnectionMetaData[maxPlayers];
            ConnectionState[] clientState = new ConnectionState[maxPlayers];

            for (int i = 0; i < maxPlayers; i++)
            {
                clients[i] = new ConnectionAPIs();
                clientDatas[i] = new ConnectionMetaData();
                clientState[i] = new ConnectionState();
            }



            unreliableSignalsSeenFromServer = new IncomingSignal[clients.Length][][];
            unreliableSignalsSentFromServer = new OutgoingSignal[clients.Length][][];
            reliableSignalsSeenFromServer = new IncomingSignal[clients.Length][][];
            reliableSignalsSentFromServer = new OutgoingSignal[clients.Length][][];


            for (int i = 0; i < clients.Length; i++)
            {
                unreliableSignalsSeenFromServer[i] = new IncomingSignal[historyCount][]; 
                reliableSignalsSeenFromServer[i] = new IncomingSignal[historyCount][];
                //unreliableSignalsSentFromServer[i] = new OutgoingSignal[historyCount][]; 
                //reliableSignalsSentFromServer[i] = new OutgoingSignal[historyCount][]; 

                unreliableSignalsSentFromServer[i] = new OutgoingSignal[clients.Length][];
                reliableSignalsSentFromServer[i] = new OutgoingSignal[clients.Length][]; 

                for(int j = 0; j < historyCount; j++)
                {
                    unreliableSignalsSeenFromServer[i][j] = SignalFactory.ConstructIncomingSignalArray(unreliableCount);
                    reliableSignalsSeenFromServer[i][j] = SignalFactory.ConstructIncomingSignalArray(reliableCount );
                    
                }
                for (int j = 0; j < clients.Length; j++)
                {
                    unreliableSignalsSentFromServer[i][j] = SignalFactory.ConstructOutgoingSignalArray(unreliableCount);
                    reliableSignalsSentFromServer[i][j] = SignalFactory.ConstructOutgoingSignalArray(reliableCount );
                }
            }

            serverData[0].myIp = args[1];
            serverData[0].iListenToPort = int.Parse(args[2]);
            serverData[0].matchmakingServerIp = args[3];
            serverData[0].matchmakingServerPort = int.Parse(args[4]);
        }

    }
}
