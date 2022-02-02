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
                if (!dataMember.data.Equals(value.data))
                {
                    dataMember = value;
                    dataMember.timeStamp = DateTime.UtcNow;//TODO
                    dataHasBeenUpdated = true;
                }
                
            }
            get
            {
                return dataMember;
            }
        }

        public bool dataHasBeenUpdated;

        public override string ToString()
        {
            return "data : " + data;
        }
    }
}
