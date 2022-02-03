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
                if (!dataMember.data.Equals(value.data))
                {
                    dataMember = value;
                    dataMember.timeStamp = DateTime.UtcNow;//TODO
                    dataDirty = true;
                }
                
            }
            internal get { return dataMember; }
        }

        public bool dataDirty;

        public override string ToString()
        {
            return "data : " + data + " dataDirty " + dataDirty;
        }

        public bool Equals(IncomingSignal incoming)
        {
            return data.data == incoming.data.data;
        }
    }
}
