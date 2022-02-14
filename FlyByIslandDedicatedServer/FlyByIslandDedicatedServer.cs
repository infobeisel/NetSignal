using NetSignal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlyByIslandMultiplayerShared;
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

            DedicatedServer.DedicatedServer.Initialize(args, cancel, shouldPrint, out connectionMetaDatasSeenFromServer, out connectionApisSeenFromServer, out connectionStatesSeenFromServer, out server, out serverData, out serverState, out unreliableSignalsSeenFromServer, out unreliableSignalsSentFromServer, out reliableSignalsSeenFromServer, out reliableSignalsSentFromServer, FlyByIslandConnectionConsts.UnreliableSignalCountPerClient, FlyByIslandConnectionConsts.ReliableSignalCountPerClient);

            NetSignalStarter.StartServer(shouldPrint, server, serverData, serverState, () => cancel, connectionApisSeenFromServer,
                connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer, unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer).Wait();

            LoopFlyByIslandMatch().Wait();
        }

        private static async Task LoopFlyByIslandMatch()
        {
            while(true)
            {
                //TODO: 
                /* move shared datatypes (used in flybyisland unity and this flybyisland dedicated server project) to a separate project that is built into a .dll
                 * program simple game loop here:
                 *  - choose terrain and track hashes
                 *  - ~1 minute pre launch countdown (clients can fly around freely)
                 *  - ~5 minutes match where new highscores are accepted
                 *  - ~ minute finish and highscore table view
                 *  - repeat
                 */
                await Countdown();
                await Match();
                await HighScoreView();
                await Task.Delay(1000);
            }

        }

        private async static Task Countdown()
        {
            foreach(var toClient in reliableSignalsSentFromServer)
            {
                toClient[ReliableSignalIndices.MATCH_STATE].WriteInt((int)MatchState.WaitForPlayers);
                OutgoingSignal.WriteLong((DateTime.UtcNow + TimeSpan.FromMinutes(1)).Ticks, ref toClient[ReliableSignalIndices.TIMESTAMP_0], ref toClient[ReliableSignalIndices.TIMESTAMP_1]);
            }
            await Task.Delay(60000);
        }

        private async static Task Match()
        {
            foreach (var toClient in reliableSignalsSentFromServer)
            {
                toClient[ReliableSignalIndices.MATCH_STATE].WriteInt((int)MatchState.Started);
                OutgoingSignal.WriteLong((DateTime.UtcNow + TimeSpan.FromSeconds(300)).Ticks, ref toClient[ReliableSignalIndices.TIMESTAMP_0], ref toClient[ReliableSignalIndices.TIMESTAMP_1]);
            }
            await Task.Delay(300000);
        }

        private async static Task HighScoreView()
        {
            foreach (var toClient in reliableSignalsSentFromServer)
            {
                toClient[ReliableSignalIndices.MATCH_STATE].WriteInt((int)MatchState.HighScoreView);
                OutgoingSignal.WriteLong((DateTime.UtcNow + TimeSpan.FromSeconds(60)).Ticks, ref toClient[ReliableSignalIndices.TIMESTAMP_0], ref toClient[ReliableSignalIndices.TIMESTAMP_1]);
            }
            await Task.Delay(60000);
        }
    }
}
