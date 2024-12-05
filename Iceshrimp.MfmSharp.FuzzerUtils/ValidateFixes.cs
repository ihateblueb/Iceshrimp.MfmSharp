using System.Collections.Concurrent;
using System.Diagnostics;

namespace Iceshrimp.MfmSharp.FuzzerUtils;

public static class ValidateFixes
{
	private static readonly ConcurrentBag<string> Fail = [];
	private static readonly ConcurrentBag<string> Slow = [];
	private static readonly ConcurrentBag<string> Pass = [];

	public static void ValidateCrashesAndSlowdowns(string[] directories)
	{
		Fail.Clear();
		Slow.Clear();
		Pass.Clear();

		var files = directories.SelectMany(Directory.EnumerateFiles).ToArray();

		var total = files.Length;
		var opts  = new ParallelOptions { MaxDegreeOfParallelism = 8 };
		var i     = 0;
		var start = Stopwatch.GetTimestamp();

		Parallel.ForEach(files, opts, file =>
		{
			Console.Write($"\rProcessing payload {++i}/{total}");
			var input = File.ReadAllText(file);
			var pre   = Stopwatch.GetTimestamp();
			try
			{
				MfmParser.Parse(input);
			}
			catch
			{
				Fail.Add(file);
				return;
			}

			if (Stopwatch.GetElapsedTime(pre).TotalMilliseconds > 50)
				Slow.Add(file);
			else
				Pass.Add(file);
		});
		var elapsed = Math.Round(Stopwatch.GetElapsedTime(start).TotalSeconds, 2);

		Console.WriteLine($"\rProcessed {total} payloads in {elapsed} seconds.");
		Console.WriteLine($"Fail: {Fail.Count}");
		Console.WriteLine($"Slow: {Slow.Count}");
		Console.WriteLine($"Pass: {Pass.Count}");

		string? next = null;
		if (!Fail.IsEmpty)
		{
			Console.Write("Next crash to fix: ");
			next = Fail.Order().First();
		}
		else if (!Slow.IsEmpty)
		{
			Console.Write("Next slowdown to fix: ");
			next = Slow.Order().First();
		}

		if (next != null)
		{
			Console.WriteLine(next);
			File.WriteAllText("debug_payload.txt", File.ReadAllText(next));
			Console.WriteLine("Saved as debug_payload.txt");
		}
	}
}
