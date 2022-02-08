using System;

namespace NetSignal
{
    [Serializable]
    public struct ServerListElementResponse
    {
        public string name;
        public string ip;
        public long tick;
        public int port;
        public int currentPlayerCount;
        public int maxPlayerCount;

        public override string ToString()
        {
            return name + "," + ip + ":" + port.ToString() + "," + currentPlayerCount.ToString() + "/" + maxPlayerCount.ToString();
        }
    }

}
