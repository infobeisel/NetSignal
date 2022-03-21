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

        public static int findLatestHistIndex(NetSignal.IncomingSignal[][] signals, int signalI)
        {
            var ret = 0;
            var comp = new DateTime(0);
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i][signalI].data.timeStamp > comp)
                {
                    comp = signals[i][signalI].data.timeStamp;
                    ret = i;
                }
            }
            return ret;
        }

        public static void findTwoLatestHistIndices(NetSignal.IncomingSignal[][] signals, int signalI, out int latest, out int secondLatest)
        {
            var compLatest = new DateTime(0);
            var compSecondLatest = new DateTime(0);
            latest = 0;
            secondLatest = 0;
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i][signalI].data.timeStamp > compLatest)
                {
                    compLatest = signals[i][signalI].data.timeStamp;
                    latest = i;
                }
            }
            for (int i = 0; i < signals.Length; i++)
            {
                if (signals[i][signalI].data.timeStamp > compSecondLatest && i != latest)
                {
                    compSecondLatest = signals[i][signalI].data.timeStamp;
                    secondLatest = i;
                }
            }
        }
    }
}