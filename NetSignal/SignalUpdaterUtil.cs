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


                //Logging.Write("parse " + bytes.ToString() + " # " + bytes.Length);
                var parsedString = Encoding.ASCII.GetString(bytes, 1, bytes.Length - 1);


                report(parsedString);
                var package = SignalCompressor.DecompressFloatPackage(parsedString);
                report(package.ToString());
                signals[package.clientId][package.index].data = package;
                signals[package.clientId][package.index].cameIn = DateTime.UtcNow;

            },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package connection request!?"); },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package tcp keepalive!?"); },
            async () => {
                //Logging.Write("parse " + bytes.ToString() + " # " + bytes.Length);
                var parsedString = Encoding.ASCII.GetString(bytes, 1, bytes.Length - 1);
                var package = SignalCompressor.DecompressKeepAlive(parsedString);
                report(package.ToString());
                if (fromConnectionDatas.Length > package.clientId)
                {
                    //Logging.Write("I, server,  received sth from " + udpReceiveResult.RemoteEndPoint + " . write to " + package.clientId);
                    fromConnectionDatas[package.clientId].iListenToPort = udpReceiveResult.RemoteEndPoint.Port;
                    fromConnectionDatas[package.clientId].myIp = udpReceiveResult.RemoteEndPoint.Address.ToString();
                }
            });
        }
    }
}
