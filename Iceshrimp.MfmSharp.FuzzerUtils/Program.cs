using Iceshrimp.MfmSharp.FuzzerUtils;

if (args.Contains("--validate-random"))
{
	var      prefix   = Path.Combine("..", "..", "..", "..", "Iceshrimp.MfmSharp.RandomFuzz", "results");
	string[] subpaths = ["crashes", "slowdowns", "old"];
	var      dirs     = subpaths.Select(p => Path.Combine(prefix, p)).Where(Directory.Exists).ToArray();
	ValidateFixes.ValidateCrashesAndSlowdowns(dirs);
	return;
}

if (args.Contains("--validate-sharpfuzz"))
{
	var prefix = Path.Combine("..", "..", "..", "..", "Iceshrimp.MfmSharp.SharpFuzz");
	var dirs = Directory.EnumerateDirectories(prefix, "findings*")
	                    .SelectMany(Directory.EnumerateDirectories)
	                    .SelectMany(p => Directory.EnumerateDirectories(p, "crashes*"));
	ValidateFixes.ValidateCrashesAndSlowdowns(dirs.ToArray());
	return;
}

if (args.Contains("--debug"))
{
	PayloadDebug.DebugPayload(args[1]);
	return;
}
