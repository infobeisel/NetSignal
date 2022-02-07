using System;

namespace NetSignal
{
    public struct IncomingSignal
    {
        private DataPackage dataMember;
        public DataPackage data
        {
            internal set
            {
                dataMember = value;
                dataHasBeenUpdated = true;
            }
            get
            {
                return dataMember;
            }
        }

        public bool dataHasBeenUpdated;
        public DateTime cameIn;


        public override string ToString()
        {
            return "D: " + data + "pn:" + (cameIn - dataMember.timeStamp).TotalMilliseconds.ToString("000.00");
        }
    }
}
