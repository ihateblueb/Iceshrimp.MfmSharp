using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Iceshrimp.MfmSharp;
using Iceshrimp.MfmSharp.Examples;
using Perfolizer.Horology;

var opts = DefaultConfig
           .Instance
           .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.Method))
           .WithSummaryStyle(SummaryStyle
                             .Default
                             .WithTimeUnit(TimeUnit.Microsecond)
                             .WithMaxParameterColumnWidth(100));

BenchmarkRunner.Run<Benchmarks>(opts);

[MemoryDiagnoser(false)]
[ShortRunJob]
[HideColumns("Method", "StdDev")]
public class Benchmarks
{
	public static IEnumerable<string> GetPayloadNames()
		=> typeof(MfmExamples).GetMethods(BindingFlags.Static | BindingFlags.Public)
		                      .Select(p => p.Name);

	// ReSharper disable once MemberCanBePrivate.Global
	[ParamsSource(nameof(GetPayloadNames))]
	public string PayloadName { get; set; } = null!;

	private static readonly Dictionary<string, string> PayloadCache = [];

	private string GetPayload(string name)
	{
		if (PayloadCache.TryGetValue(name, out var payload)) return payload;
		PayloadCache[name] = payload = (string)typeof(MfmExamples).GetMethod(PayloadName)!.Invoke(null, [])!;
		return payload;
	}

	[Benchmark]
	public void MfmSharp() => _ = MfmParser.Parse(GetPayload(PayloadName));
}
