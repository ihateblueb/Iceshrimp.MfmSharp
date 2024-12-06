using System.Diagnostics;
using System.Security.Cryptography;
using Iceshrimp.MfmSharp;

Directory.CreateDirectory("results");
var crashes   = Directory.CreateDirectory("results/crashes");
var slowdowns = Directory.CreateDirectory("results/slowdowns");

var opts    = new ParallelOptions { MaxDegreeOfParallelism = 32 };
var charset = " !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~\nabcdefghijklmnopqrstuvwxyz".ToCharArray();

const int length  = 100_000;
const int slowDur = 50;

long id   = 1; //changeme to restart
var  fail = 0;
var  slow = 0;

Parallel.ForEach(GetRandomPayloads(), opts, input =>
{
	id++;
	if (id < 0) throw new Exception("count underflow!");

	try
	{
		if (id % 1000 == 0)
			Console.Write($"\rCompleted {id} payloads ({fail} fail, {slow} slow)");
		var pre = Stopwatch.GetTimestamp();

		MfmParser.Parse(input);

		var elapsedMs = (int)Stopwatch.GetElapsedTime(pre).TotalMilliseconds;
		if (elapsedMs > slowDur)
		{
			slow++;
			File.WriteAllText($"results/{slowdowns.Name}/{id}.txt", input);
		}
	}
	catch
	{
		fail++;
		File.WriteAllText($"results/{crashes.Name}/{id}.txt", input);
	}
});
return;

IEnumerable<string> GetRandomPayloads()
{
	while (true)
	{
		yield return RandomNumberGenerator.GetString(charset, length);
	}
}
