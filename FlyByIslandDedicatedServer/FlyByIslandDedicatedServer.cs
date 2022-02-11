using NetSignal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FlyByIslandDedicatedServer
{
    class FlyByIslandDedicatedServer
    {
        private static bool cancel;
        private static bool shouldPrint;
        private static ConnectionMetaData[] connectionMetaDatasSeenFromServer, serverData;
        private static ConnectionAPIs[] connectionApisSeenFromServer, server;
        private static ConnectionState[] connectionStatesSeenFromServer, serverState;
        private static IncomingSignal[][] unreliableSignalsSeenFromServer, reliableSignalsSeenFromServer;
        private static OutgoingSignal[][] unreliableSignalsSentFromServer, reliableSignalsSentFromServer;
        static void Main(string[] args)
        {
            cancel = false;
            shouldPrint = true;

            DedicatedServer.DedicatedServer.Initialize(args, cancel, shouldPrint, out connectionMetaDatasSeenFromServer, out connectionApisSeenFromServer, out connectionStatesSeenFromServer, out server, out serverData, out serverState, out unreliableSignalsSeenFromServer, out unreliableSignalsSentFromServer, out reliableSignalsSeenFromServer, out reliableSignalsSentFromServer);

            NetSignalStarter.StartServer(shouldPrint, server, serverData, serverState, () => cancel, connectionApisSeenFromServer,
                connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer, unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer).Wait();

            LoopFlyByIslandMatch().Wait();
        }

        private static async Task LoopFlyByIslandMatch()
        {
            while(true)
            {
                                

                await Task.Delay(10000);
            }

        }

        private static void StartCountdownUntilRoundStarts()
        {
            foreach(var toClient in reliableSignalsSentFromServer)
            {
                //toClient[]
            }
        }
    }
}
