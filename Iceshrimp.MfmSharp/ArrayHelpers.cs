using static Iceshrimp.MfmSharp.MfmParser.ParserState.RecursionInfoEntry;

namespace Iceshrimp.MfmSharp;

internal static class ArrayExtensions
{
	//TODO: custom binary search?
	public static int FindIndex(
		this Span<int> lut, int flag,
		int lutIdxRangeStart, int? lutIdxRangeEnd,
		int resIdxRangeStart, int? resIdxRangeEnd = null
	)
	{
		lutIdxRangeEnd ??= lut.Length;
		if (lutIdxRangeEnd - lutIdxRangeStart == 0)
			return -1;

		for (var i = lutIdxRangeStart; i < lutIdxRangeEnd; i++)
		{
			var candidate    = lut[i];
			var candidateIdx = candidate & IndexBitmask;
			if (candidateIdx >= resIdxRangeEnd)
				break;

			if (candidateIdx < resIdxRangeStart)
				continue;

			if ((candidate & OpenBitmask) != flag)
				continue;

			return i;
		}

		return -1;
	}
}
