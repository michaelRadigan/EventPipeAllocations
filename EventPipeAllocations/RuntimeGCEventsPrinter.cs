using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;


namespace EventPipeAllocations
{
    public class RuntimeGCEventsPrinter
    {
        private static void OnAllocationTick(GCAllocationTickTraceData traceData)
        {
            // TODO[michaelr]: Implement me
            Console.WriteLine($"OnAllocationTick: Type name: {traceData.TypeName}, TypeId: {traceData.TypeID}");
        }

        private static void OnSampleObjectAllocation(GCSampledObjectAllocationTraceData traceData)
        {
            // TODO[michaelr]: Implement me
            Console.WriteLine($"OnSampleObjectAllocation: typeId: {traceData.TypeID}");
        }

        private static void OnTypeBulkType(GCBulkTypeTraceData traceData)
        {
            
        }
        
        private static void SetupListeners(EventPipeEventSource source)
        {
            source.Clr.GCAllocationTick += OnAllocationTick;

            source.Clr.GCSampledObjectAllocation += OnSampleObjectAllocation;

            // required to receive the mapping between type ID (received in GCSampledObjectAllocation)
            // and their name (received in TypeBulkType)
            source.Clr.TypeBulkType += OnTypeBulkType;
        }
        
        public static void PrintRuntimeGCEvents(int processId)
        {
            
            //var providers = new List<EventPipeProvider>()
            // {
            //    new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational)
            //};
            var providers = new List<EventPipeProvider>()
            {
                new EventPipeProvider("Microsoft-Windows-DotNETRuntime",
                    EventLevel.Verbose, // this is needed in order to receive AllocationTick_V2 event
                    (long) (ClrTraceEventParser.Keywords.GC |
                            // the CLR source code indicates that the provider must be set before the monitored application starts
                            ClrTraceEventParser.Keywords.GCSampledObjectAllocationLow | 
                            ClrTraceEventParser.Keywords.GCSampledObjectAllocationHigh | 

                            // required to receive the BulkType events that allows 
                            // mapping between the type ID received in the allocation events
                            ClrTraceEventParser.Keywords.GCHeapAndTypeNames |   
                            ClrTraceEventParser.Keywords.Type)),
            };

            var client = new DiagnosticsClient(processId);
            using (EventPipeSession session = client.StartEventPipeSession(providers, false))    
            {
                var source = new EventPipeEventSource(session.EventStream);
                
                // So, I think that there are two methods here that allow us to create a tracelog 
                //var foo = TraceLog.CreateFromEventPipeDataFile()
                //var traceLogSource = TraceLog.CreateFromTraceEventSession(session);

                //SetupListeners(source);
                
                source.Clr.All += (TraceEvent obj) => Console.WriteLine(obj.ToString());

                try
                {
                    source.Process();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error encountered while processing events");
                    Console.WriteLine(e.ToString());
                }
            }
        }
    }    
}
