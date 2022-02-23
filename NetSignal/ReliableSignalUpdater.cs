using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSignal
{
    public class ReliableSignalUpdater
    {
       

        public async static void SyncSignalsToAllReliablyAndTrackIsConnected(OutgoingSignal[][][] signals, TimeControl timeControl, Func<bool> cancel, Action<string> report, IEnumerable<int> toAllIndices, ConnectionAPIs[] toConnections, ConnectionMetaData[] toConnectionsDatas, ConnectionState[] toConnectionStates)
        {
            foreach(var ind in toAllIndices)
            {
                await Task.Run(() =>
                {
                    SyncSignalsToReliably(signals, timeControl, cancel, report, toConnections, toConnectionsDatas, toConnectionStates, ind);
                });
            }
        }

        private async static void SyncSignalsToReliably(OutgoingSignal[][][] signals, TimeControl timeControl, Func<bool> cancel, Action<string> report, ConnectionAPIs[] toConnections, ConnectionMetaData[] toConnectionsDatas, ConnectionState[] toConnectionStates, int toConnectionI)
        {
            while (!cancel())
            {

                

                var isConActive = toConnections[toConnectionI].tcpClient == null ? false : toConnections[toConnectionI].tcpClient.Connected;
                if (!isConActive)
                {
                    ConnectionUpdater.TearDownTcpOfClient(toConnections, toConnectionStates, toConnectionI);
                    await Task.Delay(2000);
                    continue;
                }

                var previousState = Util.CompareExchange(ref toConnectionStates[toConnectionI].tcpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                if (previousState == StateOfConnection.Uninitialized)
                    await Task.Delay(2000);//pause

                if (previousState != StateOfConnection.ReadyToOperate)
                {
                    await Task.Delay(2000);
                    continue;
                }
                //report("try to send r to " + toConnectionI + " , " + toConnections[toConnectionI].tcpClient.Connected);

                bool isSyncingSuccessfully = true;
                var historyIndex = SignalUpdaterUtil.CurrentHistoryIndex(timeControl);
               // Logging.Write("SyncSignalsToReliably: willtoConnectionsDatas.Length " + toConnectionsDatas.Length);
                for (int fromConnectionI = 0; fromConnectionI < toConnectionsDatas.Length && isSyncingSuccessfully; fromConnectionI++)
                {
                    int fromClientId = fromConnectionI;

                    if (!toConnectionStates[fromConnectionI].isConnectionActive) //inactive connection
                        continue;

                    for (int signalI = 0; signalI < signals[fromClientId][historyIndex].Length && isSyncingSuccessfully; signalI++)
                    {
                        
                        
                        if (signals[fromClientId][historyIndex][signalI].dataDirty)
                        {
                            var dataToSend = signals[fromClientId][historyIndex][signalI].data;
                            dataToSend.clientId = fromClientId; //make sure client id is correct;
                            dataToSend.index = signalI;
                            //dataToSend.timeStamp = new DateTime(timeControl.CurrentTimeTicks);

                            if (signalI == 0 && dataToSend.signalType != SignalType.TCPAlive)
                            {
                                Logging.Write("the signal with index 0 is reserved for tcp keepalive, please dont use it for game specific data");
                                dataToSend.signalType = SignalType.TCPAlive;
                            }
                            signals[fromClientId][historyIndex][signalI].data = dataToSend;

                            report("send data to " + toConnectionI + " : " + dataToSend);

                            var usingBytes = toConnectionStates[toConnectionI].tcpWriteBytes;
                            Util.FlushBytes(usingBytes);
                            await MessageDeMultiplexer.MarkSignal(dataToSend.signalType, usingBytes, async () =>
                            {
                                SignalCompressor.Compress(dataToSend, usingBytes, 1);
                                try
                                {
                                    await toConnections[toConnectionI].tcpStream.WriteAsync(usingBytes, 0, usingBytes.Length);

                                }
                                catch (SocketException e)
                                {
                                    ConnectionUpdater.TearDownTcpOfClient(toConnections, toConnectionStates, toConnectionI);
                                    isSyncingSuccessfully = false;
                                    Logging.Write("SyncSignalsToAllReliably: tcp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                }
                                catch (NullReferenceException e)
                                {
                                    ConnectionUpdater.TearDownTcpOfClient(toConnections, toConnectionStates, toConnectionI);
                                    isSyncingSuccessfully = false;
                                    Logging.Write("SyncSignalsToAllReliably: tcp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                }
                                catch (ObjectDisposedException e)
                                {
                                    ConnectionUpdater.TearDownTcpOfClient(toConnections, toConnectionStates, toConnectionI);
                                    isSyncingSuccessfully = false;
                                    Logging.Write("SyncSignalsToAllReliably: tcp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                }
                                catch (System.IO.IOException e)
                                {
                                    ConnectionUpdater.TearDownTcpOfClient(toConnections, toConnectionStates, toConnectionI);
                                    isSyncingSuccessfully = false;
                                    Logging.Write("SyncSignalsToAllReliably: tcp stream " + toConnectionI + " has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                                }
                                //signals[fromClientI][signalI].dataDirty = false; TODO need proper mechanism to sync this across threads
                            });
                        }
                    }
                }
            

                Util.CompareExchange(ref toConnectionStates[toConnectionI].tcpWriteStateName, StateOfConnection.ReadyToOperate, StateOfConnection.BeingOperated);
                await Task.Delay(30);
            }
        }

        public async static void ReceiveSignalsReliablyFromAllAndTrackIsConnected(IncomingSignal[][][] signals, TimeControl timeControl, Func<bool> cancel, Action<string> report,IEnumerable<int> fromIndices, ConnectionAPIs[] fromStreams, ConnectionMetaData[] fromDatas, ConnectionState[] fromStates)
        {
            foreach(var index in fromIndices)
            {
                await Task.Run(() =>
                {
                    ReceiveSignalsReliablyFromAndTrackIsConnected(signals, timeControl, cancel, report, fromStreams, fromDatas, fromStates, index);
                });
            }
        }

        //uses tcp to sync signals reliably
        private async static void ReceiveSignalsReliablyFromAndTrackIsConnected(IncomingSignal[][][] signals, TimeControl timeControl, Func<bool> cancel, Action<string> report, ConnectionAPIs[] fromStreams, ConnectionMetaData[] fromDatas, ConnectionState[] fromStates, int streamI)
        {
            while (!cancel())
            {

                var isConActive = fromStreams[streamI].tcpClient == null ? false : fromStreams[streamI].tcpClient.Connected;
                if (!isConActive)
                {
                    ConnectionUpdater.TearDownTcpOfClient(fromStreams, fromStates, streamI);

                    await Task.Delay(2000);
                    continue;
                }

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
                    await SignalUpdaterUtil.WriteToIncomingSignals(signals, timeControl, (string s) =>  report("from " + streamI + " " + s), fromStates[streamI].tcpReadBytes, new UdpReceiveResult(), fromDatas[streamI]);

                    Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.ReadyToOperate);
                }
                catch (ObjectDisposedException e)
                {
                    ConnectionUpdater.TearDownTcpOfClient(fromStreams, fromStates, streamI);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream " + streamI + " has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (SocketException e)
                {
                    ConnectionUpdater.TearDownTcpOfClient(fromStreams, fromStates, streamI);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream " + streamI + "  has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (FormatException e)
                {
                    ConnectionUpdater.TearDownTcpOfClient(fromStreams, fromStates, streamI);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream " + streamI + "  has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                catch (System.IO.IOException e)
                {
                    ConnectionUpdater.TearDownTcpOfClient(fromStreams, fromStates, streamI);
                    Logging.Write("ReceiveSignalsReliablyFrom: tcp stream " + streamI + "  has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                }
                
            }
        }

        
    }
}
