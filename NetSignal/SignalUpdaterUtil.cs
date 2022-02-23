using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
            return timeControl.CurrentHistIndex % timeControl.historySize;
        }

        public static float Regress(IncomingSignal[][][] signals, int clientId, int signalI, TimeControl timeControl, long toTicks)
        {

            //int regressSize = timeControl.historySize;
            int regressSize = 4;

            ThreadLocal<double[]> xs = new ThreadLocal<double[]>();
            if (xs.Value == null || xs.Value.Length != regressSize )
                xs.Value = new double[regressSize ];
            ThreadLocal<double[]> ys = new ThreadLocal<double[]>();
            if (ys.Value == null || ys.Value.Length != regressSize )
                ys.Value = new double[regressSize ];

            
            var historyI = SignalUpdaterUtil.CurrentHistoryIndex(timeControl);
            var histSize = timeControl.historySize;
            var fromTimeStamp = toTicks;
            
            var minTimeStamp = long.MaxValue;
            for(int i = 0; i < regressSize; i++) {
                minTimeStamp = Math.Min(minTimeStamp, signals[clientId][((historyI - i) % histSize + histSize) % histSize][signalI].data.timeStamp.Ticks);
            }

            for (int i = 0; i < regressSize ;i++)// regressSize; i++)
            {   
                var ticks = signals[clientId][((historyI - i) % histSize +histSize) % histSize][signalI].data.timeStamp.Ticks;
                double x = (double)(ticks - minTimeStamp) / (double)TimeSpan.TicksPerMillisecond / 1000.0;
                double y = signals[clientId][((historyI - i) % histSize +histSize) % histSize][signalI].data.AsFloat();

                xs.Value[i] = x;
                ys.Value[i] = y;
                //Console.WriteLine("at x " + x.ToString("0.000") +  "  or ticks " + ticks + " , y: " + y.ToString("0.000")); 
            }
            var poly = MathNet.Numerics.Polynomial.Fit(xs.Value, ys.Value, 2);
            //Console.WriteLine("params " + poly.Coefficients[0].ToString("0.000") +  " , " + 
            // poly.Coefficients[1].ToString("0.000") +  " , " 
            // +              poly.Coefficients[2].ToString("0.000") +  " , hist index " + historyI);
            return (float)poly.Evaluate( (double)(toTicks - minTimeStamp) / (double)TimeSpan.TicksPerMillisecond / 1000.0);
        }

    }
}
