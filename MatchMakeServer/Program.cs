using NetSignal;
using System.Threading;

namespace MatchMakeServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var teard = false;
            var con = new ConnectionAPIs();
            var d = new ConnectionMetaData();
            var s = new ConnectionState();

            d.matchmakingServerPort = int.Parse(args[0]);

            MatchmakingConnectionUpdater.InitializeMatchMakingServer(ref con, ref d, ref s, () => teard);

            while (true)
                Thread.Sleep(1000);
        }
    }
}