using System;

namespace NetSignal
{
    public struct OutgoingSignal
    {
        private FloatDataPackage dataMember;//TODO WARNING?!

        public FloatDataPackage data
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
            return data.data == incoming.data.data;
        }
    }
}
