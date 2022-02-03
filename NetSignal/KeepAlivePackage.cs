using System;

namespace NetSignal
{
    [Serializable]
    public struct KeepAlivePackage
    {
        public int clientId;
        public DateTime timeStamp;
        public override string ToString()
        {
            return "id : " + clientId + " timestamp " + timeStamp ;
        }
    }
}
