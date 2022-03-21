using System;

namespace NetSignal
{
    //data necessary to run a connection, does (should!) not change during lifetime
    [Serializable]
    public struct ConnectionMetaData
    {
        public string matchmakingServerIp;
        public int matchmakingServerPort;
        /*
        public string serverIpToSendTo;
        public int thisListensToPort;
        public int portToSendTo;
        public IPEndPoint toSendToThis;
        public IPEndPoint thisListensTo;

        public IPEndPoint toSendToServer;
        */

        public string myIp;
        public int iListenToPort;
    }
}