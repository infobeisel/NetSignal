using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSignal
{
    public class SignalUpdaterUtil
    {
        public static async void SyncTcpToUdpSignalsWorkaroundIncoming(IncomingSignal[][][] incomingReliable, IncomingSignal[][][] incomingUnreliable, TimeControl timeControl, Func<bool> cancel)
        {
            if (incomingReliable.Length != incomingUnreliable.Length)
                throw new Exception("incoming and outgoing array length unequal");

            var clientCount = Math.Min(incomingReliable.Length, incomingUnreliable.Length);

            var tcpToUdpSignalRangeStart = incomingReliable[0][0].Length - incomingUnreliable[0][0].Length;
            var tcpToUdpSignalRangeEnd = incomingReliable[0][0].Length;
            try
            {
                while (!cancel())
                {
                    var historyIndex = SignalUpdaterUtil.CurrentHistoryIndex(timeControl);
                    for (int connectionI = 0; connectionI < clientCount; connectionI++)
                        for (int signalI = tcpToUdpSignalRangeStart; signalI < tcpToUdpSignalRangeEnd; signalI++)
                        {
                            var data = incomingReliable[connectionI][historyIndex][signalI].data;
                            data.index = signalI - tcpToUdpSignalRangeStart;
                            incomingUnreliable[connectionI][historyIndex][signalI - tcpToUdpSignalRangeStart].data = data;
                            incomingUnreliable[connectionI][historyIndex][signalI - tcpToUdpSignalRangeStart].dataHasBeenUpdated = true;
                            incomingUnreliable[connectionI][historyIndex][signalI - tcpToUdpSignalRangeStart].cameIn =
                                incomingReliable[connectionI][historyIndex][signalI].cameIn;
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

        public static void LogIncoming(IncomingSignal[][][] clientIncoming, int incomingClientId, int regressedI, Action<string> logLine, Action<string> log)
        {
            logLine("incoming signals:");
            foreach (var signal in clientIncoming[incomingClientId][regressedI])
            {
                log(signal.data.AsFloat().ToString("0.00") + " , ");
            }
            logLine("");
            foreach (var signal in clientIncoming[incomingClientId][regressedI])
            {
                log(signal.data.AsInt().ToString("0000") + " , ");
            }
            logLine("");
            logLine("");
        }

        public static void LogOutgoing(OutgoingSignal[][] clientOutgoing, int otherClientId)
        {
            Console.WriteLine("outgoing signals:");
            foreach (var signal in clientOutgoing[otherClientId])
            {
                Console.Write(signal.data.AsFloat().ToString("0.00") + " , ");
            }
            Console.WriteLine();
            foreach (var signal in clientOutgoing[otherClientId])
            {
                Console.Write(signal.data.AsInt().ToString("0000") + " , ");
            }
            Console.WriteLine();
            Console.WriteLine();
        }

        public static async void SyncURIsToROsInCaseOfUdpOverTcpWorkaround(IncomingSignal[][][] unreliableIncomingSignals, IncomingSignal[][][] reliableIncomingSignals, OutgoingSignal[][][] reliableOutgoingSignals, TimeControl timeControl, Func<bool> cancel)
        {
            var tcpToUdpSignalRangeStart = reliableOutgoingSignals[0][0].Length - unreliableIncomingSignals[0][0].Length;
            var tcpToUdpSignalRangeEnd = reliableOutgoingSignals[0][0].Length;

            if (unreliableIncomingSignals.Length != reliableOutgoingSignals.Length)
                throw new Exception("incoming and outgoing array length unequal");

            var clientCount = Math.Min(unreliableIncomingSignals.Length, reliableOutgoingSignals.Length);
            try
            {
                while (!cancel())
                {
                    var historyIndex = SignalUpdaterUtil.CurrentHistoryIndex(timeControl);

                    for (int connectionI = 0; connectionI < clientCount; connectionI++)
                    {
                        var reliableKeepAliveI = Util.findLatestHistIndex(reliableIncomingSignals[connectionI], 0);
                        var unreliableKeepAliveI = Util.findLatestHistIndex(unreliableIncomingSignals[connectionI], 0);

                        //keepalive shows that udp is not working, for this client send everything over tcp
                        if (
                            ((UdpKeepAliveInfo)unreliableIncomingSignals[connectionI][unreliableKeepAliveI][0].data.AsInt())
                            == UdpKeepAliveInfo.UdpOverTcpReceiveMode)
                        {
                            for (int signalI = 0; signalI < unreliableIncomingSignals[connectionI][historyIndex].Length; signalI++)
                            {
                                //if (unreliableIncomingSignals[fromConI][historyIndex][signalI].dataHasBeenUpdated)
                                {
                                    //Logging.Write("keepalive was tcp mode, write to  " + connectionI + " , start from signal " + tcpToUdpSignalRangeStart);//+ reliableOutgoingSignals[connectionI][signalI + tcpToUdpSignalRangeStart].data);
                                    for (int fromConI = 0; fromConI < clientCount; fromConI++)
                                    {
                                        //does not arrive at client for some reason
                                        reliableOutgoingSignals[connectionI][fromConI][signalI + tcpToUdpSignalRangeStart].data = unreliableIncomingSignals[fromConI][historyIndex][signalI].data;
                                        reliableOutgoingSignals[connectionI][fromConI][signalI + tcpToUdpSignalRangeStart].dataDirty = true;
                                    }
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

        public static async void SyncIncomingToOutgoingSignals(IncomingSignal[][][] incomingSignals, OutgoingSignal[][][] outgoingSignals, TimeControl timeControl, Func<bool> cancel)
        {
            if (incomingSignals.Length != outgoingSignals.Length)
                throw new Exception("incoming and outgoing array length unequal");

            var clientCount = Math.Min(incomingSignals.Length, outgoingSignals.Length);
            var signalCount = incomingSignals[0][0].Length;
            try
            {
                while (!cancel())
                {
                    var historyIndex = SignalUpdaterUtil.CurrentHistoryIndex(timeControl);
                    for (int connectionI = 0; connectionI < clientCount; connectionI++)
                        for (int signalI = 0; signalI < signalCount; signalI++)
                        {
                            if (incomingSignals[connectionI][historyIndex][signalI].dataHasBeenUpdated)
                            {
                                for (int toConI = 0; toConI < clientCount; toConI++)
                                {
                                    outgoingSignals[toConI][connectionI][signalI].WriteFloat(incomingSignals[connectionI][historyIndex][signalI].data.AsFloat());
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

        public static async Task WriteToIncomingSignals(IncomingSignal[][][] signals, TimeControl timeControl, Action<string> report, byte[] bytes, UdpReceiveResult udpReceiveResult, Action<int, int, int> perSignalUpdate, params ConnectionMetaData[] fromConnectionDatas)
        {
            await MessageDeMultiplexer.Divide(bytes, async () =>
            {
                int historyIndex = CurrentHistoryIndex(timeControl);
             

                SignalCompressor.Decompress(report, bytes, 1, historyIndex, signals, perSignalUpdate);
            },
            async () => { Logging.Write("ReceiveSignals: unexpected package connection request!?"); },
            async () =>
            {
                var package = SignalCompressor.DecompressDataPackage(bytes, 1);
                report("keep alive package: " + package.ToString());
            });
        }

        public static int CurrentHistoryIndex(TimeControl timeControl)
        {
            return timeControl.CurrentHistIndex % timeControl.historySize;
        }

        public static float Regress(IncomingSignal[][][] signals, int clientId, int signalI, TimeControl timeControl, long toTicks)
        {
            int latest = 0;
            int secondLatest = 0;

            Util.findTwoLatestHistIndices(signals[clientId], signalI, out latest, out secondLatest);

            var minTimeStamp = signals[clientId][secondLatest][signalI].data.timeStamp.Ticks;
            var ticks = signals[clientId][secondLatest][signalI].data.timeStamp.Ticks;
            var secondLatestX = (double)(ticks - minTimeStamp) / (double)TimeSpan.TicksPerMillisecond / 1000.0;
            var secondLatestY = signals[clientId][secondLatest][signalI].data.AsFloat();

            ticks = signals[clientId][latest][signalI].data.timeStamp.Ticks;
            var latestX = (double)(ticks - minTimeStamp) / (double)TimeSpan.TicksPerMillisecond / 1000.0;
            var latestY = signals[clientId][latest][signalI].data.AsFloat();

            var ping = signals[clientId][latest][signalI].cameIn.Ticks - ticks;

            double nowX = (double)(toTicks - minTimeStamp - ping) / (double)TimeSpan.TicksPerMillisecond / 1000.0;

            double alpha = (nowX - secondLatestX) / (latestX - secondLatestX);
            return (float)((1.0 - alpha) * secondLatestY + alpha * latestY);
        }
    }
}