using System;

namespace NetSignal
{
    [System.Serializable]
    public struct OutgoingSignal
    {
        private DataPackage dataMember;//TODO WARNING?!
        public DataPackage data
        {
            set
            {
                
                dataMember = value;
                dataMember.timeStamp = DateTime.UtcNow;//TODO
                dataDirty = true;
                
                
            }
            internal get { return dataMember; }
        }

        public bool dataDirty;

        public override string ToString()
        {
            return "D: " + data + ", d: " + dataDirty;
        }


        public bool Equals(IncomingSignal incoming)
        {
            if (data.signalType != incoming.data.signalType)
                return false;

            return data.d0 == incoming.data.d0 &&
                data.d1 == incoming.data.d1 &&
                data.d2 == incoming.data.d2 &&
                data.d3 == incoming.data.d3;

        }
    }
}
