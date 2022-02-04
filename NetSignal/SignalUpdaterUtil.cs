using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSignal
{
    public class SignalUpdaterUtil
    {

        public async static void SyncIncomingToOutgoingSignals(IncomingSignal[][] incomingSignals, OutgoingSignal[][] outgoingSignals, Func<bool> cancel)
        {
            if (incomingSignals.Length != outgoingSignals.Length)
                throw new Exception("incoming and outgoing array length unequal");

            var clientCount = Math.Min(incomingSignals.Length, outgoingSignals.Length);
            try
            {
                while (!cancel())
                {
                    for (int fromClientI = 0; fromClientI < clientCount; fromClientI++)
                        for (int signalI = 0; signalI < Math.Min(incomingSignals[fromClientI].Length, outgoingSignals[fromClientI].Length); signalI++)
                        {
                            if (incomingSignals[fromClientI][signalI].dataHasBeenUpdated)
                            {
                                for (int toClientI = 0; toClientI < clientCount; toClientI++)
                                {
                                    if (toClientI != fromClientI) //dont send to self
                                    {
                                        outgoingSignals[toClientI][signalI].data = incomingSignals[fromClientI][signalI].data;
                                        incomingSignals[toClientI][signalI].dataHasBeenUpdated = false;
                                    }
                                }
                            }
                        }
                    await Task.Delay(30);
                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }
            finally
            {
            }
        }


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
