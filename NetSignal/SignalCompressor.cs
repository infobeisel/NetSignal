using System;

namespace NetSignal
{
    public class SignalFactory
    {
        public static IncomingSignal[] ConstructIncomingSignalArray(int count)
        {
            var ret = new IncomingSignal[count];

            return ret;
        }

        public static OutgoingSignal[] ConstructOutgoingSignalArray(int count)
        {
            var ret = new OutgoingSignal[count];
            return ret;
        }
    }

    public class SignalCompressor
    {
      /*  public unsafe static void Compress(KeepAlivePackage package, byte [] to, int startFrom)
        {
            byte* cIdPtr = (byte*) & package.clientId;
            for (int i = 0; i < 4; i++)
                to[startFrom + 0 + i] = cIdPtr[i];

            var ticks = package.timeStamp.Ticks;
            cIdPtr = (byte*)& ticks;
            for (int i = 0; i < 8; i++)
                to[startFrom + 4 + i] = cIdPtr[i];
            
        }

        public unsafe static KeepAlivePackage DecompressKeepAlive(byte [] compressed, int startFrom)
        {
            
            KeepAlivePackage p = new KeepAlivePackage();
            byte* cIdPtr = (byte*)&p.clientId;
            for (int i = 0; i < 4; i++)
                cIdPtr[i] = compressed[startFrom + 0 + i];

            long ticks = 0;
            cIdPtr = (byte*)&ticks;
            for (int i = 0; i < 8; i++)
                cIdPtr[i] = compressed[startFrom + 4 + i];
            p.timeStamp = new DateTime(ticks);
            return p;
        }*/


        public unsafe static void Compress(DataPackage package, byte[] to, int startFrom)
        {
            byte* cIdPtr = (byte*)&package.clientId;
            for (int i = 0; i < 4; i++)
                to[startFrom + 0 + i] = cIdPtr[i];

            cIdPtr = (byte*)&package.index;
            for (int i = 0; i < 4; i++)
                to[startFrom + 4 + i] = cIdPtr[i];

            var ticks = package.timeStamp.Ticks;
            cIdPtr = (byte*)&ticks;
            for (int i = 0; i < 8; i++)
                to[startFrom + 8 + i] = cIdPtr[i];

            to[startFrom + 16 + 0] = package.d0;
            to[startFrom + 16 + 1] = package.d1;
            to[startFrom + 16 + 2] = package.d2;
            to[startFrom + 16 + 3] = package.d3;


        }


        public unsafe static DataPackage DecompressDataPackage(byte[] compressed, int startFrom)
        {

            DataPackage p = new DataPackage();
            byte* cIdPtr = (byte*)&p.clientId;
            for (int i = 0; i < 4; i++)
                cIdPtr[i] = compressed[startFrom + 0 + i];

            cIdPtr = (byte*)&p.index;
            for (int i = 0; i < 4; i++)
                cIdPtr[i] = compressed[startFrom + 4 + i];

            long ticks = 0;
            cIdPtr = (byte*)&ticks;
            for (int i = 0; i < 8; i++)
                cIdPtr[i] = compressed[startFrom + 8 + i];
            p.timeStamp = new DateTime(ticks);

            
            p.signalType = (SignalType)compressed[0];


            p.d0 = compressed[startFrom + 16 + 0];
            p.d1 = compressed[startFrom + 16 + 1];
            p.d2 = compressed[startFrom + 16 + 2];
            p.d3 = compressed[startFrom + 16 + 3];
            return p;
        }

    }
}
