using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSignal
{
    public class UnreliableSignalUpdater
    {
        //uses udp to sync signals unreliably
        public async static void SyncSignalsToAll(OutgoingSignal[][] signals,TimeControl timeControl,  Action<string> report, Func<bool> cancel, ConnectionAPIs[] toAllApis, ConnectionMetaData[] toAllData, ConnectionState[] toAllStates, IEnumerable<int> toIndices)
        {
            foreach(var toConnectionI in toIndices)
            {
                  await Task.Run(() =>
                   {
                    _ = SyncSignalsTo(signals, timeControl,  report, toAllApis, toAllData, toAllStates, toConnectionI, cancel);
                  });
            }
                
        }

        

        private static async Task SyncSignalsTo(OutgoingSignal[][] signals, TimeControl timeControl, Action<string> report, ConnectionAPIs[] toAllApis, ConnectionMetaData[] toAllData, ConnectionState[] toAllStates, int toConnectionI, Func<bool> cancel)
        {
            try
            {
                Logging.Write("SyncSignalsTo on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                while (!cancel())
                {
                    StateOfConnection previousState = StateOfConnection.Uninitialized;
                    bool isConActive = false;
                    isConActive = toAllStates[toConnectionI].isConnectionActive;
                    if (!isConActive)
                    {
                        await Task.Delay(2000);
                        continue;
                    }
                    previousState = Util.CompareExchange(ref toAllStates[toConnectionI].udpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);
                    //report("try to send ur lock test " + toConnectionI + " , " + isConActive + " , " + previousState);
                    if (previousState == StateOfConnection.Uninitialized)
                        await Task.Delay(2000);//pause

                    if (previousState != StateOfConnection.ReadyToOperate || !isConActive)
                    {
                        await Task.Delay(2000);//pause
                        continue;
                    }
                    //report("try to send ur to " + toConnectionI);
                    //responsible for sending everything to one connection
                    //for each signal connection
                    var historyIndex = SignalUpdaterUtil.CurrentHistoryIndex(timeControl);
                    for (int fromConnectionI = 0; fromConnectionI < toAllData.Length; fromConnectionI++)
                    {
                        int fromClientId = fromConnectionI;//the clientId corresponds to the index in the connection array //toAllStates[fromConnectionI].clientID;
                        //report("try to send ur to A" + toConnectionI);
                        if (!toAllStates[fromConnectionI].isConnectionActive) //inactive connection
                            continue;
                        //report("try to send ur to B" + toConnectionI);
                        //check integrity
                        for (int signalI = 0; signalI < signals[fromClientId].Length; signalI++)
                        {
                            //report("try to send ur to C" + signals[fromClientId][signalI] + " contnue? " + !cancel());
                            if (signals[fromClientId][signalI].dataDirty) //on server side: this can happen for every fromClientI, but on client side this should happen only for the local client, i.e. the local client should only write to its own outgoing signals
                            {
                                var dataToSend = signals[fromClientId][signalI].data;
                                dataToSend.clientId = fromClientId; //make sure client id is correct;
                                dataToSend.index = signalI;
                                dataToSend.timeStamp = new DateTime(timeControl.CurrentTimeTicks);
                                if(signalI == 0 && dataToSend.signalType != SignalType.UDPAlive)
                                {
                                    //Logging.Write("the signal with index 0 is reserved for udp keepalive, please dont use it for game specific data");
                                    dataToSend.signalType = SignalType.UDPAlive;
                                }
                                signals[fromClientId][signalI].data = dataToSend;
                            }
                        }
                        //for (int signalI = 0; signalI < signals[fromClientId].Length; signalI++)
                        {
                            //report("try to send ur to C" + signals[fromClientId][signalI] + " contnue? " + !cancel());
                            //if (signals[fromClientId][signalI].dataDirty) //on server side: this can happen for every fromClientI, but on client side this should happen only for the local client, i.e. the local client should only write to its own outgoing signals
                            {
                                //report("try to send ur to D" + toConnectionI);
                                

                                var toAddressData = toAllData[toConnectionI];
                                var udpClientToUse = toAllApis[toConnectionI].udpClient;

                                IPEndPoint toSendTo = new IPEndPoint(IPAddress.Parse(toAddressData.myIp), toAddressData.iListenToPort);


                                var usingBytes = toAllStates[toConnectionI].udpWriteBytes;
                                Util.FlushBytes(usingBytes);
                                
                                int compressedSignalCount = SignalCompressor.Compress(signals[fromClientId], 0, usingBytes, 1);
                                report("send data to " + toSendTo + " : " + compressedSignalCount);

                                //SignalCompressor.Compress(dataToSend, usingBytes, 1);


                                await MessageDeMultiplexer.MarkSignal(SignalType.Data, usingBytes, async () =>
                                {
                                    try
                                    {
                                        //var lockObj = useOwnUdpClient ? connectionState.udpWriteLock : toAllStates[toClientI].udpWriteLock;
                                        await udpClientToUse.SendAsync(usingBytes, usingBytes.Length, toSendTo);
                                    }
                                    catch (SocketException e)
                                    {
                                        previousState = Util.Exchange(ref toAllStates[toConnectionI].udpWriteStateName, StateOfConnection.Uninitialized);
                                        report("SyncSignalsToAll: udp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                    }
                                    catch (ObjectDisposedException e)
                                    {
                                        previousState = Util.Exchange(ref toAllStates[toConnectionI].udpWriteStateName, StateOfConnection.Uninitialized);
                                        report("SyncSignalsToAll: udp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                    }
                                });
                            }
                            //report("try to send ur to E" + signals);
                            //TODO need mechanism to exclude signal from being sent
                            //signals[fromClientI][signalI].dataDirty = false;
                        }
                    }
                    //report("try to send ur to F");
                    previousState = Util.Exchange(ref toAllStates[toConnectionI].udpWriteStateName, StateOfConnection.ReadyToOperate);
                    //report("try to send ur to G");
                    await Task.Delay(30);
                    //report("try to send ur to H");
                }
                Logging.Write("stop SyncSignalsTo " + toConnectionI + " on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            } catch (Exception e)
            {
                Console.Write("-------------------------------------------------------------------------------------");
                Console.Write(e.Message);
                Console.Write("-------------------------------------------------------------------------------------");
            }
        }

        
        public async static void ReceiveSignals(int withInd, ConnectionAPIs [] connection, ConnectionMetaData [] connectionData, ConnectionState [] connectionState, IncomingSignal[][][] signals, TimeControl timeControl, Func<bool> cancel, Action<string> report, params ConnectionMetaData[] from)
        {
            var usingBytes = connectionState[withInd].udpReadBytes;
            Logging.Write("ReceiveSignals on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            try
            {
                while (!cancel())
                {

                    bool isConActive = false;
                    isConActive = connectionState[withInd].isConnectionActive;
                    if (!isConActive)
                    {
                        await Task.Delay(2000);
                        continue;
                    }

                    var previousState = Util.CompareExchange(ref connectionState[withInd].udpReadStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                    if (previousState == StateOfConnection.Uninitialized)
                        await Task.Delay(2000);//pause

                    if (previousState != StateOfConnection.ReadyToOperate)
                    {
                        await Task.Delay(2000);
                        continue;
                    }
                    //dont know a better way: receive async does not accept cancellation tokens, so need to let it fail here (because some other disposed the udpclient)
                    UdpReceiveResult receiveResult;
                    try
                    {
                        receiveResult = await connection[withInd].udpClient.ReceiveAsync();
                        var bytes = receiveResult.Buffer;
                        await SignalUpdaterUtil.WriteToIncomingSignals(signals, timeControl, (string s) => Logging.Write(s), bytes, receiveResult,
                            (int c,int h,int s) => {
                                if (s == 0)
                                {
                                    if(c >= 0 && c < from.Length)
                                    {
                                        
                                        from[c].iListenToPort = receiveResult.RemoteEndPoint.Port;
                                        from[c].myIp = receiveResult.RemoteEndPoint.Address.ToString();
                                        Logging.Write("udp endpoint update " + receiveResult.RemoteEndPoint.ToString() );
                                    }
                                    
                                }
                            }, from);
                        Util.Exchange(ref connectionState[withInd].udpReadStateName, StateOfConnection.ReadyToOperate);
                    }
                    catch (ObjectDisposedException e)
                    {
                        Util.Exchange(ref connectionState[withInd].udpReadStateName, StateOfConnection.Uninitialized);
                        Logging.Write("ReceiveSignals: udp socket has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                    }
                    catch (SocketException e)
                    {
                        Util.Exchange(ref connectionState[withInd].udpReadStateName, StateOfConnection.Uninitialized);
                        Logging.Write("ReceiveSignals: udp socket has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                    }

                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }
            finally
            {
                Logging.Write("stop ReceiveSignals on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                //connectionState.udpStateName = StateOfConnection.ReadyToOperate;
            }
        }
    }
}
