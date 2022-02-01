using System;

namespace NetSignal
{
    [Serializable]
    public struct FloatDataPackage
    {
        public int id;
        public DateTime timeStamp;
        public float data;

        public override string ToString()
        {
            return "id : " + id + " timestamp " + timeStamp + " data " + data;
        }
    }
}
