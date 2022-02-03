using System;
using System.Threading.Tasks;

namespace NetSignal
{
    //contains the NetSignal protocol rules for identifying the right type of an incoming udp or tcp message
    public class MessageDeMultiplexer
    {
        public async static Task Divide(byte [] message, Func<Task> handleFloatSignal, Func<Task> handleTCPConnectionRequest, Func<Task> handleTCPAliveSignal, Func<Task> handleUdpAliveSignal)
        {
            switch (message[0])
            {
                case 0: //normal float signal
                    await handleFloatSignal();
                    break;
                case 1: //tcp connection request
                    await handleTCPConnectionRequest();
                    break;
                case 2: //tcp alive signal
                    await handleTCPAliveSignal();
                    break;
                case 3: //udp alive signal
                    await handleUdpAliveSignal();
                    break;
                default:
                    Logging.Write("found invalid message type: " + message);
                    break;
            }
        }


        public async static Task MarkFloatSignal(byte[] message, Func<Task> handleFloatSignal)
        {
            message[0] = 0;
            await handleFloatSignal();
        }

        public async static Task MarkTCPConnectionRequest(byte[] message, Func<Task> handleRequest)
        {
            message[0] = 1;
            await handleRequest();
        }

        public async static Task MarkTCPKeepAlive(byte[] message, Func<Task> handleRequest)
        {
            message[0] = 2;
            await handleRequest();
        }

        public async static Task MarkUdpKeepAlive(byte[] message, Func<Task> handleRequest)
        {
            message[0] = 3;
            await handleRequest();
        }

    }
}
