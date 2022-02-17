using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Stacks;


namespace EventPipeAllocations
{
    public class TraceLogStackTracePrinter
    {
        // This should probably be async with tasks... For now I'm going to use Thread.sleep
        public static async Task<int> PrintBasicStackTraces(int processId)
        {
            var providers = new List<EventPipeProvider>()
            {
                new EventPipeProvider("Microsoft-Windows-DotNETRuntime",
                    EventLevel.Verbose, // this is needed in order to receive AllocationTick_V2 event
                    (long) (ClrTraceEventParser.Keywords.GC |
                            // the CLR source code indicates that the provider must be set before the monitored application starts
                            //ClrTraceEventParser.Keywords.GCSampledObjectAllocationLow | 
                            //ClrTraceEventParser.Keywords.GCSampledObjectAllocationHigh | 

                            // required to receive the BulkType events that allows 
                            // mapping between the type ID received in the allocation events
                            ClrTraceEventParser.Keywords.GCHeapAndTypeNames |   
                            ClrTraceEventParser.Keywords.Type |
                            // TODO[michaelr]: Just Experimenting
                            ClrTraceEventParser.Keywords.GCAllObjectAllocation
                    )),

                // TODO[michaelr]: Is this needed/valid?
                //new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational)
            };
            
            // Ok, so this is us going via an intermediate file for a small period of time
            var tempNetTraceFilename = Path.GetRandomFileName() + ".nettrace";
            var tempEtlxFilename = "";

            // Let's grab just a bit of data for now
            var duration = TimeSpan.FromSeconds(5);

            try
            {
                var client = new DiagnosticsClient(processId);
                using (EventPipeSession session = client.StartEventPipeSession(providers))
                using (FileStream fs = File.OpenWrite(tempNetTraceFilename))
                {
                    Task copyTask = session.EventStream.CopyToAsync(fs);
                    await Task.Delay(duration);
                    session.Stop();

                    // check if rundown is taking more than 5 seconds and add comment to report
                    Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                    Task completedTask = await Task.WhenAny(copyTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        Console.WriteLine($"# Sufficiently large applications can cause this command to take non-trivial amounts of time");
                    }
                    await copyTask;
                }
                
                
                // using the generated trace file, symbolocate and compute stacks.
                tempEtlxFilename = TraceLog.CreateFromEventPipeDataFile(tempNetTraceFilename);
                using (var symbolReader = new SymbolReader(System.IO.TextWriter.Null) { SymbolPath = SymbolPath.MicrosoftSymbolServerPath })
                using (var eventLog = new TraceLog(tempEtlxFilename))
                {
                    var stackSource = new MutableTraceEventStackSource(eventLog)
                    {
                        OnlyManagedCodeStacks = true
                    };

                    var computer = new SampleProfilerThreadTimeComputer(eventLog, symbolReader);
                    computer.GenerateThreadTimeStacks(stackSource);
                    
                    var samplesForThread = new Dictionary<int, List<StackSourceSample>>();

                    stackSource.ForEach((sample) =>
                    {
                        var stackIndex = sample.StackIndex;
                        while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false).StartsWith("Thread ("))
                            stackIndex = stackSource.GetCallerIndex(stackIndex);

                        // long form for: int.Parse(threadFrame["Thread (".Length..^1)])
                        // Thread id is in the frame name as "Thread (<ID>)"
                        string template = "Thread (";
                        string threadFrame = stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false);
                        int threadId = int.Parse(threadFrame.Substring(template.Length, threadFrame.Length - (template.Length + 1)));

                        if (samplesForThread.TryGetValue(threadId, out var samples))
                        {
                            samples.Add(sample);
                        }
                        else
                        {
                            samplesForThread[threadId] = new List<StackSourceSample>() { sample };
                        }
                    });
                    
                    var counter = new Dictionary<Tuple<int, StackSourceCallStackIndex>, int>();

                    foreach (var (threadId, samples) in samplesForThread)
                    {
                        // Why are we only printing the first??
                        foreach (var sample in samples)
                        {
                            var key = new Tuple<int, StackSourceCallStackIndex>(threadId, sample.StackIndex);
                            if (counter.TryGetValue(key, out var count))
                            {
                                counter[key] = count + 1;
                            }
                            else
                            {
                                counter[key] = 1;
                            }
                            //PrintStack(threadId, sample, stackSource);
                        }
                        //PrintStack(threadId, samples[0], stackSource);
                    }

                    foreach (var ((threadId, stackIndex), count) in counter.OrderBy(kvp => kvp.Value).Take(10))
                    {
                        var name = stackSource.GetFrameName(stackSource.GetFrameIndex(stackSource.GetCallerIndex(stackIndex)), true);
                        Console.WriteLine($"{name} : {count}");
                    }
                }
            }
            finally
            {
                if (File.Exists(tempNetTraceFilename))
                    File.Delete(tempNetTraceFilename);
                if (File.Exists(tempEtlxFilename))
                    File.Delete(tempEtlxFilename);
            }

            return 0;
        }
        
        // Example of how to grab method that allocated:
        // stackSource.GetFrameName(stackSource.GetFrameIndex(stackSource.GetCallerIndex(stackIndex)), verboseName: false)
        // Which returns e.g.: "AllocationCounter!AllocationCounter.Program.Allocate10K()"

        private static void PrintStack(int threadId, StackSourceSample stackSourceSample, StackSource stackSource)
        {
            Console.WriteLine($"Thread (0x{threadId:X}):");
            var stackIndex = stackSourceSample.StackIndex;
            while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), verboseName: false).StartsWith("Thread ("))
            {
                Console.WriteLine($"  {stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), verboseName: false)}"
                    .Replace("UNMANAGED_CODE_TIME", "[Native Frames]"));
                stackIndex = stackSource.GetCallerIndex(stackIndex);
            }
            Console.WriteLine();
        }

        private static void PrintStackWithCount(int threadId, StackSourceSample stackSourceSample, StackSource stackSource, int count)
        {
            PrintStack(threadId, stackSourceSample, stackSource);
            Console.WriteLine($"Called: {count} times");
        }
    }    
}
