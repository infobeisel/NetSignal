using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSignal
{
    public class ReliableSignalUpdater
    {
        private static AsyncCallback MakeHandleReceiveReliableSignal(IncomingSignal[][] signals, ConnectionAPIs connection, ConnectionMetaData metaData, ConnectionState connectionState, Action<string> report)
        {
            return async (IAsyncResult ar) =>
            {
                try
                {
                    var byteCountRead = connection.tcpStream.EndRead(ar);
                    await SignalUpdaterUtil.WriteToIncomingSignals(signals, report, connectionState.tcpReadBytes, new UdpReceiveResult(), metaData);

                    Util.Exchange(ref connectionState.tcpReadStateName, StateOfConnection.ReadyToOperate);
                }
                catch (ObjectDisposedException e)
                {
                    Logging.Write("MakeHandleReceiveReliableSignal: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (System.IO.IOException e)
                {
                    Logging.Write("MakeHandleReceiveReliableSignal: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (FormatException e)
                {
                    Logging.Write("MakeHandleReceiveReliableSignal: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
            };
        }

        public async static void SyncSignalsToAllReliably(OutgoingSignal[][] signals, Func<bool> cancel, ConnectionAPIs[] toConnections, ConnectionMetaData[] toConnectionsDatas, ConnectionState[] toConnectionStates)
        {
            for (var connectionI = 0; connectionI < toConnections.Length; connectionI++)
            {
                await Task.Run(() =>
                {
                    SyncSignalsToReliably(signals, cancel, toConnections, toConnectionsDatas, toConnectionStates, connectionI);
                });
            }
        }

        private async static void SyncSignalsToReliably(OutgoingSignal[][] signals, Func<bool> cancel, ConnectionAPIs[] toConnections, ConnectionMetaData[] toConnectionsDatas, ConnectionState[] toConnectionStates, int toI)
        {
            while (!cancel())
            {
                var previousState = Util.CompareExchange(ref toConnectionStates[toI].tcpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                if (previousState == StateOfConnection.Uninitialized)
                    await Task.Delay(2000);//pause

                if (previousState != StateOfConnection.ReadyToOperate)
                {
                    continue;
                }
                for (int fromClientI = 0; fromClientI < signals.Length; fromClientI++)
                    for (int signalI = 0; signalI < signals[fromClientI].Length; signalI++)
                    {
                        if (signals[fromClientI][signalI].dataDirty)
                        {
                            var usingBytes = toConnectionStates[toI].tcpWriteBytes;
                            Util.FlushBytes(usingBytes);
                            await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () =>
                            {
                                SignalCompressor.Compress(signals[fromClientI][signalI].data, usingBytes, 1);
                                try
                                {
                                    await toConnections[toI].tcpStream.WriteAsync(usingBytes, 0, usingBytes.Length);
                                    Util.Exchange(ref toConnectionStates[toI].tcpWriteStateName, StateOfConnection.ReadyToOperate);
                                }
                                catch (SocketException e)
                                {
                                    Util.Exchange(ref toConnectionStates[toI].tcpWriteStateName, StateOfConnection.Uninitialized);
                                    Logging.Write("SyncSignalsToAllReliably: tcp client socket got closed, (unfortunately) this is intended behaviour, stop sending.");
                                }
                                catch (System.IO.IOException e)
                                {
                                    Util.Exchange(ref toConnectionStates[toI].tcpWriteStateName, StateOfConnection.Uninitialized);
                                    Logging.Write("SyncSignalsToAllReliably: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                                }
                                //signals[fromClientI][signalI].dataDirty = false; TODO need proper mechanism to sync this across threads
                            });
                        }
                    }

                await Task.Delay(60);
            }
        }

        public async static void ReceiveSignalsReliablyFromAll(IncomingSignal[][] signals, Func<bool> cancel, Action<string> report, ConnectionAPIs[] fromStreams, ConnectionMetaData[] fromDatas, ConnectionState[] fromStates)
        {
            for (int streamI = 0; streamI < fromStreams.Length; streamI++)
            {
                await Task.Run(() =>
                {
                    ReceiveSignalsReliablyFrom(signals, cancel, report, fromStreams, fromDatas, fromStates, streamI);
                });
            }
        }

        //uses tcp to sync signals reliably
        private async static void ReceiveSignalsReliablyFrom(IncomingSignal[][] signals, Func<bool> cancel, Action<string> report, ConnectionAPIs[] fromStreams, ConnectionMetaData[] fromDatas, ConnectionState[] fromStates, int streamI)
        {
            while (!cancel())
            {
                var previousState = Util.CompareExchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                if (previousState == StateOfConnection.Uninitialized)
                    await Task.Delay(2000);//pause

                if (previousState != StateOfConnection.ReadyToOperate)
                {
                    continue;
                }

                try
                {
                    var usingBytes = fromStates[streamI].tcpReadBytes;
                    Util.FlushBytes(usingBytes);
                    //fromStreams[streamI].tcpStream.BeginRead(usingBytes, 0, usingBytes.Length, MakeHandleReceiveReliableSignal(signals, fromStreams[streamI], fromDatas[streamI], fromStates[streamI], report), null);
                    var bytesRead = await fromStreams[streamI].tcpStream.ReadAsync(usingBytes, 0, usingBytes.Length);

                    await SignalUpdaterUtil.WriteToIncomingSignals(signals, report, fromStates[streamI].tcpReadBytes, new UdpReceiveResult(), fromDatas[streamI]);
                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.ReadyToOperate);
                }
                catch (ObjectDisposedException e)
                {
                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (SocketException e)
                {
                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (FormatException e)
                {
                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (System.IO.IOException e)
                {
                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
            }
        }
    }
}
