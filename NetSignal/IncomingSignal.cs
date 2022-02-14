using System;

namespace NetSignal
{
    public struct TimeControl
    {
        public bool HandleTimeManually;
        public long CurrentTimeTicks;
    }


    [Serializable]
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

        public static long AsLong(ref IncomingSignal from0, ref IncomingSignal from1)
        {
            long ret = 0;
            unsafe
            {
                byte* longBytes = (byte*)&ret;
                longBytes[0] = from0.data.d0;
                longBytes[1] = from0.data.d1;
                longBytes[2] = from0.data.d2;
                longBytes[3] = from0.data.d3;
                longBytes[4] = from1.data.d0;
                longBytes[5] = from1.data.d1;
                longBytes[6] = from1.data.d2;
                longBytes[7] = from1.data.d3;
            }
            return ret;
        }
    }
}
