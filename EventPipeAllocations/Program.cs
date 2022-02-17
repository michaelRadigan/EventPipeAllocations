using System;
using System.Diagnostics;

namespace EventPipeAllocations
{
    class Program
    {
        static void Main(string[] args)
        {
            var process = Process.GetCurrentProcess();
            //RuntimeGCEventsPrinter.PrintRuntimeGCEvents(5968);
            var bar = TraceLogStackTracePrinter.PrintBasicStackTraces(7645).Result;
        }
    }
}
