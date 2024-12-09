using static Iceshrimp.MfmSharp.MfmParser.ParserState;

namespace Iceshrimp.MfmSharp;

internal static class ArrayExtensions
{
	//TODO: custom binary search?
	public static int FindIndex(
		this Span<RecursionInfoLutEntry> lut, bool open,
		int lutIdxRangeStart, int? lutIdxRangeEnd,
		int resIdxRangeStart, int? resIdxRangeEnd = null
	)
	{
		lutIdxRangeEnd ??= lut.Length;
		if (lutIdxRangeEnd - lutIdxRangeStart == 0)
			return -1;

		for (var i = lutIdxRangeStart; i < lutIdxRangeEnd; i++)
		{
			var candidate = lut[i];
			if (resIdxRangeEnd != null && candidate.Idx >= resIdxRangeEnd)
				break;

			if (candidate.Idx < resIdxRangeStart)
				continue;

			if (candidate.Open != open)
				continue;

			return i;
		}

		return -1;
	}
}
