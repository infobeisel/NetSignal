using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSignal
{
    public class SignalUpdater
    {
        private static AsyncCallback MakeHandleReceiveReliableSignal(IncomingSignal[][] signals, ConnectionAPIs connection, ConnectionMetaData metaData, ConnectionState connectionState, Action<string> report)
        {
            return async (IAsyncResult ar) =>
            {
                try
                {
                    var byteCountRead = connection.tcpStream.EndRead(ar);
                    await WriteToIncomingSignals(metaData, signals, report, connectionState.tcpReadBytes);

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

                        //it was previously uninit, well then write uninit and leave
                        if (previousState == StateOfConnection.Uninitialized)
                        {
                            Util.Exchange(ref fromStates[streamI].tcpReadStateName, StateOfConnection.Uninitialized);
                            continue;
                        }
                        //it was previously busy, can not continue with that
                        if (previousState == StateOfConnection.BeingOperated)
                        {
                            continue;
                        }

                        Logging.Write("ReceiveSignalsReliably: eligible for begin read?" + fromStates[streamI].tcpReadStateName.ToString());
                        try
                        {
                            Logging.Write("ReceiveSignalsReliably: begin read tcp stream of index " + streamI);
                            var usingBytes = fromStates[streamI].tcpReadBytes;
                            Util.FlushBytes(usingBytes);
                            Logging.Write("ReceiveSignalsReliably");
                            fromStreams[streamI].tcpStream.BeginRead(usingBytes, 0, usingBytes.Length, MakeHandleReceiveReliableSignal(signals, fromStreams[streamI], fromDatas[streamI], fromStates[streamI], report), null);
                        }
                        catch (ObjectDisposedException e)
                        {
                            Logging.Write("ReceiveSignals: tcp stream has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                            continue;
                        }
                    }
                    await Task.Delay(1);
                }
            }
            catch (SocketException e)
            {
                Logging.Write(e);
            }
        }

        //uses udp to sync signals unreliably
        public async static void SyncSignalsToAll(ConnectionAPIs with, ConnectionMetaData connectionMetaData, ConnectionState connectionState, OutgoingSignal[][] signals, Func<bool> cancel, params ConnectionMetaData[] all)
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
                                //TODO: never reached!!
                                var previousState = Util.CompareExchange(ref connectionState.udpStateName, StateOfConnection.ReadyToOperate, StateOfConnection.ReadyToOperate);

                                //it was previously uninit or not ready to operate
                                if (previousState != StateOfConnection.ReadyToOperate)
                                {
                                    //Util.Exchange(ref connectionState.udpStateName, StateOfConnection.Uninitialized);
                                    continue;
                                }
                                foreach (var toClient in all)
                                {
                                    IPEndPoint toSendTo = new IPEndPoint(IPAddress.Parse(toClient.myIp), toClient.iListenToPort);

                                    Logging.Write("data is dirty. send it to " + toSendTo);

                                    //make sure correct metadata is written. TODO VERIFY
                                    var modified = signals[fromClientI][signalI].data;
                                    modified.clientId = fromClientI;
                                    modified.index = signalI;
                                    signals[fromClientI][signalI].data = modified;

                                    var dataStr = SignalCompressor.Compress(signals[fromClientI][signalI].data);
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
                                        
                                    });
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

                                    //it was previously uninit, well then write uninit and leave
                                    if (previousState == StateOfConnection.Uninitialized)
                                    {
                                        //Util.Exchange(ref toConnectionStates[connectionI].tcpWriteStateName, StateOfConnection.Uninitialized);
                                        continue;
                                    }
                                    //it was previously busy, can not continue with that
                                    if (previousState == StateOfConnection.BeingOperated)
                                    {
                                        continue;
                                    }

                                    Logging.Write("SyncSignalsToAllReliably: eligible for begin write?" + toConnectionStates[connectionI].tcpWriteStateName.ToString());
                                    Logging.Write("data is dirty. send it reliably");

                                    //make sure correct metadata is written. TODO VERIFY
                                    var modified = signals[fromClientI][signalI].data;
                                    modified.clientId = fromClientI;
                                    modified.index = signalI;
                                    signals[fromClientI][signalI].data = modified;

                                    var dataStr = SignalCompressor.Compress(signals[fromClientI][signalI].data);
                                    var usingBytes = toConnectionStates[connectionI].tcpWriteBytes;
                                    Util.FlushBytes(usingBytes);
                                    await MessageDeMultiplexer.MarkFloatSignal(usingBytes, async () =>
                                    {
                                        Encoding.ASCII.GetBytes(dataStr, 0, dataStr.Length, usingBytes, 1);
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

        public async static void ReceiveSignals(ConnectionAPIs connection, ConnectionMetaData connectionData, ConnectionState connectionState, IncomingSignal[][] signals, Func<bool> cancel, Action<string> report)
        {
            //TODO currently unused
            var usingBytes = connectionState.udpReadBytes;
            try
            {
                while (!cancel())
                {
                    var previousState = Util.CompareExchange(ref connectionState.udpStateName, StateOfConnection.ReadyToOperate, StateOfConnection.ReadyToOperate);

                    //it was previously uninit, well then write uninit and leave
                    if (previousState != StateOfConnection.ReadyToOperate)
                    {
                        //Util.Exchange(ref connectionState.udpStateName, StateOfConnection.Uninitialized);
                        await Task.Delay(1);
                        continue;
                    }
                    //it was previously busy, can not continue with that
                    /*if (previousState == StateOfConnection.BeingOperated)
                    {
                        continue;
                    }*/

                    //dont know a better way: receive async does not accept cancellation tokens, so need to let it fail here (because some other disposed the udpclient)
                    UdpReceiveResult receiveResult;
                    try
                    {
                        Logging.Write("ReceiveSignals");
                        receiveResult = await connection.udpClient.ReceiveAsync();
                    }
                    catch (ObjectDisposedException e)
                    {
                        Logging.Write("ReceiveSignals: udp socket has been closed, (unfortunately) this is intended behaviour, stop receiving.");
                        continue;
                    }
                    var bytes = receiveResult.Buffer;

                    await WriteToIncomingSignals(connectionData, signals, report, bytes);
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

        private static async Task WriteToIncomingSignals(ConnectionMetaData connectionData, IncomingSignal[][] signals, Action<string> report, byte[] bytes)
        {
            await MessageDeMultiplexer.Divide(bytes, async () =>
            {
                Logging.Write("I (" + connectionData.iListenToPort + ") received sth ");

                Logging.Write("parse " + bytes.ToString() + " # " + bytes.Length);
                var parsedString = Encoding.ASCII.GetString(bytes, 1, bytes.Length - 1);
                Logging.Write("report " + parsedString);
                report(parsedString);
                var package = SignalCompressor.Decompress(parsedString);
                signals[package.clientId][package.index].data = package;
            },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package connection request!?"); },
                                    async () => { Logging.Write("ReceiveSignals: unexpected package tcp keepalive!?"); });
        }
    }
}
