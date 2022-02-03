using System;

namespace NetSignal
{
    public class SignalCompressor
    {
        public unsafe static void Compress(KeepAlivePackage package, byte [] to, int startFrom)
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
        }


        public unsafe static void Compress(FloatDataPackage package, byte[] to, int startFrom)
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

            cIdPtr = (byte*)&package.data;
            for (int i = 0; i < 4; i++)
                to[startFrom + 16 + i] = cIdPtr[i];
        }

        /*
        public static string Compress(FloatDataPackage package)
        {

            return package.clientId.ToString("00000000000000000000000000000000") +
                package.index.ToString("00000000000000000000000000000000") +
                package.timeStamp.Ticks.ToString("00000000000000000000000000000000") +
                BitConverter.DoubleToInt64Bits((double)package.data).ToString("00000000000000000000000000000000");
        
        }*/


        public unsafe static FloatDataPackage DecompressFloatPackage(byte[] compressed, int startFrom)
        {

            FloatDataPackage p = new FloatDataPackage();
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

            cIdPtr = (byte*)&p.data;
            for (int i = 0; i < 4; i++)
                cIdPtr[i] = compressed[startFrom + 16 + i];

            return p;
        }

        /*
        public static FloatDataPackage DecompressFloatPackage(string compressed)
        {
            
            //TODO OMG MEMORY
            FloatDataPackage p = new FloatDataPackage();
            p.clientId = int.Parse(compressed.Substring(0, 32));
            p.index = int.Parse(compressed.Substring(32, 32));
            p.timeStamp = new DateTime(Int64.Parse(compressed.Substring(64, 32)));
            p.data = (float)BitConverter.Int64BitsToDouble(long.Parse(compressed.Substring(96, 32)));
            return p;
        }*/
    }
}
