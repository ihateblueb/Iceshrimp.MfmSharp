namespace Iceshrimp.MfmSharp.FuzzerUtils;

public static class PayloadDebug
{
	public static void DebugPayload(string path)
	{
		var input = File.ReadAllText(path);
		MfmParser.Parse(input);
	}
}
