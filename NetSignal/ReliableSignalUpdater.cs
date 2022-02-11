using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSignal
{
    public class ReliableSignalUpdater
    {
       

        public async static void SyncSignalsToAllReliably(OutgoingSignal[][] signals, Func<bool> cancel, Action<string> report, IEnumerable<int> toAllIndices, ConnectionAPIs[] toConnections, ConnectionMetaData[] toConnectionsDatas, ConnectionState[] toConnectionStates)
        {
            foreach(var ind in toAllIndices)
            {
                await Task.Run(() =>
                {
                    SyncSignalsToReliably(signals, cancel, report, toConnections, toConnectionsDatas, toConnectionStates, ind);
                });
            }
        }

        private async static void SyncSignalsToReliably(OutgoingSignal[][] signals, Func<bool> cancel, Action<string> report, ConnectionAPIs[] toConnections, ConnectionMetaData[] toConnectionsDatas, ConnectionState[] toConnectionStates, int toConnectionI)
        {
            while (!cancel())
            {
                var previousState = Util.CompareExchange(ref toConnectionStates[toConnectionI].tcpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                if (previousState == StateOfConnection.Uninitialized)
                    await Task.Delay(2000);//pause

                if (previousState != StateOfConnection.ReadyToOperate)
                {
                    await Task.Delay(2000);
                    continue;
                }
                report("try to send r to " + toConnectionI);

                bool isSyncingSuccessfully = true;
               // Logging.Write("SyncSignalsToReliably: willtoConnectionsDatas.Length " + toConnectionsDatas.Length);
                for (int fromConnectionI = 0; fromConnectionI < toConnectionsDatas.Length && isSyncingSuccessfully; fromConnectionI++)
                {
                    int fromClientId = toConnectionStates[fromConnectionI].clientID;

                    if (fromClientId == -1)
                        continue;

                    for (int signalI = 0; signalI < signals[fromClientId].Length && isSyncingSuccessfully; signalI++)
                    {
                        
                        
                        if (signals[fromClientId][signalI].dataDirty)
                        {
                            var dataToSend = signals[fromClientId][signalI].data;
                            dataToSend.clientId = fromClientId; //make sure client id is correct;
                            dataToSend.index = signalI;
                            signals[fromClientId][signalI].data = dataToSend;


                            report("send data to " + toConnectionI + " : " + dataToSend);

                            var usingBytes = toConnectionStates[toConnectionI].tcpWriteBytes;
                            Util.FlushBytes(usingBytes);
                            await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () =>
                            {
                                SignalCompressor.Compress(dataToSend, usingBytes, 1);
                                try
                                {
                                    await toConnections[toConnectionI].tcpStream.WriteAsync(usingBytes, 0, usingBytes.Length);

                                }
                                catch (SocketException e)
                                {
                                    Util.Exchange(ref toConnectionStates[toConnectionI].tcpWriteStateName, StateOfConnection.Uninitialized);
                                    isSyncingSuccessfully = false;
                                    Logging.Write("SyncSignalsToAllReliably: tcp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                }
                                catch (NullReferenceException e)
                                {
                                    Util.Exchange(ref toConnectionStates[toConnectionI].tcpWriteStateName, StateOfConnection.Uninitialized);
                                    isSyncingSuccessfully = false;
                                    Logging.Write("SyncSignalsToAllReliably: tcp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                }
                                catch (ObjectDisposedException e)
                                {
                                    Util.Exchange(ref toConnectionStates[toConnectionI].tcpWriteStateName, StateOfConnection.Uninitialized);
                                    isSyncingSuccessfully = false;
                                    Logging.Write("SyncSignalsToAllReliably: tcp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                }
                                catch (System.IO.IOException e)
                                {
                                    Util.Exchange(ref toConnectionStates[toConnectionI].tcpWriteStateName, StateOfConnection.Uninitialized);
                                    isSyncingSuccessfully = false;
                                    Logging.Write("SyncSignalsToAllReliably: tcp stream " + toConnectionI + " has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                                }
                                //signals[fromClientI][signalI].dataDirty = false; TODO need proper mechanism to sync this across threads
                            });
                        }
                    }
                }
            

                Util.CompareExchange(ref toConnectionStates[toConnectionI].tcpWriteStateName, StateOfConnection.ReadyToOperate, StateOfConnection.BeingOperated);
                await Task.Delay(60);
            }
        }

        public async static void ReceiveSignalsReliablyFromAll(IncomingSignal[][] signals, Func<bool> cancel, Action<string> report,IEnumerable<int> fromIndices, ConnectionAPIs[] fromStreams, ConnectionMetaData[] fromDatas, ConnectionState[] fromStates)
        {
            foreach(var index in fromIndices)
            {
                await Task.Run(() =>
                {
                    ReceiveSignalsReliablyFrom(signals, cancel, report, fromStreams, fromDatas, fromStates, index);
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
                    await Task.Delay(2000);
                    continue;
                }

                try
                {
                    var usingBytes = fromStates[streamI].tcpReadBytes;
                    Util.FlushBytes(usingBytes);

                    var bytesRead = await fromStreams[streamI].tcpStream.ReadAsync(usingBytes, 0, usingBytes.Length);
                    await SignalUpdaterUtil.WriteToIncomingSignals(signals, (string s) =>  report("from " + streamI + " " + s), fromStates[streamI].tcpReadBytes, new UdpReceiveResult(), fromDatas[streamI]);

                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.ReadyToOperate);
                }
                catch (ObjectDisposedException e)
                {
                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream " + streamI + " has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (SocketException e)
                {
                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream " + streamI + "  has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (FormatException e)
                {
                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream " + streamI + "  has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (System.IO.IOException e)
                {
                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream " + streamI + "  has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                
            }
        }
    }
}
