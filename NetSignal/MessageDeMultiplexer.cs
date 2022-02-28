using System;
using System.Threading.Tasks;

namespace NetSignal
{
    //contains the NetSignal protocol rules for identifying the right type of an incoming udp or tcp message
    public class MessageDeMultiplexer
    {
        public async static Task Divide(byte [] message, Func<Task> handleDataSignal, Func<Task> handleTCPConnectionRequest, Func<Task> handleTCPAliveSignal, Func<Task> handleUdpAliveSignal)
        {
            switch ((SignalType) message[0])
            {
                case SignalType.TCPConnectionRequest: //tcp connection request
                    await handleTCPConnectionRequest();
                    break;
                case SignalType.TCPAlive: //tcp alive signal
                    await handleTCPAliveSignal();
                    break;
                case SignalType.UDPAlive: //udp alive signal
                    await handleUdpAliveSignal();
                    break;
                case SignalType.Data:
                    await handleDataSignal();
                    break;
            }
        }

        public async static Task MarkSignal(SignalType signalType, byte[] message, Func<Task> handleSignal)
        {
            message[0] = (byte)signalType;
            await handleSignal();
        }
        /*

        public async static Task MarkFloatSignal(byte[] message, Func<Task> handleFloatSignal)
        {
            message[0] = (byte)SignalType.Float;
            await handleFloatSignal();
        }


        public async static Task MarkIntSignal(byte[] message, Func<Task> handleIntSignal)
        {
            message[0] = (byte)SignalType.Int;
            await handleIntSignal();
        }
        public async static Task MarkStringSignal(byte[] message, Func<Task> handleStringSignal)
        {
            message[0] = (byte)SignalType.String;
            await handleStringSignal();
        }

        public async static Task MarkTCPConnectionRequest(byte[] message, Func<Task> handleRequest)
        {
            message[0] = (byte)SignalType.TCPConnectionRequest;
            await handleRequest();
        }

        public async static Task MarkTCPKeepAlive(byte[] message, Func<Task> handleRequest)
        {
            message[0] = (byte)SignalType.TCPAlive;
            await handleRequest();
        }

        public async static Task MarkUdpKeepAlive(byte[] message, Func<Task> handleRequest)
        {
            message[0] = (byte)SignalType.UDPAlive;
            await handleRequest();
        }
        */
    }
}
