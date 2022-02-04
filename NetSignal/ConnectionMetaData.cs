using System.Net;

namespace NetSignal
{
    //data necessary to run a connection, does (should!) not change during lifetime
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

        public override string ToString()
        {
            return "server IP : " + serverIpToSendTo + " server port " + thisListensToPort + " sendtoendpoint " + toSendToThis + " listentoendpoint " + thisListensTo + " clientID " + clientID;
        }
        */

        public string myIp;
        public int iListenToPort;

        public int clientID;

    }
}
