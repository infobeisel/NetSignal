using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSignal
{
    public class SignalUpdaterUtil
    {

        public static async Task WriteToIncomingSignals(IncomingSignal[][] signals, Action<string> report, byte[] bytes, UdpReceiveResult udpReceiveResult, params ConnectionMetaData[] fromConnectionDatas)
        {
            await MessageDeMultiplexer.Divide(bytes, async () =>
            {

                var package = SignalCompressor.DecompressFloatPackage(bytes, 1);
                report(package.ToString());
                signals[package.clientId][package.index].data = package;
                signals[package.clientId][package.index].cameIn = DateTime.UtcNow;

            },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package connection request!?"); },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package tcp keepalive!?"); },
            async () => {
                

                var package = SignalCompressor.DecompressKeepAlive(bytes, 1);

                //report(package.ToString());
                if (fromConnectionDatas.Length > package.clientId)
                {
                
                    fromConnectionDatas[package.clientId].iListenToPort = udpReceiveResult.RemoteEndPoint.Port;
                    fromConnectionDatas[package.clientId].myIp = udpReceiveResult.RemoteEndPoint.Address.ToString();
                }
            });
        }
    }
}
