using System;
using System.Threading.Tasks;

namespace NetSignal
{
    //contains the NetSignal protocol rules for identifying the right type of an incoming udp or tcp message
    public class MessageDeMultiplexer
    {
        public static async Task Divide(byte[] message, Func<Task> handleDataSignal, Func<Task> handleTCPConnectionRequest, Func<Task> handleTCPAliveSignal)
        {
            switch ((SignalType)message[0])
            {
                case SignalType.TCPConnectionRequest: //tcp connection request
                    await handleTCPConnectionRequest();
                    break;

                case SignalType.TCPAlive: //tcp alive signal
                    await handleTCPAliveSignal();
                    break;

                case SignalType.Data:
                    await handleDataSignal();
                    break;
            }
        }

        public static async Task MarkSignal(SignalType signalType, byte[] message, Func<Task> handleSignal)
        {
            message[0] = (byte)signalType;
            await handleSignal();
        }

    }
}