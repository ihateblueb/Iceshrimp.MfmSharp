using System.Diagnostics;
using Iceshrimp.MfmSharp;
using SharpFuzz;

Fuzzer.Run(input =>
{
	var pre = Stopwatch.GetTimestamp();
	MfmParser.Parse(input);
	if (Stopwatch.GetElapsedTime(pre).TotalMilliseconds > 100)
		throw new Exception("Timeout!");
}, bufferSize: 100_000);
