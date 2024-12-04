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

[MemoryDiagnoser]
[ShortRunJob]
//[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class Benchmarks
{
	public static IEnumerable<string> GetPayloads()
		=> typeof(MfmExamples).GetMethods(BindingFlags.Static | BindingFlags.Public)
		                      .Select(p => p.Name);

	// ReSharper disable once MemberCanBePrivate.Global
	[ParamsSource(nameof(GetPayloads))] public string Payload { get; set; } = null!;

	[Benchmark(Baseline = true)]
	public void MfmSharp()
	{
		var input = (string)typeof(MfmExamples).GetMethod(Payload)!.Invoke(null, [])!;
		_ = MfmParser.Parse(input);
		//if (res is [MfmNodeTypes.MfmTimeoutTextNode])
		//	throw new Exception("Test timed out");
	}

	//[Benchmark]
	//public void FParsec()
	//{
	//	var input = (string)typeof(MfmExamples).GetMethod(Payload)!.Invoke(null, [])!;
	//	var res   = Mfm.parse(input);
	//	if (res is [MfmNodeTypes.MfmTimeoutTextNode])
	//		throw new Exception("Test timed out");
	//	_ = res.ToList();
	//}
}
