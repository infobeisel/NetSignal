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
                makeDirty();

            }
            internal get { return dataMember; }
        }

        private void makeDirty()
        {
            dataMember.timeStamp = DateTime.UtcNow;//TODO
            dataDirty = true;
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

        //convenience:
        
        public void WriteFloat(float f)
        {
            
            dataMember.WriteFloat(f);
            makeDirty();
        }

        public void WriteInt(int i)
        {

            dataMember.WriteInt(i);
            makeDirty();
        }

        public void WriteUdpAlive()
        {

            dataMember.WriteUdpAlive();
            makeDirty();
        }

        public void WriteTcpAlive()
        {

            dataMember.WriteTcpAlive();
            makeDirty();
        }

        public void WriteString(string str)
        {

            dataMember.WriteString(str);
            makeDirty();
        }

        public static void WriteLong(long value, ref OutgoingSignal to0, ref OutgoingSignal to1)
        {
            unsafe
            {
                byte* longBytes = (byte*)&value;
                var toD = to0.dataMember;
                toD.d0 = longBytes[0];
                toD.d1 = longBytes[1];
                toD.d2 = longBytes[2];
                toD.d3 = longBytes[3];
                to0.data = toD;


                toD = to1.dataMember;
                toD.d0 = longBytes[4];
                toD.d1 = longBytes[5];
                toD.d2 = longBytes[6];
                toD.d3 = longBytes[7];
                to1.data = toD;
            }
        }

        
    }
}
