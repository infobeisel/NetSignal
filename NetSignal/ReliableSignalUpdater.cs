using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSignal
{
    public class ReliableSignalUpdater
    {
       

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

        private async static void SyncSignalsToReliably(OutgoingSignal[][] signals, Func<bool> cancel, ConnectionAPIs[] toConnections, ConnectionMetaData[] toConnectionsDatas, ConnectionState[] toConnectionStates, int toConnectionI)
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

                bool isSyncingSuccessfully = true;

                for (int fromConnectionI = 0; fromConnectionI < signals.Length && isSyncingSuccessfully; fromConnectionI++)
                {
                    int fromClientId = toConnectionsDatas[fromConnectionI].clientID;

                    for (int signalI = 0; signalI < signals[fromClientId].Length && isSyncingSuccessfully; signalI++)
                    {
                        
                        
                        if (signals[fromClientId][signalI].dataDirty)
                        {
                            var dataToSend = signals[fromClientId][signalI].data;
                            dataToSend.clientId = fromClientId; //make sure client id is correct;
                            signals[fromClientId][signalI].data = dataToSend;


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
                /*
                if(!isSyncingSuccessfully) //failed syncing, need to delete saved connection info
                {
                    var shouldDeleteKey = mapping.ClientIdentificationToEndpoint?.ContainsKey(toI);
                    if (shouldDeleteKey.HasValue && shouldDeleteKey.Value)
                    {
                        var endp = mapping.ClientIdentificationToEndpoint[toI];
                        mapping.ClientIdentificationToEndpoint.Remove(toI);
                        mapping.EndPointToClientIdentification.Remove(endp);
                    }
                }*/
                

                Util.CompareExchange(ref toConnectionStates[toConnectionI].tcpWriteStateName, StateOfConnection.ReadyToOperate, StateOfConnection.BeingOperated);
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
