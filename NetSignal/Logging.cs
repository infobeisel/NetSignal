using System;

namespace NetSignal
{
    public class Logging
    {
        private static object lockObject = new object();
        public static void Write(string msg)
        {
            lock(lockObject)
            {
                Console.WriteLine(msg);
            }
        }
        public static void Write(Exception e)
        {
            lock (lockObject)
            {
                Console.WriteLine(e);
            }
        }
    }
}
