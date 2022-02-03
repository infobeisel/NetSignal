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
            return "ci: " + clientId + ", si: " + index + ",t: " + timeStamp.ToShortTimeString() + ", p:" + data.ToString("0000.000");
        }
    }
}
