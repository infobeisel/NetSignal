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

        public static StateOfConnection Exchange(ref int loc, StateOfConnection val)
        {
            return (StateOfConnection)System.Threading.Interlocked.Exchange(ref loc, (int)val);
        }
    }
}
