using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Iceshrimp.MfmSharp;
using Iceshrimp.MfmSharp.Examples;
using Perfolizer.Horology;
using Perfolizer.Metrology;

var opts = DefaultConfig
           .Instance
           .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.Method))
           .WithSummaryStyle(SummaryStyle
                             .Default
                             .WithTimeUnit(TimeUnit.Microsecond)
                             .WithSizeUnit(SizeUnit.KB)
                             .WithMaxParameterColumnWidth(100));

BenchmarkRunner.Run<Benchmarks>(opts);

[MemoryDiagnoser(false)]
//[ShortRunJob]
[HideColumns("Method", "StdDev")]
public class Benchmarks
{
	public static IEnumerable<string> GetPayloadNames()
		=> typeof(MfmExamples).GetMethods(BindingFlags.Static | BindingFlags.Public)
		                      .Select(p => p.Name);

	// ReSharper disable once MemberCanBePrivate.Global
	[ParamsSource(nameof(GetPayloadNames))]
	public string PayloadName { get; set; } = null!;

	private string _payload = null!;

	[GlobalSetup]
	public void GlobalSetup() => _payload = (string)typeof(MfmExamples).GetMethod(PayloadName)!.Invoke(null, [])!;

	// We reallocate the string here because the results are nonsensical otherwise.
	// It seems to be some artifact of the synthetic workload.
	// Using [RunOncePerIteration] without reallocation also reproduces these results.
	// I have no idea why, but the results match manual testing of prod-like workloads (different text in every iteration).
	[Benchmark]
	public IMfmNode[] MfmSharp() => MfmParser.Parse(_payload.AsSpan().ToString());
}
