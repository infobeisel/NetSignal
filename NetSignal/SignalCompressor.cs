using System;

namespace NetSignal
{
    public class SignalCompressor
    {
        public static string Compress(FloatDataPackage package)
        {

            return package.id.ToString("00000000000000000000000000000000") +
                package.timeStamp.Ticks.ToString("00000000000000000000000000000000") +
                BitConverter.DoubleToInt64Bits((double)package.data).ToString("00000000000000000000000000000000");
            /*return Convert.ToString( package.id, 2).PadLeft(32,'0')
                + Convert.ToString(package.timeStamp.Ticks,2).PadLeft(64, '0')
                + Convert.ToString(BitConverter.DoubleToInt64Bits((double)package.data),2).PadLeft(64, '0');*/
        }

        public static FloatDataPackage Decompress(string compressed)
        {
            
            //TODO OMG MEMORY
            FloatDataPackage p = new FloatDataPackage();
            p.id = int.Parse(compressed.Substring(0, 32));
            p.timeStamp = new DateTime(Int64.Parse(compressed.Substring(32, 32)));
            p.data = (float)BitConverter.Int64BitsToDouble(long.Parse(compressed.Substring(64, 32)));
            return p;
        }
    }
}
