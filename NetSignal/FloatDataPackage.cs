using System;

namespace NetSignal
{
    [Serializable]
    public struct FloatDataPackage
    {
        public int clientId;
        public int index;
        public DateTime timeStamp;
        public float data;

        public override string ToString()
        {
            return "id : " + clientId + " index " + index + " timestamp " + timeStamp + " data " + data;
        }
    }
}
