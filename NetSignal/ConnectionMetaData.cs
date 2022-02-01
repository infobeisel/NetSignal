using System.Net;

namespace NetSignal
{
    //data necessary to run a connection, does (should!) not change during lifetime
    public struct ConnectionMetaData
    {

        public string matchmakingServerIp;
        public int matchmakingServerPort;

        public string serverIp;
        public int listenPort;
        public int sendToPort;
        public IPEndPoint toSendToThis;
        public IPEndPoint thisListensTo;

        public IPEndPoint toSendToServer;

        public int clientID;

        public override string ToString()
        {
            return "server IP : " + serverIp + " server port " + listenPort + " sendtoendpoint " + toSendToThis + " listentoendpoint " + thisListensTo + " clientID " + clientID;
        }
    }
}
