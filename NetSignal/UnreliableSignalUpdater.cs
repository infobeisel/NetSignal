using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSignal
{
    public class UnreliableSignalUpdater
    {
        //uses udp to sync signals unreliably
        public async static void SyncSignalsToAll(ConnectionAPIs with, ConnectionMetaData connectionMetaData, ConnectionState connectionState, OutgoingSignal[][] signals, Action<string> report, Func<bool> cancel, ConnectionAPIs[] toAllApis, ConnectionMetaData[] toAllData, ConnectionState[] toAllStates)
        {
            bool useOwnUdpClient = toAllApis == null; // whether to use the udp client relying in with, or the udp client that has been set up for each connection
            for (int toClientI = 0; toClientI < toAllData.Length; toClientI++)
            {
               await Task.Run(() =>
               {
                   _ = SyncSignalsTo(with, connectionState, signals, report, toAllApis, toAllData, toAllStates, useOwnUdpClient, toClientI, cancel);
               });
            }
                
        }

        private static async Task SyncSignalsTo(ConnectionAPIs with, ConnectionState connectionState, OutgoingSignal[][] signals, Action<string> report, ConnectionAPIs[] toAllApis, ConnectionMetaData[] toAllData, ConnectionState[] toAllStates, bool useOwnUdpClient, int toClientI, Func<bool> cancel)
        {
            Logging.Write("SyncSignalsTo on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            while (!cancel())
            {
                StateOfConnection previousState = StateOfConnection.Uninitialized;
                if (useOwnUdpClient)
                    previousState = Util.CompareExchange(ref connectionState.udpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);
                else
                    previousState = Util.CompareExchange(ref toAllStates[toClientI].udpWriteStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);

                if (previousState != StateOfConnection.ReadyToOperate)
                {
                    await Task.Delay(30);
                    continue;
                }

                for (int fromClientI = 0; fromClientI < signals.Length; fromClientI++)
                    for (int signalI = 0; signalI < signals[fromClientI].Length; signalI++)
                    {
                        if (signals[fromClientI][signalI].dataDirty) //on server side: this can happen for every fromClientI, but on client side this should happen only for the local client, i.e. the local client should only write to its own outgoing signals
                        {
                            

                            var toClient = toAllData[toClientI];
                            var udpClientToUse = useOwnUdpClient ? with.udpClient : toAllApis[toClientI].udpClient;

                            IPEndPoint toSendTo = new IPEndPoint(IPAddress.Parse(toClient.myIp), toClient.iListenToPort);

                            report("send data to " + toSendTo + " : " + signals[fromClientI][signalI].data);

                            var usingBytes = useOwnUdpClient ? connectionState.udpWriteBytes : toAllStates[toClientI].udpWriteBytes;
                            Util.FlushBytes(usingBytes);
                            SignalCompressor.Compress(signals[fromClientI][signalI].data, usingBytes, 1);
                            await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () =>
                            {
                                try
                                {
                                    await udpClientToUse.SendAsync(usingBytes, usingBytes.Length, toSendTo);
                                    if (useOwnUdpClient)
                                        previousState = Util.Exchange(ref connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate);
                                    else
                                        previousState = Util.Exchange(ref toAllStates[toClientI].udpWriteStateName, StateOfConnection.ReadyToOperate);

                                }
                                catch (SocketException e)
                                {
                                    if (useOwnUdpClient)
                                        previousState = Util.Exchange(ref connectionState.udpWriteStateName, StateOfConnection.Uninitialized);
                                    else
                                        previousState = Util.Exchange(ref toAllStates[toClientI].udpWriteStateName, StateOfConnection.Uninitialized);
                                    Logging.Write("SyncSignalsToAll: udp client socket got closed, (unfortunately) this is intended behaviour, stop sending.");
                                }
                                catch (ObjectDisposedException e)
                                {
                                    if (useOwnUdpClient)
                                        previousState = Util.Exchange(ref connectionState.udpWriteStateName, StateOfConnection.Uninitialized);
                                    else
                                        previousState = Util.Exchange(ref toAllStates[toClientI].udpWriteStateName, StateOfConnection.Uninitialized);
                                    Logging.Write("SyncSignalsToAll: udp client socket got closed, (unfortunately) this is intended behaviour, stop sending.");
                                }
                            });

                            
                        }
                        //TODO need mechanism to exclude signal from being sent
                        //signals[fromClientI][signalI].dataDirty = false;
                    }


                

                await Task.Delay(30);
            }
            Logging.Write("stop SyncSignalsTo on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
        }

        
        public async static void ReceiveSignals(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState, IncomingSignal[][] signals, Func<bool> cancel, Action<string> report, params ConnectionMetaData[] from)
        {
            var usingBytes = connectionState.udpReadBytes;
            Logging.Write("ReceiveSignals on thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            try
            {
                while (!cancel())
                {
                    
                    var previousState = Util.CompareExchange(ref connectionState.udpReadStateName, StateOfConnection.BeingOperated, StateOfConnection.ReadyToOperate);
                    if (previousState != StateOfConnection.ReadyToOperate)
                    {
                        await Task.Delay(1000); //sth is wrong with this connection
                        continue;
                    }
                    //dont know a better way: receive async does not accept cancellation tokens, so need to let it fail here (because some other disposed the udpclient)
                    UdpReceiveResult receiveResult;
                    try
                    {
                        receiveResult = await connection.udpClient.ReceiveAsync();
                        var bytes = receiveResult.Buffer;
                        await SignalUpdaterUtil.WriteToIncomingSignals(signals, report, bytes, receiveResult, from);
                        Util.Exchange(ref connectionState.udpReadStateName, StateOfConnection.ReadyToOperate);
                    }
                    catch (ObjectDisposedException e)
                    {
                        Util.Exchange(ref connectionState.udpReadStateName, StateOfConnection.Uninitialized);
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
