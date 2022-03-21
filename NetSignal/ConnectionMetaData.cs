using System;

namespace NetSignal
{
    //data necessary to run a connection, does (should!) not change during lifetime
    [Serializable]
    public struct ConnectionMetaData
    {
        public string matchmakingServerIp;
        public int matchmakingServerPort;
       
        public string myIp;
        public int iListenToPort;
    }
}