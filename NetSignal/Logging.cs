using System;

namespace NetSignal
{
    public class Logging
    {
        public delegate void LoggingHandler(string text);
        public static event LoggingHandler WantsLog;

        private static object lockObject = new object();
        public static void Write(string msg)
        {
            lock(lockObject)
            {
                Console.WriteLine(msg);
                WantsLog?.Invoke(msg);
            }
        }
        public static void Write(Exception e)
        {
            lock (lockObject)
            {
                Console.WriteLine(e);
                WantsLog?.Invoke(e.Message);
            }
        }
    }
}
