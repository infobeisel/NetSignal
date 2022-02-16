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
        private const int historySize = 10;
        private static bool cancel;
        private static bool shouldPrint;
        private static ConnectionMetaData[] connectionMetaDatasSeenFromServer, serverData;
        private static ConnectionAPIs[] connectionApisSeenFromServer, server;
        private static ConnectionState[] connectionStatesSeenFromServer, serverState;
        private static IncomingSignal[][][] unreliableSignalsSeenFromServer, reliableSignalsSeenFromServer;
        private static OutgoingSignal[][][] unreliableSignalsSentFromServer, reliableSignalsSentFromServer;
        private static TimeControl timeControl;
        static void Main(string[] args)
        {
            cancel = false;
            shouldPrint = true;
            
            DedicatedServer.DedicatedServer.Initialize(args, cancel, shouldPrint, out connectionMetaDatasSeenFromServer, out connectionApisSeenFromServer, out connectionStatesSeenFromServer, out server, out serverData, out serverState, out unreliableSignalsSeenFromServer, out unreliableSignalsSentFromServer, out reliableSignalsSeenFromServer, out reliableSignalsSentFromServer, FlyByIslandConnectionConsts.UnreliableSignalCountPerClient, FlyByIslandConnectionConsts.ReliableSignalCountPerClient, historySize);
            timeControl = new TimeControl(false, DateTime.UtcNow.Ticks, 60, historySize );
            
            NetSignalStarter.StartServer(shouldPrint, server, serverData, serverState, () => cancel, connectionApisSeenFromServer,
                connectionMetaDatasSeenFromServer, connectionStatesSeenFromServer, unreliableSignalsSentFromServer, unreliableSignalsSeenFromServer,
                reliableSignalsSentFromServer, reliableSignalsSeenFromServer, timeControl).Wait();

            LoopFlyByIslandMatch().Wait();
        }

        private static async Task LoopFlyByIslandMatch()
        {
            var rng = new System.Random();
            while (true)
            {
                await ChooseTerrainAndTrack(timeControl, rng);
                await Countdown(timeControl);
                await Match(timeControl);
                await HighScoreView(timeControl);
                await Task.Delay(1000);
            }

        }
        private async static Task ChooseTerrainAndTrack(TimeControl timeControl, System.Random rng)
        {
            var terrainId = rng.Next();
            var trackId = rng.Next();
            foreach (var toClient in reliableSignalsSentFromServer)
            {
                toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.TERRAIN_ID].WriteInt(terrainId);
                toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.TRACK_ID].WriteInt(trackId);
            }
            await Task.Delay(5000);
        }

        private async static Task Countdown(TimeControl timeControl)
        {
            foreach(var toClient in reliableSignalsSentFromServer)
            {
                toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.MATCH_STATE].WriteInt((int)MatchState.WaitForPlayers);
                OutgoingSignal.WriteLong((DateTime.UtcNow + TimeSpan.FromMinutes(1)).Ticks, ref toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.TIMESTAMP_0], ref toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.TIMESTAMP_1]);
            }
            await Task.Delay(60000);
        }

        private async static Task Match(TimeControl timeControl)
        {
            foreach (var toClient in reliableSignalsSentFromServer)
            {
                toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.MATCH_STATE].WriteInt((int)MatchState.Started);
                OutgoingSignal.WriteLong((DateTime.UtcNow + TimeSpan.FromSeconds(300)).Ticks, ref toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.TIMESTAMP_0], ref toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.TIMESTAMP_1]);
            }
            await Task.Delay(300000);
        }

        private async static Task HighScoreView(TimeControl timeControl)
        {
            foreach (var toClient in reliableSignalsSentFromServer)
            {
                toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.MATCH_STATE].WriteInt((int)MatchState.HighScoreView);
                OutgoingSignal.WriteLong((DateTime.UtcNow + TimeSpan.FromSeconds(60)).Ticks, ref toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.TIMESTAMP_0], ref toClient[SignalUpdaterUtil.CurrentHistoryIndex(timeControl)][ReliableSignalIndices.TIMESTAMP_1]);
            }
            await Task.Delay(60000);
        }
    }
}
