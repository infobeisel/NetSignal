using System;

namespace NetSignal
{
    public struct IncomingSignal
    {
        private FloatDataPackage dataMember;
        public FloatDataPackage data
        {
            internal set
            {
                
                dataMember = value;
                dataMember.timeStamp = DateTime.UtcNow;//TODO
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
