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
                    for (int connectionI = 0; connectionI < clientCount; connectionI++)
                        for (int signalI = 0; signalI < Math.Min(incomingSignals[connectionI].Length, outgoingSignals[connectionI].Length); signalI++)
                        {
                            if (incomingSignals[connectionI][signalI].dataHasBeenUpdated)
                            {
                                outgoingSignals[connectionI][signalI].WriteFloat(incomingSignals[connectionI][signalI].data.AsFloat());
                                /*for (int toConnectionI = 0; toConnectionI < clientCount; toConnectionI++)
                                {
                                    if (fromConnectionI != toConnectionI) //dont send to self
                                    {
                                        outgoingSignals[toConnectionI][signalI].WriteFloat( incomingSignals[fromConnectionI][signalI].data.AsFloat());
                                        //outgoingSignals[toConnectionI][signalI].data = incomingSignals[fromConnectionI][signalI].data;
                                        //incomingSignals[toConnectionI][signalI].dataHasBeenUpdated = false;
                                    }
                                }*/
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

                var package = SignalCompressor.DecompressDataPackage(bytes, 1);
                report("data package: " + package.ToString());
                signals[package.clientId][package.index].data = package;
                signals[package.clientId][package.index].cameIn = DateTime.UtcNow;

            },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package connection request!?"); },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package tcp keepalive!?"); },
            async () => {
                

                var package = SignalCompressor.DecompressKeepAlive(bytes, 1);

                report("keep alive package: " + package.ToString());
                if (fromConnectionDatas.Length > package.clientId)
                {
                
                    fromConnectionDatas[package.clientId].iListenToPort = udpReceiveResult.RemoteEndPoint.Port;
                    fromConnectionDatas[package.clientId].myIp = udpReceiveResult.RemoteEndPoint.Address.ToString();
                }
            });
        }
    }
}
