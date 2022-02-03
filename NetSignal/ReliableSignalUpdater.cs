using System;
using System.Net.Sockets;
using System.Text;
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

        //TODO? : make use of the
        public async static void SyncSignalsToAllReliably(OutgoingSignal[][] signals, Func<bool> cancel, ConnectionAPIs[] toConnections, ConnectionMetaData[] toConnectionsDatas, ConnectionState[] toConnectionStates)
        {
            try
            {
                while (!cancel())
                {
                    for (var connectionI = 0; connectionI < toConnections.Length; connectionI++)
                    {
                        for (int fromClientI = 0; fromClientI < signals.Length; fromClientI++)
                            for (int signalI = 0; signalI < signals[fromClientI].Length; signalI++)
                            {
                                if (signals[fromClientI][signalI].dataDirty)
                                {
                                    var previousState = Util.CompareExchange(ref toConnectionStates[connectionI].tcpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                                    if (previousState != StateOfConnection.ReadyToOperate)
                                    {
                                        await Task.Delay(1);
                                        continue;
                                    }
                                    

                                    //Logging.Write("SyncSignalsToAllReliably: eligible for begin write?" + toConnectionStates[connectionI].tcpWriteStateName.ToString());
                                    //Logging.Write("data is dirty. send it reliably");

                                    //make sure correct metadata is written. TODO VERIFY
                                    /*var modified = signals[fromClientI][signalI].data;
                                    modified.clientId = fromClientI;
                                    modified.index = signalI;
                                    signals[fromClientI][signalI].data = modified;*/

                                    
                                    //Logging.Write("will send " + dataStr);
                                    var usingBytes = toConnectionStates[connectionI].tcpWriteBytes;
                                    Util.FlushBytes(usingBytes);
                                    await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () =>
                                    {
                                        SignalCompressor.Compress(signals[fromClientI][signalI].data, usingBytes, 1);
                                        try
                                        {
                                            await toConnections[connectionI].tcpStream.WriteAsync(usingBytes, 0, usingBytes.Length);
                                        }
                                        catch (SocketException e)
                                        {
                                            Logging.Write("SyncSignalsToAll: tcp client socket got closed, (unfortunately) this is intended behaviour, stop sending.");
                                        }
                                        signals[fromClientI][signalI].dataDirty = false;
                                    });

                                    Util.Exchange(ref toConnectionStates[connectionI].tcpWriteStateName, StateOfConnection.ReadyToOperate);
                                }
                            }
                        await Task.Delay(1);
                    }
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


        //uses tcp to sync signals reliably
        public async static void ReceiveSignalsReliably(IncomingSignal[][] signals, Func<bool> cancel, Action<string> report, ConnectionAPIs[] fromStreams, ConnectionMetaData[] fromDatas, ConnectionState[] fromStates)
        {
            try
            {
                while (!cancel())
                {
                    for (int streamI = 0; streamI < fromStreams.Length; streamI++)
                    {
                        var previousState = Util.CompareExchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                        if (previousState != StateOfConnection.ReadyToOperate)
                        {
                            await Task.Delay(1);
                            continue;
                        }

                        //Logging.Write("ReceiveSignalsReliably: eligible for begin read?" + fromStates[streamI].tcpReadStateName.ToString());
                        try
                        {
                            //Logging.Write("ReceiveSignalsReliably: begin read tcp stream of index " + streamI);
                            var usingBytes = fromStates[streamI].tcpReadBytes;
                            Util.FlushBytes(usingBytes);
                            //Logging.Write("ReceiveSignalsReliably");
                            fromStreams[streamI].tcpStream.BeginRead(usingBytes, 0, usingBytes.Length, MakeHandleReceiveReliableSignal(signals, fromStreams[streamI], fromDatas[streamI], fromStates[streamI], report), null);
                        }
                        catch (ObjectDisposedException e)
                        {
                            Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                            Logging.Write("ReceiveSignals: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                            continue;
                        }
                        catch (SocketException e)
                        {
                            Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                            Logging.Write("ReceiveSignals: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                            continue;
                        }
                        Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.ReadyToOperate);
                    }
                    await Task.Delay(1);
                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }
        }


    }
}
