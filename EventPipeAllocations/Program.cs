using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EventPipeAllocations
{
    class Program
    {
        static void Main(string[] args)
        {
            var process = Process.GetCurrentProcess();
            //RuntimeGCEventsPrinter.PrintRuntimeGCEvents(5968);
            // We could always start the eventListener as a sidecar process?
            
            
            var currentProcessId = Process.GetCurrentProcess().Id;
            Task.Factory.StartNew(() => TraceLogStackTracePrinter.PrintBasicStackTraces(currentProcessId).Result);

            var i = 0;
            while (i < 100)
            {
                Allocate10K();
                Allocate5K();
                GC.Collect();
                Console.WriteLine(i);
                Thread.Sleep(5000);
                i += 1;
            }
            //Console.WriteLine(task.Result);
            
            
            
        }
        private static void Allocate10K()
        {
            for (int i = 0; i < 10000; i++)
            {
                int[] x = new int[100];
            }
        }

        private static void Allocate5K()
        {
            for (int i = 0; i < 5000; i++)
            {
                int[] x = new int[100];
            }
        }
    }
}
