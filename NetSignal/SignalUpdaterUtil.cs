using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSignal
{
    public class SignalUpdaterUtil
    {

        public async static void SyncIncomingToOutgoingSignals(IncomingSignal[][][] incomingSignals, OutgoingSignal[][][] outgoingSignals, TimeControl timeControl, Func<bool> cancel)
        {
            if (incomingSignals.Length != outgoingSignals.Length)
                throw new Exception("incoming and outgoing array length unequal");

            var clientCount = Math.Min(incomingSignals.Length, outgoingSignals.Length);
            try
            {
                while (!cancel())
                {
                    var historyIndex = SignalUpdaterUtil.CurrentHistoryIndex(timeControl);
                    for (int connectionI = 0; connectionI < clientCount; connectionI++)
                        for (int signalI = 0; signalI < Math.Min(incomingSignals[connectionI][historyIndex].Length, outgoingSignals[connectionI][historyIndex].Length); signalI++)
                        {
                            if (incomingSignals[connectionI][historyIndex][signalI].dataHasBeenUpdated)
                            {
                                outgoingSignals[connectionI][historyIndex][signalI].WriteFloat(incomingSignals[connectionI][historyIndex][signalI].data.AsFloat());
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


        public static async Task WriteToIncomingSignals(IncomingSignal[][][] signals, TimeControl timeControl, Action<string> report, byte[] bytes, UdpReceiveResult udpReceiveResult, params ConnectionMetaData[] fromConnectionDatas)
        {
            await MessageDeMultiplexer.Divide(bytes, async () =>
            {

                var package = SignalCompressor.DecompressDataPackage(bytes, 1);
                report("data package: " + package.ToString());
                long historyIndex = CurrentHistoryIndex(timeControl);
                signals[package.clientId][historyIndex][package.index].data = package;
                signals[package.clientId][historyIndex][package.index].cameIn = new DateTime(timeControl.CurrentTimeTicks);

            },
            async () => { Logging.Write("ReceiveSignals: unexpected package connection request!?"); },
            async () => {
                var package = SignalCompressor.DecompressDataPackage(bytes, 1);
                report("keep alive package: " + package.ToString());
                
            },
            async () => {
                

                var package = SignalCompressor.DecompressDataPackage(bytes, 1);

                report("keep alive package: " + package.ToString());
                if (fromConnectionDatas.Length > package.clientId)
                {
                
                    fromConnectionDatas[package.clientId].iListenToPort = udpReceiveResult.RemoteEndPoint.Port;
                    fromConnectionDatas[package.clientId].myIp = udpReceiveResult.RemoteEndPoint.Address.ToString();
                }
            });
        }

        public static long CurrentHistoryIndex(TimeControl timeControl)
        {
            return (timeControl.CurrentTimeTicks / TimeSpan.TicksPerMillisecond / timeControl.updateTimeStepMs) % timeControl.historySize;
        }
    }
}
