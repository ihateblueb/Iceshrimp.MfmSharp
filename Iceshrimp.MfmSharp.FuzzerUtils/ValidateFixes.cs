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
		var opts  = new ParallelOptions { MaxDegreeOfParallelism = 4 };
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

			if (Stopwatch.GetElapsedTime(pre).TotalMilliseconds > 25)
				Slow.Add(file);
			else
				Pass.Add(file);
		});

		var slow = Slow.ToList();
		Slow.Clear();
		i = 0;
		foreach (var file in slow.ToArray())
		{
			Console.Write($"\rAuditing slow payload {++i}/{slow.Count} ({Slow.Count} still slow)");
			var  input = File.ReadAllText(file);
			long pre   = 0;
			for (var j = 0; j < 5; j++)
			{
				pre = Stopwatch.GetTimestamp();
				MfmParser.Parse(input);
				if ((Stopwatch.GetElapsedTime(pre) / 5).TotalMilliseconds <= 25)
					break;
			}

			if ((Stopwatch.GetElapsedTime(pre) / 5).TotalMilliseconds > 25)
				Slow.Add(file);
			else
				Pass.Add(file);
		}

		var elapsed = Math.Round(Stopwatch.GetElapsedTime(start).TotalSeconds, 2);
		Console.WriteLine($"\rProcessed {total} payloads in {elapsed} seconds.        ");
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
