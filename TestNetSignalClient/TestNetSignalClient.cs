using NetSignal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestNetSignalClient
{
    class TestNetSignalClient
    {
        static void Main(string[] args)
        {
    
            int avWThreads; int avIOThreads;
            System.Threading.ThreadPool.GetMinThreads(out avWThreads, out avIOThreads);

            System.Threading.ThreadPool.SetMaxThreads(40, 40);
            Logging.Write("max threads " + avWThreads + " , " + avIOThreads);
            NetSignalTests.Test(args).Wait();

            string key = Console.ReadLine();
        }
    }
}
