using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSignal
{
    public class SignalUpdaterUtil
    {
        public async static void SyncTcpToUdpSignalsWorkaroundIncoming(IncomingSignal[][][] incomingReliable, IncomingSignal[][][] incomingUnreliable, TimeControl timeControl, Func<bool> cancel)
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
                        for (int signalI = tcpToUdpSignalRangeStart; signalI < tcpToUdpSignalRangeEnd ; signalI++)
                        {
                           // if (incomingReliable[connectionI][historyIndex][signalI].dataHasBeenUpdated)
                            {
                                incomingUnreliable[connectionI][historyIndex][signalI - tcpToUdpSignalRangeStart].data = 
                                    incomingReliable[connectionI][historyIndex][signalI].data;
                              
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
        public static void LogIncoming(IncomingSignal[][][] clientIncoming, int incomingClientId, int regressedI)
        {
            Console.WriteLine("incoming signals:");
            foreach (var signal in clientIncoming[incomingClientId][regressedI])
            {
                Console.Write(signal.data.AsFloat().ToString("0.00") + " , ");
            }
            Console.WriteLine();
            foreach (var signal in clientIncoming[incomingClientId][regressedI])
            {
                Console.Write(signal.data.AsInt().ToString("0000") + " , ");
            }
            Console.WriteLine();
            Console.WriteLine();
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

        public async static void SyncURIsToROsInCaseOfUdpOverTcpWorkaround(IncomingSignal[][][] unreliableIncomingSignals, IncomingSignal[][][] reliableIncomingSignals, OutgoingSignal[][][] reliableOutgoingSignals, TimeControl timeControl, Func<bool> cancel)
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
                        var reliableKeepAliveI = ConnectionUpdater.findLatest(reliableIncomingSignals[connectionI], 0);
                        var unreliableKeepAliveI = ConnectionUpdater.findLatest(unreliableIncomingSignals[connectionI], 0);
                        
                        //keepalive shows that udp is not working, for this client send everything over tcp
                        if( 
                            ((UdpKeepAliveInfo) unreliableIncomingSignals[connectionI][unreliableKeepAliveI][0].data.AsInt())
                            == UdpKeepAliveInfo.UdpOverTcpReceiveMode)
                        //if (Math.Abs((reliableIncomingSignals[connectionI][reliableKeepAliveI][0].data.timeStamp - 
                        //unreliableIncomingSignals[connectionI][unreliableKeepAliveI][0].data.timeStamp).TotalMilliseconds) > 1000)
                        {
                            for (int signalI = 0; signalI < unreliableIncomingSignals[connectionI][historyIndex].Length; signalI++)
                            {
                                if (unreliableIncomingSignals[connectionI][historyIndex][signalI].dataHasBeenUpdated)
                                {

                                    //Logging.Write("keepalive was tcp mode, write to  " + connectionI + " , start from signal " + tcpToUdpSignalRangeStart);//+ reliableOutgoingSignals[connectionI][signalI + tcpToUdpSignalRangeStart].data);
                                    for (int fromConI = 0; fromConI < clientCount; fromConI++)
                                    {
                                        //does not arrive at client for some reason
                                        reliableOutgoingSignals[connectionI][fromConI][signalI + tcpToUdpSignalRangeStart].data = unreliableIncomingSignals[connectionI][historyIndex][signalI].data;
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




        public async static void SyncIncomingToOutgoingSignals(IncomingSignal[][][] incomingSignals, OutgoingSignal[][][] outgoingSignals, TimeControl timeControl, Func<bool> cancel)
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
                                for(int toConI = 0; toConI < clientCount; toConI++)
                                {
                                    outgoingSignals[connectionI][toConI][signalI].WriteFloat(incomingSignals[connectionI][historyIndex][signalI].data.AsFloat());
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


        public static async Task WriteToIncomingSignals(IncomingSignal[][][] signals, TimeControl timeControl, Action<string> report, byte[] bytes, UdpReceiveResult udpReceiveResult, Action<int, int, int> perSignalUpdate, params ConnectionMetaData[] fromConnectionDatas )
        {
            await MessageDeMultiplexer.Divide(bytes, async () =>
            {

                int historyIndex = CurrentHistoryIndex(timeControl);
                /*var package = SignalCompressor.DecompressDataPackage(bytes, 1);
                report("data package: " + package.ToString());
                
                signals[package.clientId][historyIndex][package.index].data = package;
                signals[package.clientId][historyIndex][package.index].cameIn = new DateTime(timeControl.CurrentTimeTicks);
                */

                SignalCompressor.Decompress(report,bytes, 1, historyIndex, signals, perSignalUpdate);

                

            },
            async () => { Logging.Write("ReceiveSignals: unexpected package connection request!?"); },
            async () => {
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

            //int regressSize = timeControl.historySize;
            int regressSize = 4;

            /*ThreadLocal<double[]> xs = new ThreadLocal<double[]>();
            if (xs.Value == null || xs.Value.Length != regressSize )
                xs.Value = new double[regressSize ];
            ThreadLocal<double[]> ys = new ThreadLocal<double[]>();
            if (ys.Value == null || ys.Value.Length != regressSize )
                ys.Value = new double[regressSize ];
            */

            double[] xs = new double[regressSize];
            double[] ys = new double[regressSize];
            
            var historyI = SignalUpdaterUtil.CurrentHistoryIndex(timeControl);
            var histSize = timeControl.historySize;
            var fromTimeStamp = toTicks;
            
            var minTimeStamp = long.MaxValue;
            for(int i = 0; i < regressSize; i++) {
                minTimeStamp = Math.Min(minTimeStamp, signals[clientId][((historyI - i) % histSize + histSize) % histSize][signalI].data.timeStamp.Ticks);
            }

            var latestX = double.MinValue;
            var latestY = 0.0;

            var secondLatestX = double.MinValue;
            var secondLatestY = 0.0;

            for (int i = 0; i < regressSize ;i++)// regressSize; i++)
            {   
                var ticks = signals[clientId][((historyI - i) % histSize +histSize) % histSize][signalI].data.timeStamp.Ticks;
                double x = (double)(ticks - minTimeStamp) / (double)TimeSpan.TicksPerMillisecond / 1000.0;
                double y = signals[clientId][((historyI - i) % histSize +histSize) % histSize][signalI].data.AsFloat();

                xs[i] = x;
                ys[i] = y;

                if(x > latestX) {
                    latestX = x;
                    latestY = y;
                }
                if(x > secondLatestX && x != latestX) {
                    secondLatestX = x;
                    secondLatestY = y;
                }
            }

            double nowX = (double)(toTicks - minTimeStamp) / (double)TimeSpan.TicksPerMillisecond / 1000.0;
            var ySpeed = latestY - secondLatestY;
            var xSpeed = latestX - secondLatestX;
            var yStep = ySpeed / xSpeed;
            var extrapolateSpeedX = nowX - latestX;
            var ret = latestY + yStep * extrapolateSpeedX;
            return (float)ret;
    
            //var poly = MathNet.Numerics.Polynomial.Fit(xs, ys, 2);
            //return (float)poly.Evaluate( (double)(toTicks - minTimeStamp) / (double)TimeSpan.TicksPerMillisecond / 1000.0);
            //return (float)latestY;

        }

    }
}
