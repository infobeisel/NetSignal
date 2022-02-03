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
        public async static void SyncSignalsToAll(ConnectionAPIs with, ConnectionMetaData connectionMetaData, ConnectionState connectionState, OutgoingSignal[][] signals,Action<string> report, Func<bool> cancel, params ConnectionMetaData[] all)
        {
            try
            {
                while (!cancel())
                {
                    for (int fromClientI = 0; fromClientI < signals.Length; fromClientI++)
                        for (int signalI = 0; signalI < signals[fromClientI].Length; signalI++)
                        {
                            if (signals[fromClientI][signalI].dataDirty) //on server side: this can happen for every fromClientI, but on client side this should happen only for the local client, i.e. the local client should only write to its own outgoing signals
                            {
                                var previousState = Util.CompareExchange(ref connectionState.udpStateName, StateOfConnection.ReadyToOperate, StateOfConnection.ReadyToOperate);

                                if (previousState != StateOfConnection.ReadyToOperate)
                                {
                                    await Task.Delay(1);
                                    continue;
                                }

                                foreach (var toClient in all)
                                {
                                    IPEndPoint toSendTo = new IPEndPoint(IPAddress.Parse(toClient.myIp), toClient.iListenToPort);

                                    

                                    //make sure correct metadata is written. TODO VERIFY
                                    /*var modified = signals[fromClientI][signalI].data;
                                    modified.clientId = fromClientI;
                                    modified.index = signalI;
                                    signals[fromClientI][signalI].data = modified;*/

                                    report("send data to " + toSendTo + " : " + signals[fromClientI][signalI].data);

                                    var dataStr = SignalCompressor.Compress(signals[fromClientI][signalI].data);
                                    //Logging.Write("will send " + dataStr);
                                    var usingBytes = connectionState.udpWriteBytes;
                                    Util.FlushBytes(usingBytes);
                                    await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () =>
                                    {
                                        Encoding.ASCII.GetBytes(dataStr, 0, dataStr.Length, usingBytes, 1);
                                        try
                                        {
                                            await with.udpClient.SendAsync(usingBytes, usingBytes.Length, toSendTo);
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
                                }
                                signals[fromClientI][signalI].dataDirty = false;

                                Util.Exchange(ref connectionState.udpStateName, StateOfConnection.ReadyToOperate);
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
                    var previousState = Util.CompareExchange(ref connectionState.udpStateName, StateOfConnection.ReadyToOperate, StateOfConnection.ReadyToOperate);
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
                        Util.Exchange(ref connectionState.udpStateName, StateOfConnection.Uninitialized);
                        Logging.Write("ReceiveSignals: udp socket has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                        continue;
                    }
                    var bytes = receiveResult.Buffer;
                    await SignalUpdaterUtil.WriteToIncomingSignals(signals, report, bytes, receiveResult, from);
                    Util.Exchange(ref connectionState.udpStateName, StateOfConnection.ReadyToOperate);
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
