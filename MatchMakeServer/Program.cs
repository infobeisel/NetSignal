using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using System.Runtime.InteropServices;
using NetSignal;
namespace MatchMakeServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var teard = false;
            var con = new ConnectionAPIs();
            var d = new ConnectionMetaData();
            var s = new ConnectionState();

            d.matchmakingServerPort = int.Parse(args[0]);
            

            MatchmakingConnectionUpdater.InitializeMatchMakingServer(ref con, ref d, ref s, () => teard);

            while (true)
                Thread.Sleep(1000);
            /*await Task(1000);

            var clientCon = new ConnectionAPIs();
            var clientD = new ConnectionMetaData();
            var clientS = new ConnectionState();
            clientD.matchmakingServerPort = 5432;
            clientD.matchmakingServerIp = "http://127.0.0.1";
            MatchmakingConnectionUpdater.ServerList l = new MatchmakingConnectionUpdater.ServerList();
            l.list = new List<MatchmakingConnectionUpdater.ServerListElementResponse>();
            MatchmakingConnectionUpdater.InitializeMatchMakingClient(ref clientCon, ref clientD, ref clientS, () => teard);

            MatchmakingConnectionUpdater.GatherServerList(clientCon, clientD, clientS, l);

            await Task.Delay(5555);
            teard = true;*/
        }
    }
}
