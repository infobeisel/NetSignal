using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSignal
{
    public class UnreliableSignalUpdater
    {
        //uses udp to sync signals unreliably
        public async static void SyncSignalsToAll(int withInd, ConnectionAPIs [] with, ConnectionMetaData [] connectionMetaData, ConnectionState [] connectionState, OutgoingSignal[][] signals, Action<string> report, Func<bool> cancel, ConnectionAPIs[] toAllApis, ConnectionMetaData[] toAllData, ConnectionState[] toAllStates)
        {
            bool useOwnUdpClient = toAllApis == null; // whether to use the udp client relying in with, or the udp client that has been set up for each connection
            for (int toConnectionI = 0; toConnectionI < toAllData.Length; toConnectionI++)
            {
                  await Task.Run(() =>
                   {
                    _ = SyncSignalsTo(withInd, with, connectionMetaData, connectionState, signals, report, toAllApis, toAllData, toAllStates, useOwnUdpClient, toConnectionI, cancel);
                  });
            }
                
        }

        

        private static async Task SyncSignalsTo(int withInd, ConnectionAPIs [] with, ConnectionMetaData[] connectionMetaData, ConnectionState [] connectionState, OutgoingSignal[][] signals, Action<string> report, ConnectionAPIs[] toAllApis, ConnectionMetaData[] toAllData, ConnectionState[] toAllStates, bool useOwnUdpClient, int toConnectionI, Func<bool> cancel)
        {
            try
            {
                Logging.Write("SyncSignalsTo on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
                while (!cancel())
                {
                    report("try to send ur lock");


                    StateOfConnection previousState = StateOfConnection.Uninitialized;
                    if (useOwnUdpClient)
                        previousState = Util.CompareExchange(ref connectionState[withInd].udpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);
                    else
                        previousState = Util.CompareExchange(ref toAllStates[toConnectionI].udpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                    if (!useOwnUdpClient && previousState == StateOfConnection.Uninitialized)
                        await Task.Delay(2000);//pause

                    if (previousState != StateOfConnection.ReadyToOperate)
                    {
                        await Task.Delay(2000);//pause
                        continue;
                    }
                    report("try to send ur to " + toConnectionI);

                    for (int fromConnectionI = 0; fromConnectionI < signals.Length; fromConnectionI++)
                    {
                        int fromClientId = useOwnUdpClient ? 
                            (fromConnectionI == toConnectionI ? connectionMetaData[fromConnectionI].clientID : -1) :
                            toAllData[fromConnectionI].clientID;
                        // Logging.Write("SyncSignalsToReliably: will try to sync to clientId " + fromClientId);

                        report("try to send ur to A" + toConnectionI);
                        if (fromClientId == -1)
                            continue;
                        report("try to send ur to B" + toConnectionI);

                        for (int signalI = 0; signalI < signals[fromClientId].Length; signalI++)
                        {
                            report("try to send ur to C" + signals[fromClientId][signalI] + " contnue? " + !cancel());
                            if (signals[fromClientId][signalI].dataDirty) //on server side: this can happen for every fromClientI, but on client side this should happen only for the local client, i.e. the local client should only write to its own outgoing signals
                            {
                                report("try to send ur to D" + toConnectionI);
                                var dataToSend = signals[fromClientId][signalI].data;
                                dataToSend.clientId = fromClientId; //make sure client id is correct;
                                dataToSend.index = signalI;
                                signals[fromClientId][signalI].data = dataToSend;

                                var toClient = toAllData[toConnectionI];
                                var udpClientToUse = useOwnUdpClient ? with[withInd].udpClient : toAllApis[toConnectionI].udpClient;

                                IPEndPoint toSendTo = new IPEndPoint(IPAddress.Parse(toClient.myIp), toClient.iListenToPort);

                                report("send data to " + toSendTo + " : " + dataToSend);

                                var usingBytes = useOwnUdpClient ? connectionState[withInd].udpWriteBytes : toAllStates[toConnectionI].udpWriteBytes;
                                Util.FlushBytes(usingBytes);
                                SignalCompressor.Compress(dataToSend, usingBytes, 1);
                                await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () =>
                                {
                                    try
                                    {
                                    //var lockObj = useOwnUdpClient ? connectionState.udpWriteLock : toAllStates[toClientI].udpWriteLock;
                                    await udpClientToUse.SendAsync(usingBytes, usingBytes.Length, toSendTo);

                                    }
                                    catch (SocketException e)
                                    {
                                        if (useOwnUdpClient)
                                            previousState = Util.Exchange(ref connectionState[withInd].udpWriteStateName, StateOfConnection.Uninitialized);
                                        else
                                            previousState = Util.Exchange(ref toAllStates[toConnectionI].udpWriteStateName, StateOfConnection.Uninitialized);
                                        report("SyncSignalsToAll: udp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                    }
                                    catch (ObjectDisposedException e)
                                    {
                                        if (useOwnUdpClient)
                                            previousState = Util.Exchange(ref connectionState[withInd].udpWriteStateName, StateOfConnection.Uninitialized);
                                        else
                                            previousState = Util.Exchange(ref toAllStates[toConnectionI].udpWriteStateName, StateOfConnection.Uninitialized);
                                        report("SyncSignalsToAll: udp client socket " + toConnectionI + " got closed, (unfortunately) this is intended behaviour, stop sending.");
                                    }
                                });


                            }
                            report("try to send ur to E" + signals);

                            //TODO need mechanism to exclude signal from being sent
                            //signals[fromClientI][signalI].dataDirty = false;
                        }
                    }
                    report("try to send ur to F");
                    if (useOwnUdpClient)
                        previousState = Util.Exchange(ref connectionState[withInd].udpWriteStateName, StateOfConnection.ReadyToOperate);
                    else
                        previousState = Util.Exchange(ref toAllStates[toConnectionI].udpWriteStateName, StateOfConnection.ReadyToOperate);

                    report("try to send ur to G");

                    await Task.Delay(30);
                    report("try to send ur to H");
                }
                Logging.Write("stop SyncSignalsTo " + toConnectionI + " on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            } catch (Exception e)
            {
                report(e.Message);
            }
        }

        
        public async static void ReceiveSignals(int withInd, ConnectionAPIs [] connection, ConnectionMetaData [] connectionData, ConnectionState [] connectionState, IncomingSignal[][] signals, Func<bool> cancel, Action<string> report, params ConnectionMetaData[] from)
        {
            var usingBytes = connectionState[withInd].udpReadBytes;
            Logging.Write("ReceiveSignals on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            try
            {
                while (!cancel())
                {
                    
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
                        await SignalUpdaterUtil.WriteToIncomingSignals(signals, report, bytes, receiveResult, from);
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
