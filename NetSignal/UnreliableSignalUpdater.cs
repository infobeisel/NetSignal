using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSignal
{
    public class UnreliableSignalUpdater
    {
        


        //uses udp to sync signals unreliably
        public async static void SyncSignalsToAll(ConnectionAPIs with, ConnectionMetaData connectionMetaData, ConnectionState connectionState, OutgoingSignal[][] signals,Action<string> report, Func<bool> cancel, ConnectionAPIs [] toAllApis, ConnectionMetaData[] toAllData, ConnectionState [] toAllStates)
        {
            bool useOwnUdpClient = toAllApis == null; // whether to use the udp client relying in with, or the udp client that has been set up for each connection
            try
            {
                while (!cancel())
                {
                    for (int fromClientI = 0; fromClientI < signals.Length; fromClientI++)
                        for (int signalI = 0; signalI < signals[fromClientI].Length; signalI++)
                        {
                            if (signals[fromClientI][signalI].dataDirty) //on server side: this can happen for every fromClientI, but on client side this should happen only for the local client, i.e. the local client should only write to its own outgoing signals
                            {

                                for (int toClientI = 0; toClientI  < toAllData.Length; toClientI++)
                                {
                                    TODO: if set to beingoperated (as it should be) , the server never ever sends anything because it has already locked for listening. use two udp clients?
                                    

                                    StateOfConnection previousState = StateOfConnection.Uninitialized;
                                    if(useOwnUdpClient)
                                        previousState = Util.CompareExchange(ref connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate, StateOfConnection.ReadyToOperate);
                                    else
                                        previousState = Util.CompareExchange(ref toAllStates[toClientI].udpWriteStateName, StateOfConnection.ReadyToOperate, StateOfConnection.ReadyToOperate);


                                    if (previousState != StateOfConnection.ReadyToOperate)
                                    {
                                        await Task.Delay(1);
                                        continue;
                                    }

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
                                        }
                                        catch (SocketException e)
                                        {
                                            Logging.Write("SyncSignalsToAll: udp client socket got closed, (unfortunately) this is intended behaviour, stop sending.");
                                        }
                                        catch (ObjectDisposedException e)
                                        {
                                            Logging.Write("SyncSignalsToAll: udp client socket got closed, (unfortunately) this is intended behaviour, stop sending.");
                                        }

                                    });

                                    if (useOwnUdpClient)
                                        previousState = Util.Exchange(ref connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate);
                                    else
                                        previousState = Util.Exchange(ref toAllStates[toClientI].udpWriteStateName, StateOfConnection.ReadyToOperate);

                                }
                                signals[fromClientI][signalI].dataDirty = false;

                                
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

        public async static void SyncIncomingToOutgoingSignals(IncomingSignal[][] incomingSignals, OutgoingSignal[][] outgoingSignals, Func<bool> cancel)
        {
            if (incomingSignals.Length != outgoingSignals.Length)
                throw new Exception("incoming and outgoing array length unequal");

            var clientCount = Math.Min(incomingSignals.Length, outgoingSignals.Length);
            try
            {
                while (!cancel())
                {
                    for (int fromClientI = 0; fromClientI < clientCount; fromClientI++)
                        for (int signalI = 0; signalI < Math.Min(incomingSignals[fromClientI].Length, outgoingSignals[fromClientI].Length); signalI++)
                        {
                            if (incomingSignals[fromClientI][signalI].dataHasBeenUpdated)
                            {
                                for (int toClientI = 0; toClientI < clientCount; toClientI++)
                                {
                                    if (toClientI != fromClientI) //dont send to self
                                    {
                                        outgoingSignals[toClientI][signalI].data = incomingSignals[fromClientI][signalI].data;
                                        incomingSignals[toClientI][signalI].dataHasBeenUpdated = false;
                                    }
                                }
                            }
                        }
                    await Task.Delay(1);
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

        public async static void ReceiveSignals(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState, IncomingSignal[][] signals, Func<bool> cancel, Action<string> report, params ConnectionMetaData [] from)
        {
            var usingBytes = connectionState.udpReadBytes;
            try
            {
                while (!cancel())
                {
                    var previousState = Util.CompareExchange(ref connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate, StateOfConnection.ReadyToOperate);
                    if (previousState != StateOfConnection.ReadyToOperate)
                    {
                        await Task.Delay(1);
                        continue;
                    }
                    //dont know a better way: receive async does not accept cancellation tokens, so need to let it fail here (because some other disposed the udpclient)
                    UdpReceiveResult receiveResult;
                    try
                    {
                        receiveResult = await connection.udpClient.ReceiveAsync();
                    }
                    catch (ObjectDisposedException e)
                    {
                        Util.Exchange(ref connectionState.udpWriteStateName, StateOfConnection.Uninitialized);
                        Logging.Write("ReceiveSignals: udp socket has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                        continue;
                    }
                    var bytes = receiveResult.Buffer;
                    await SignalUpdaterUtil.WriteToIncomingSignals(signals, report, bytes, receiveResult, from);
                    Util.Exchange(ref connectionState.udpWriteStateName, StateOfConnection.ReadyToOperate);
                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }
            finally
            {
                //connectionState.udpStateName = StateOfConnection.ReadyToOperate;
            }
        }

    }
}
