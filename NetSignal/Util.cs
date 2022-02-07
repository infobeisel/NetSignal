using System;

namespace NetSignal
{
    public class Util
    {
        public static void FlushBytes(byte[] bytes)
        {
            for (var byteI = 0; byteI < bytes.Length; byteI++) bytes[byteI] = 0;
        }

        public static StateOfConnection CompareExchange(ref int loc, StateOfConnection val, StateOfConnection comp)
        {
            return (StateOfConnection)System.Threading.Interlocked.CompareExchange(ref loc, (int)val, (int)comp);
        }

        public static bool CompareExchange(ref int loc, bool val, bool comp)
        {
            return System.Threading.Interlocked.CompareExchange(ref loc, val ? 1 : 0, comp ? 1 : 0) == 1;
        }

        public static StateOfConnection Exchange(ref int loc, StateOfConnection val)
        {
            return (StateOfConnection)System.Threading.Interlocked.Exchange(ref loc, (int)val);
        }

        public static bool Exchange(ref int loc, bool val)
        {
            return System.Threading.Interlocked.Exchange(ref loc, val ? 1 : 0) == 1;
        }

    }
}
