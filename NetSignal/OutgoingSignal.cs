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
                    dataDirty = true;
                    dataMember.timeStamp = DateTime.UtcNow;//TODO
                }
                dataMember = value;
            }
            internal get { return dataMember; }
        }

        public bool dataDirty;

        public override string ToString()
        {
            return "data : " + data + " dataDirty " + dataDirty;
        }
    }
}
