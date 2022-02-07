using System;
using System.Threading;

namespace NetSignal
{
    //state of a connection, changes during lifetime of a connection
    public class ConnectionState
    {
        private const int byteCount = 256;

        public int tcpWriteStateName;
        public int tcpReadStateName;
        public DateTime tcpKeepAlive;//maybe, to keep the tcp connection open.

        public int udpWriteStateName;
        public SemaphoreSlim udpWriteSemaphore;

        public int udpReadStateName;

        public int httpListenerStateName;

        public byte [] tcpWriteBytes;
        public byte [] udpWriteBytes;
        public byte [] tcpReadBytes;
        public byte [] udpReadBytes;

        public bool shouldTearDown;

        public ConnectionState()
        {
            shouldTearDown = false;
            udpWriteStateName = (int)StateOfConnection.Uninitialized;
            udpWriteSemaphore = new SemaphoreSlim(1, 1);
            udpReadStateName = (int)StateOfConnection.Uninitialized;
            tcpWriteStateName = (int)StateOfConnection.Uninitialized;
            tcpReadStateName = (int)StateOfConnection.Uninitialized;
            httpListenerStateName = (int)StateOfConnection.Uninitialized;
            tcpKeepAlive = new DateTime(0);

            tcpWriteBytes = new byte[byteCount];
            udpWriteBytes = new byte[byteCount];
            tcpReadBytes = new byte[byteCount];
            udpReadBytes = new byte[byteCount];
        }
    }
}
