using System.Buffers;
using Iceshrimp.MfmSharp.Helpers;
using JetBrains.Annotations;
using static Iceshrimp.MfmSharp.MfmParser.ParserState.RecursionInfoConstants;

namespace Iceshrimp.MfmSharp;

[PublicAPI]
public static class MfmParser
{
	private const int LengthLimit     = 100_000;
	private const int RecursionLimit  = 20;
	private const int LookupThreshold = 500;

	public static IMfmNode[] Parse(ReadOnlySpan<char> input) => Parse(input, false);

	public static IMfmNode[] Parse(ReadOnlySpan<char> input, bool simple)
	{
		input = input.Trim();
		if (input.Length == 0) return [];

		var processed = input.ToString().ReplaceLineEndings("\n");
		if (processed.Length == 0) return [];
		if (processed.Length > LengthLimit) return [new MfmTextNode(processed)];
#if !DEBUG && !FUZZ
		try
		{
#endif
			var    state = new ParserState(processed, simple ? ParseMode.Simple : ParseMode.Full);
			Parser func  = simple ? ParseNodeSimple : ParseNode;
			while (!state.IsEos)
				func(ref state);
			return state.GetResults();
#if !DEBUG && !FUZZ
		}
		catch
		{
			return [new MfmTextNode(processed)];
		}
#endif
	}

	internal enum ParseMode
	{
		Full,
		Inline,
		Simple
	}

	internal delegate void Parser(ref ParserState arg);

	internal delegate void Accumulator(ref ParserState arg, int endIdx);

	[PublicAPI]
	internal ref struct ParserState(ReadOnlySpan<char> input, ParseMode mode = ParseMode.Full)
	{
		// Basic private state
		private readonly ReadOnlySpan<char> _stream      = input;
		private          bool               _closed      = false;
		private          int                _position    = 0;
		private          int                _length      = input.Length;
		private          int                _lastIdx     = input.Length - 1;
		private          int                _depth       = 0;
		private          Range?             _pendingText = null;
		private          bool               _skipLookup  = input.Length < LookupThreshold;

		// These are separated to improve performance & reduce allocations
		private AutoResizeArray<IMfmNode>       _results        = new();
		private AutoResizeArray<IMfmInlineNode> _recurseResults = new();

		// Cache for (possibly) expensive lookups
		private Dictionary<LookupEntry, int>? _lookupCache      = null;
		private HashSet<string>?              _unmatchedTags    = null;
		private MaterializedRecursionInfo     _recursionInfo    = default;
		private bool                          _recursionInfoSet = false;
		private int?                          _offset           = null;

		// Helper expression-bodied properties
		public int  Position    => _position;
		public int  Depth       => _depth;
		public int  Offset      => _offset ?? 0;
		public int  Length      => _length;
		public int  Remaining   => _length - _position;
		public int  LastIdx     => _lastIdx;
		public char CurrentChar => _stream[_position];
		public char PrevChar    => _stream[_position - 1];
		public bool IsEos       => _position == _length;
		public bool IsLast      => _position == _lastIdx;
		public bool IsStart     => _position == 0;

		public ReadOnlySpan<char> Stream => _stream;
		public ParseMode          Mode   => mode;

		public MaterializedRecursionInfo RecursionInfo
		{
			get
			{
				if (_recursionInfoSet)
					return _recursionInfo;
				_recursionInfoSet = true;
				return _recursionInfo = BuildRecursionInfo();
			}
		}

		public SearchValues<char> BoundaryChars = mode switch
		{
			ParseMode.Full   => BoundaryCharsFull,
			ParseMode.Inline => BoundaryCharsInline,
			ParseMode.Simple => BoundaryCharsSimple,
			_                => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
		};

		// Navigation methods
		public void Seek(int offset)
		{
			if (offset < 0)
				throw new InvalidOperationException("Backtracking is not allowed");

			_position += offset;

			if (_position > _length)
				_position = _length;
		}

		public void SeekTo(int dest)
		{
			if (dest < _position)
				throw new InvalidOperationException("Backtracking is not allowed");

			_position = Math.Min(dest, _length);
		}

		public void SeekToEnd() => _position = _length;

		// Recursion helper method
		public IMfmInlineNode[] Recurse(int end)
		{
			if (Mode is ParseMode.Simple)
				throw new InvalidOperationException("Cannot recurse in simple mode");

			var stream = _stream[_position..end];
			if (stream.Length == 0) return [];
			if (_depth == RecursionLimit || !stream.ContainsAny(BoundaryChars))
				return [new MfmTextNode(stream.ToString())];

			var state = new ParserState(stream, ParseMode.Inline)
			{
				_depth            = _depth + 1,
				_offset           = _position + (_offset ?? 0),
				_lookupCache      = _lookupCache,
				_unmatchedTags    = _unmatchedTags,
				_recursionInfo    = _recursionInfo,
				_recursionInfoSet = _recursionInfoSet
			};

			while (!state.IsEos)
				ParseNode(ref state);

			return state.GetRecurseResults();
		}

		// Pending text methods
		public void UpdatePendingText(int end, int lookbehind = 0)
		{
			var start = _position - lookbehind;
			if (start < 0)
				throw new InvalidOperationException("Tried to look behind start of stream");

			if (_pendingText is { } range)
			{
				if (start < range.Start.Value)
					throw new InvalidOperationException("Tried to look behind start of existing range");
				if (end < range.End.Value)
					throw new InvalidOperationException("Backtracking is not allowed");
				_pendingText = range.Start..end;
			}
			else
			{
				_pendingText = start..end;
			}
		}

		public void UpdatePendingTextBehindAndSeekToBoundary(int offset)
		{
			UpdatePendingText(_position, offset);
			UpdatePendingTextAndSeekToBoundary(0);
		}

		public void UpdatePendingTextAndSeek(int offset)
		{
			UpdatePendingText(_position + offset);
			Seek(offset);
		}

		public void UpdatePendingTextAndSeekTo(int end, int lookbehind = 0)
		{
			UpdatePendingText(end, lookbehind);
			SeekTo(end);
		}

		public void UpdatePendingTextAndSeekToBoundary(int minChars = 1)
		{
			UpdatePendingTextAndSeek(minChars);
			if (IsEos) return;
			var idx = IndexOfAnyBoundaryChar();
			if (idx == _position) return;
			if (idx == -1) UpdatePendingTextAndSeekToEnd();
			else UpdatePendingTextAndSeekTo(idx);
		}

		public void UpdatePendingTextAndSeekToEnd(int lookbehind = 0)
		{
			UpdatePendingText(_length, lookbehind);
			SeekToEnd();
		}

		private void MaterializePendingText()
		{
			if (_pendingText is not { } range) return;
			if (_depth == 0)
				_results.Add(new MfmTextNode(_stream[range].ToString()));
			else
				_recurseResults.Add(new MfmTextNode(_stream[range].ToString()));

			_pendingText = null;
		}

		// Result methods
		public void AddBlockResult(IMfmBlockNode node)
		{
			if (_depth != 0) throw new InvalidOperationException("Cannot add block result to recursive state");
			MaterializePendingText();
			_results.Add(node);
		}

		public void AddInlineResult(IMfmInlineNode node)
		{
			MaterializePendingText();
			if (_depth == 0)
				_results.Add(node);
			else
				_recurseResults.Add(node);
		}

		public IMfmNode[] GetResults()
		{
			if (_closed) throw new InvalidOperationException("This ParserState struct has already been closed.");
			_closed = true;
			MaterializePendingText();
			return _results.AsArray();
		}

		public IMfmInlineNode[] GetRecurseResults()
		{
			if (_closed) throw new InvalidOperationException("This ParserState struct has already been closed.");
			_closed = true;
			MaterializePendingText();
			return _recurseResults.AsArray();
		}

		// Match, read, slice & helper methods
		public bool MatchAhead(char match)
			=> Remaining >= 1 && CurrentChar == match;

		public bool MatchBehind(char match)
			=> _position >= 1 && PrevChar == match;

		public bool MatchAhead(ReadOnlySpan<char> match)
			=> Remaining >= match.Length && _stream.Slice(_position, match.Length).SequenceEqual(match);

		public bool MatchBehind(ReadOnlySpan<char> match)
			=> _position >= match.Length && _stream.Slice(_position - match.Length, match.Length).SequenceEqual(match);

		public bool MatchAnyAhead(SearchValues<char> match)
			=> !IsEos && match.Contains(CurrentChar);

		public bool MatchAnyBehind(SearchValues<char> match)
			=> !IsStart && match.Contains(_stream[_position - 1]);

		public bool MatchWhitespaceAhead(bool matchEos)
			=> (matchEos && IsEos) || (!IsEos && MatchAnyAhead(WhitespaceChars));

		public bool MatchWhitespaceBehind(bool matchStart)
			=> (matchStart && IsStart) || (!IsStart && MatchAnyBehind(WhitespaceChars));

		public bool MatchNewlineBehind(bool matchStart)
			=> (matchStart && IsStart) || (!IsStart && PrevChar == '\n');

		public ReadOnlySpan<char> ReadToEnd()             => _stream[_position..];
		public ReadOnlySpan<char> ReadTo(int end)         => _stream[_position..end];
		public char               ReadAt(int position)    => _stream[position];
		public ReadOnlySpan<char> Slice(Range range)      => _stream[range];
		public ReadOnlySpan<char> Slice(int end)          => _stream[_position..end];
		public ReadOnlySpan<char> SliceInclusive(int end) => _stream[_position..Math.Min(end, _lastIdx)];

		private        int WithPosition(int offset)         => offset is -1 ? -1 : _position + offset;
		private static int WithIndex(int offset, int index) => offset is -1 ? -1 : index + offset;

		// Index lookup methods
		private readonly record struct LookupEntry(
			string Method,
			string? String = null,
			char? Char = null,
			int? End = null,
			int? Start = null
		);

		private int? Lookup(ref LookupEntry key)
		{
			if (!(_lookupCache ??= []).TryGetValue(key, out var val))
				return null;
			if (val < 0)
				return val;
			val -= _offset ?? 0;
			if (val >= _position && val <= _lastIdx)
				return val;
			return null;
		}

		private int SetLookup(ref LookupEntry key, int val)
		{
			(_lookupCache ??= [])[key] = val < 0 ? val : val + (_offset ?? 0);
			return val;
		}

		public int IndexOfAny(SearchValues<char> sv) => WithPosition(_stream[_position..].IndexOfAny(sv));

		public int IndexOfAnyCached(SearchValues<char> sv, string name)
		{
			if (_skipLookup || Remaining < LookupThreshold)
				return IndexOfAny(sv);

			var key = new LookupEntry("IndexOfAny", name, null, _length + _offset);
			return Lookup(ref key) ?? SetLookup(ref key, IndexOfAny(sv));
		}

		public int IndexOfAny(SearchValues<char> sv, int end) => WithPosition(_stream[_position..end].IndexOfAny(sv));

		public int IndexOfAnyCached(SearchValues<char> sv, string name, int end)
		{
			if (_skipLookup || end - _position < LookupThreshold)
				return IndexOfAny(sv, end);

			var key = new LookupEntry("IndexOfAny", name, null, end + _offset);
			return Lookup(ref key) ?? SetLookup(ref key, IndexOfAny(sv, end));
		}

		public int IndexOfAnyOffset(SearchValues<char> sv, int start)
			=> WithIndex(_stream[start..].IndexOfAny(sv), start);

		public int IndexOfAnyOffsetCached(SearchValues<char> sv, string name, int start)
		{
			if (_skipLookup || _length - start < LookupThreshold)
				return IndexOfAnyOffset(sv, start);

			var key = new LookupEntry("IndexOfAny", name, null, null, start + _offset);
			return Lookup(ref key) ?? SetLookup(ref key, IndexOfAnyOffset(sv, start));
		}

		public int IndexOfAnyBoundaryChar() => mode switch
		{
			ParseMode.Full   => IndexOfAny(BoundaryCharsFull),
			ParseMode.Inline => IndexOfAny(BoundaryCharsInline),
			ParseMode.Simple => IndexOfAny(BoundaryCharsSimple),
			_                => throw new ArgumentOutOfRangeException()
		};

		public int IndexOf(string sequence, Range range)
			=> WithIndex(_stream[range].IndexOf(sequence), range.Start.Value);

		public int IndexOf(string sequence, int end)
			=> WithPosition(_stream[_position..end].IndexOf(sequence));

		public int IndexOf(string sequence) => WithPosition(_stream[_position..].IndexOf(sequence));

		public int IndexOf(char c, Range range) => WithIndex(_stream[range].IndexOf(c), range.Start.Value);

		public int IndexOf(char c, int? end) => WithPosition(_stream[_position..(end ?? _length)].IndexOf(c));

		public int IndexOfCached(char c, int? end)
		{
			end ??= _length;

			if (_skipLookup || end.Value - _position < LookupThreshold)
				return IndexOf(c, end);

			var key = new LookupEntry("IndexOf", null, c, end + Offset);
			return Lookup(ref key) ?? SetLookup(ref key, IndexOf(c, end));
		}

		public int IndexOf(char c) => WithPosition(_stream[_position..].IndexOf(c));

		public int IndexOfCached(char c)
		{
			if (_skipLookup || Remaining < LookupThreshold)
				return IndexOf(c);

			var key = new LookupEntry("IndexOf", null, c, _length + _offset);
			return Lookup(ref key) ?? SetLookup(ref key, IndexOf(c));
		}

		public int LastIndexOf(char c, int? end) => WithPosition(_stream[_position..(end ?? _length)].LastIndexOf(c));

		public int? IndexOfOrNull(char c)       => IndexOf(c) is var idx && idx < 0 ? null : idx;
		public int? IndexOfOrNullCached(char c) => IndexOfCached(c) is var idx && idx < 0 ? null : idx;

		public int IndexOfExcept(char match, ReadOnlySpan<char> except, int? end = null)
		{
			var stream = end != null ? _stream[_position..end.Value] : _stream[_position..];

			var idx = stream.IndexOf(match);
			if (idx == -1) return -1;

			while (
				idx != -1
				&& idx + except.Length < stream.Length
				&& stream[idx..(idx + except.Length)].SequenceEqual(except)
			)
			{
				idx += except.Length;
				idx =  WithIndex(stream[idx..].IndexOf(match), idx);
			}

			return WithPosition(idx);
		}

		// Closing tag methods
		public bool HasUnmatchedTag(string tag) => _unmatchedTags?.Contains(tag) ?? false;

		public void AddUnmatchedTag(string tag)
		{
			if (_skipLookup && _unmatchedTags is null) return;
			(_unmatchedTags ??= []).Add(tag);
		}

		// RecursionInfo generator
		private static string[] _openTags  = ["$[", "<b>", "<i>", "<s>", "<plain>", "<small>", "<center>"];
		private static string[] _closeTags = ["]", "</b>", "</i>", "</s>", "</plain>", "</small>", "</center>"];
		private static string[] _allTags   = [.._openTags, .._closeTags];
		private static int      _tagCount  = _openTags.Length;

		public static ushort GetOpenTagIdx(string tag) => (ushort)_openTags.AsSpan().IndexOf(tag);

		private static SearchValues<string> _allTagsSv = SearchValues.Create(_allTags, StringComparison.Ordinal);

		private static AutoResizeArray<int>[]
			_emptyLutMap = _openTags.Select(_ => AutoResizeArray<int>.Default).ToArray();

		public static class RecursionInfoConstants
		{
			public const int OpenBitIdx   = 17;
			public const int OpenBitmask  = 1 << OpenBitIdx;
			public const int IndexBitmask = ~(1 << OpenBitIdx);

			public const int OpenFlag  = 1 << OpenBitIdx;
			public const int CloseFlag = 0;
		}

		public readonly ref struct MaterializedRecursionInfo(Span<AutoResizeArray<int>> luts)
		{
			public readonly Span<AutoResizeArray<int>> Luts     = luts;
			public readonly Span<int>                  Pointers = new int[luts.Length];
		}

		public MaterializedRecursionInfo BuildRecursionInfo()
		{
			var luts = new AutoResizeArray<int>[_tagCount].AsSpan();
			_emptyLutMap.CopyTo(luts);
			var offset = Offset;

			var i      = -1;
			var length = Length;
			while (++i < length)
			{
				var next = _stream[i..].IndexOfAny(_allTagsSv);
				if (next is -1)
					break;

				i += next;

				var (tagIdx, open) = _stream[i] switch
				{
					'$' => (0, 1), // $[
					']' => (0, 0), // ]
					'<' => _stream[i + 1] switch
					{
						'b' => (1, 1), // <b>
						'i' => (2, 1), // <i>
						'p' => (4, 1), // <plain>
						'c' => (6, 1), // <center>
						's' => (_stream[i + 2] switch
						{
							'm' => 5, // <small>
							'>' => 3, // <s>
							_   => -1
						}, 1),
						'/' => (_stream[i + 2] switch
						{
							'b' => 1, // </b>
							'i' => 2, // </i>
							'p' => 4, // </plain>
							'c' => 6, // </center>
							's' => _stream[i + 3] switch
							{
								'm' => 5, // </small>
								'>' => 3, // </s>
								_   => -1
							},
							_ => -1
						}, 0),
						_ => (-1, 0)
					},
					_ => (-1, 0)
				};

				luts[tagIdx].Add((i + offset) | open << OpenBitIdx);
			}

			return new MaterializedRecursionInfo(luts);
		}
	}

	// General use char arrays
	private static readonly char[] NodeBoundariesSimple      = ":<".ToCharArray();
	private static readonly char[] NodeBoundariesInline      = "*_@#`h:~[<\\$?".ToCharArray();
	private static readonly char[] NodeBoundariesFull        = NodeBoundariesInline.Concat("\n>").ToArray();
	private static readonly char[] Whitespace                = " \n".ToCharArray();
	private static readonly char[] AsciiLettersLower         = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
	private static readonly char[] AsciiLettersUpper         = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
	private static readonly char[] AsciiLetters              = AsciiLettersLower.Concat(AsciiLettersUpper).ToArray();
	private static readonly char[] AsciiSymbols              = " !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~".ToCharArray();
	private static readonly char[] AsciiSymbolsAndWhitespace = AsciiSymbols.Append('\n').ToArray();
	private static readonly char[] Digits                    = "0123456789".ToCharArray();
	private static readonly char[] AsciiLettersAndDigits     = AsciiLetters.Concat(Digits).ToArray();

	// Static SearchValues instances - general use
	private static readonly SearchValues<char> BoundaryCharsSimple      = SearchValues.Create(NodeBoundariesSimple);
	private static readonly SearchValues<char> BoundaryCharsFull        = SearchValues.Create(NodeBoundariesFull);
	private static readonly SearchValues<char> BoundaryCharsInline      = SearchValues.Create(NodeBoundariesInline);
	private static readonly SearchValues<char> WhitespaceChars          = SearchValues.Create(Whitespace);
	private static readonly SearchValues<char> AsciiLetterAndDigitChars = SearchValues.Create(AsciiLettersAndDigits);

	private static readonly SearchValues<char> AsciiSymbolsAndWhitespaceChars =
		SearchValues.Create(AsciiSymbolsAndWhitespace);

	// Static SearchValues instances - specific use
	private static readonly SearchValues<char> HashtagBoundaryChars =
		SearchValues.Create(AsciiSymbols.Append('\n').Except("_-").ToArray());

	private static readonly SearchValues<char> EmojiCodeAllowedChars =
		SearchValues.Create(AsciiLettersAndDigits.Concat("+-_").ToArray());

	private static readonly SearchValues<char> ParenthesisChars =
		SearchValues.Create("()");

	private static readonly SearchValues<char> MentionUserDisallowedChars =
		SearchValues.Create(AsciiSymbols.Append('\n').Except("-_.").ToArray());

	private static readonly SearchValues<char> MentionHostAllowedChars =
		SearchValues.Create(AsciiLettersAndDigits.Concat("-_.").ToArray());

	private static readonly SearchValues<char> FnDescriptorAllowedChars =
		SearchValues.Create(AsciiLettersLower.Concat(Digits).Concat("=.,_-").ToArray());

	private static readonly SearchValues<char> FnKeyAllowedChars =
		SearchValues.Create(AsciiLettersLower.Concat(Digits).Append('_').ToArray());

	private static readonly SearchValues<char> FnArgValueAllowedChars =
		SearchValues.Create(AsciiLettersLower.Concat(Digits).Concat("_-.").ToArray());

	private static readonly SearchValues<string> FnArgsExcludeSequences
		= SearchValues.Create([",,", "==", ",=", "=,"], StringComparison.Ordinal);

	// Main parser
	private static void ParseNode(ref ParserState state)
	{
		if (state.IsEos) return;
		var position = state.Position;

		Parser? parser = null;

		// Block nodes, ordered by expected frequency
		if (state.Mode is ParseMode.Full)
		{
			parser = state.CurrentChar switch
			{
				'`'  => ParseCodeBlockOrInlineCode(state),
				'\n' => TryParseCodeBlock(state),
				'>'  => ParseQuote,
				'<'  => ParseTag(state),
				'\\' => ParseInlineMathOrMathBlock(state),
				_    => null
			};
		}

		// Inline nodes, ordered by expected frequency
		parser ??= state.CurrentChar switch
		{
			'*'  => ParseAsterisk(state),
			'_'  => ParseUnderscore(state),
			'@'  => ParseMention,
			'#'  => ParseHashtag,
			'`'  => ParseInlineCode,
			'h'  => ParseUrl,
			':'  => ParseEmojiCode,
			'~'  => ParseTilde(state),
			'['  => ParseLink,
			'<'  => ParseInlineTag(state),
			'\\' => TryParseInlineMath(state),
			'$'  => TryParseFn(state),
			'?'  => ParseLink,
			_    => ParseText
		};

		parser(ref state);

		if (state.Position == position)
		{
#if DEBUG || FUZZ
			throw new Exception("Infinite loop detected!");
#else
			state.UpdatePendingTextAndSeekToBoundary();
#endif
		}
	}

	private static void ParseNodeSimple(ref ParserState state)
	{
		if (state.IsEos) return;
		var position = state.Position;

		Parser parser = state.CurrentChar switch
		{
			':'                                  => ParseEmojiCode,
			'<' when state.MatchAhead("<plain>") => ParsePlainTag,
			_                                    => ParseText
		};

		parser(ref state);

		if (state.Position == position)
		{
#if DEBUG || FUZZ
			throw new Exception("Infinite loop detected!");
#else
			state.UpdatePendingTextAndSeekToBoundary();
#endif
		}
	}

	private static Parser ParseAsterisk(ParserState state)
		=> state.MatchAhead("**") ? ParseBoldAsterisk : ParseItalicAsterisk;

	private static Parser ParseUnderscore(ParserState state)
		=> state.MatchAhead("__") ? ParseBoldUnderscore : ParseItalicUnderscore;

	private static Parser ParseTilde(ParserState state)
		=> state.MatchAhead("~~") ? ParseStrikeTilde : ParseText;

	private static void ParseText(ref ParserState state) => state.UpdatePendingTextAndSeekToBoundary();

	private static Parser ParseInlineTag(ParserState state)
	{
		// shortest tag pair is 6 characters long
		if (state.Remaining < 6) return ParseText;

		var stream = state.Stream;
		var pos    = state.Position;

		return stream[pos + 1] switch
		{
			'b' when stream[pos + 2] is '>'        => ParseBoldTag,
			'i' when stream[pos + 2] is '>'        => ParseItalicTag,
			's' when stream[pos + 2] is '>'        => ParseStrikeTag,
			's' when state.MatchAhead("<small>")   => ParseSmallTag,
			'p' when state.MatchAhead("<plain>")   => ParsePlainTag,
			'h' when state.MatchAhead("<http://")  => ParseUrlBrackets,
			'h' when state.MatchAhead("<https://") => ParseUrlBrackets,
			//
			_ => ParseText
		};
	}

	private static Parser ParseTag(ParserState state)
		=> state.Remaining < 6
			? ParseText
			: state.Stream[state.Position + 1] == 'c' && state.MatchAhead("<center>")
				? ParseCenterTag
				: ParseInlineTag(state);

	private static void ParseHashtag(ref ParserState state)
	{
		const int delimLength = 1;
		if (!state.MatchWhitespaceBehind(true) || state.MatchWhitespaceAhead(true) || state.Remaining == 1)
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return;
		}

		state.Seek(delimLength);
		var endIdx = state.IndexOfAny(HashtagBoundaryChars);
		if (endIdx == state.Position)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return;
		}

		if (endIdx == -1)
		{
			state.AddInlineResult(new MfmHashtagNode(state.ReadToEnd().ToString()));
			state.SeekToEnd();
			return;
		}

		state.AddInlineResult(new MfmHashtagNode(state.ReadTo(endIdx).ToString()));
		state.SeekTo(endIdx);
	}

	private static void ParseEmojiCode(ref ParserState state)
	{
		const int  delimLength = 1;
		const char delim       = ':';

		state.Seek(delimLength);

		var endIdx = state.IndexOf(delim);
		if (endIdx == -1 || endIdx == state.Position || state.Slice(endIdx).ContainsAnyExcept(EmojiCodeAllowedChars))
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return;
		}

		state.AddInlineResult(new MfmEmojiCodeNode(state.ReadTo(endIdx).ToString()));
		state.SeekTo(endIdx);
		state.Seek(delimLength);
	}

	private static void ParseInlineCode(ref ParserState state)
	{
		const int  delimLength = 1;
		const char delim       = '`';

		state.Seek(delimLength);

		var endIdx = state.IndexOf(delim, state.IndexOfOrNullCached('\n'));
		if (endIdx == -1 || endIdx == state.Position)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return;
		}

		state.AddInlineResult(new MfmInlineCodeNode(state.ReadTo(endIdx).ToString()));
		state.SeekTo(endIdx);
		state.Seek(delimLength);
	}

	private static void ParseUrl(ref ParserState state)
	{
		if (!state.MatchAhead("https://") && !state.MatchAhead("http://"))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return;
		}

		var end = state.IndexOfAnyCached(WhitespaceChars, nameof(WhitespaceChars));

		if (end == -1)
			end = state.Length;

		var closeBracketIdx = state.IndexOf(')', end);
		if (closeBracketIdx != -1)
		{
			var openBracketIdx = state.IndexOf('(', closeBracketIdx);
			if (openBracketIdx == -1)
			{
				end = closeBracketIdx;
			}
			else
			{
				var bracketStack = 1;
				var slice        = state.Slice(++openBracketIdx..end);

				var i = -1;
				while (bracketStack >= 0 && ++i < slice.Length)
				{
					var next = slice[i..].IndexOfAny(ParenthesisChars);
					if (next == -1) break;
					i += next;

					bracketStack += slice[i] == '(' ? 1 : -1;
					if (bracketStack == -1)
						end = i + openBracketIdx;
				}

				if (bracketStack > RecursionLimit)
				{
					state.UpdatePendingTextAndSeekTo(end);
					return;
				}
			}
		}

		if (
			Uri.TryCreate(state.ReadTo(end).ToString(), UriKind.Absolute, out var uri)
			&& uri is { Scheme: "http" or "https" }
		)
		{
			state.AddInlineResult(new MfmUrlNode(uri.ToString(), false));
			state.SeekTo(end);
		}
		else
		{
			state.UpdatePendingTextAndSeekTo(end);
		}
	}

	private static void ParseUrlBrackets(ref ParserState state)
	{
		if (state.HasUnmatchedTag(">"))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return;
		}

		state.Seek(1);

		var end = state.IndexOf('>');
		if (end == -1) state.AddUnmatchedTag(">");
		if (end == -1 || state.IndexOf('\n', end) != -1)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(1);
			return;
		}

		if (
			Uri.TryCreate(state.ReadTo(end).ToString(), UriKind.Absolute, out var uri)
			&& uri is { Scheme: "http" or "https" }
		)
		{
			state.AddInlineResult(new MfmUrlNode(uri.ToString(), true));
			state.SeekTo(end);
			state.Seek(1);
		}
		else
		{
			state.UpdatePendingTextAndSeekTo(end + 1, 1);
		}
	}

	private static void ParseLink(ref ParserState state)
	{
		var silent      = state.MatchAhead('?');
		var delimLength = silent ? 2 : 1;

		if (silent && state.Remaining == 1)
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return;
		}

		if (state.HasUnmatchedTag("]") || state.HasUnmatchedTag(")"))
		{
			state.UpdatePendingTextAndSeekToBoundary(delimLength);
			return;
		}

		if (silent && !state.MatchAhead("?["))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return;
		}

		state.Seek(delimLength);

		var textEnd = state.IndexOfCached(']');
		if (textEnd == -1) state.AddUnmatchedTag("]");
		if (
			textEnd == -1
			|| textEnd == state.Position
			|| state.IndexOfCached('\n', textEnd) != -1
			|| textEnd > state.LastIdx - "(http://)".Length
			|| state.ReadAt(textEnd + 1) != '('
		)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return;
		}

		var linkStart = textEnd + 2;
		var linkEnd   = state.IndexOfAnyOffsetCached(WhitespaceChars, nameof(WhitespaceChars), linkStart);

		if (linkEnd == -1)
			linkEnd = state.Length;

		var closeBracketIdx = state.IndexOf(')', linkStart..linkEnd);
		if (closeBracketIdx == -1)
		{
			if (state.IndexOfCached(')') == -1) state.AddUnmatchedTag(")");
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return;
		}

		var openBracketIdx = state.IndexOf('(', linkStart..closeBracketIdx);
		if (openBracketIdx == -1)
		{
			linkEnd = closeBracketIdx;
		}
		else
		{
			var bracketStack = 1;
			var slice        = state.Slice(++openBracketIdx..linkEnd);

			var i = -1;
			while (bracketStack >= 0 && ++i < slice.Length)
			{
				var next = slice[i..].IndexOfAny(ParenthesisChars);
				if (next == -1) break;
				i += next;

				bracketStack += slice[i] == '(' ? 1 : -1;
				if (bracketStack == -1)
					linkEnd = i + openBracketIdx;
			}

			if (bracketStack > RecursionLimit)
			{
				state.UpdatePendingTextAndSeekTo(linkEnd, delimLength);
				return;
			}

			if (bracketStack > -1)
			{
				state.AddUnmatchedTag(")");
				state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
				return;
			}
		}

		if (
			Uri.TryCreate(state.Slice(linkStart..linkEnd).ToString(), UriKind.Absolute, out var uri)
			&& uri is { Scheme: "http" or "https" }
		)
		{
			state.AddInlineResult(new MfmLinkNode(uri.ToString(), state.ReadTo(textEnd).ToString(), silent));
			state.SeekTo(linkEnd);
			state.Seek(1);
		}
		else
		{
			state.UpdatePendingTextAndSeekTo(linkEnd + 1, delimLength);
		}
	}

	private static void ParseMention(ref ParserState state)
	{
		if (!state.MatchWhitespaceBehind(true) && !ParenthesisChars.Contains(state.PrevChar))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return;
		}

		state.Seek(1);

		var end = state.IndexOfAnyCached(WhitespaceChars, nameof(WhitespaceChars));
		if (end == -1)
			end = state.Length;

		if (end - state.Position <= 1)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(1);
			return;
		}

		var hostPartIdx = state.IndexOf('@', end);
		var localSlice  = state.Slice(hostPartIdx == -1 ? end : hostPartIdx);
		var nextCharIdx = state.Position + localSlice.Length + 1;

		if (
			hostPartIdx == -1
			&& localSlice.Length > 1
			&& (".-".Contains(localSlice[^1])
			    || (localSlice[^1] == ':'
			        && (state.LastIdx < nextCharIdx
			            || !AsciiSymbolsAndWhitespaceChars.Contains(state.ReadAt(nextCharIdx)))))
		)
		{
			end--;
			localSlice = localSlice[..^1];
		}

		if (
			localSlice.Length == 0
			|| (hostPartIdx != -1
			    && (end - hostPartIdx < 2 || ".-".Contains(localSlice[0]) || ".-".Contains(localSlice[^1])))
			|| localSlice.ContainsAny(MentionUserDisallowedChars)
		)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(1);
			return;
		}

		var hostSlice = ReadOnlySpan<char>.Empty;
		if (hostPartIdx != -1)
		{
			hostSlice = state.Slice((hostPartIdx + 1)..end);
			var earlyEndIdx = hostSlice.IndexOfAnyExcept(MentionHostAllowedChars);
			if (
				earlyEndIdx != -1
				&& earlyEndIdx != hostSlice.Length - 1
				&& AsciiLetterAndDigitChars.Contains(hostSlice[earlyEndIdx + 1])
			)
			{
				state.UpdatePendingTextBehindAndSeekToBoundary(1);
				return;
			}

			if (earlyEndIdx != -1)
			{
				end       = earlyEndIdx + hostPartIdx + 1;
				hostSlice = hostSlice[..earlyEndIdx];
			}

			if (hostSlice.Length > 0 && "-.".Contains(hostSlice[^1]))
			{
				end--;
				hostSlice = hostSlice[..^1];
			}

			if (hostSlice.Length > 0 && ("-.".Contains(hostSlice[0]) || !hostSlice.Contains('.')))
			{
				state.UpdatePendingTextBehindAndSeekToBoundary(1);
				return;
			}
		}

		var user = localSlice.ToString();
		var host = hostSlice.Length > 0 ? hostSlice.ToString() : null;
		state.AddInlineResult(new MfmMentionNode(user, host));
		state.SeekTo(end);
	}

	private static void ParseQuote(ref ParserState state)
	{
		const int quoteRecursionLimit = 4;

		if (!state.MatchNewlineBehind(true) || state.Remaining < 2)
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return;
		}

		state.Seek(1);
		var lookbehind   = 1;
		var currentDepth = 0;
		while (!state.IsEos && state.CurrentChar == '>' && currentDepth < quoteRecursionLimit)
		{
			state.Seek(1);
			currentDepth++;
		}

		lookbehind += currentDepth;

		if (state.IsEos)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(lookbehind);
			return;
		}

		if (state.CurrentChar == ' ')
		{
			state.Seek(1);
			lookbehind++;
		}

		if (state.CurrentChar == '\n')
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(lookbehind);
			return;
		}

		lookbehind = 0;

		var end = state.IndexOfCached('\n');
		if (end == -1)
			end = state.Length;

		// @formatter:off
		AutoResizeArray<IMfmInlineNode> results = new();
		Stack<(int depth, AutoResizeArray<IMfmInlineNode> results)> stack = [];
		// @formatter:on

		while (end != -1)
		{
			lookbehind = 0;

			if (results.Count > 0 && results[^1] is not MfmQuoteNode)
				results.Add(new MfmTextNode("\n"));

			results.AddRange(state.Recurse(end));
			state.SeekTo(end);

			if (!state.MatchAhead("\n>"))
				break;

			state.Seek(2);
			end = state.IndexOfCached('\n');

			lookbehind = 2;
			var depth = 0;
			while (!state.IsEos && state.CurrentChar == '>' && depth < quoteRecursionLimit)
			{
				state.Seek(1);
				depth++;
			}

			lookbehind += depth;

			if (state.IsEos)
				break;

			if (state.CurrentChar == ' ')
			{
				state.Seek(1);
				lookbehind++;
			}

			if (state.CurrentChar == '\n' && !state.MatchAhead("\n>"))
				break;

			if (end == -1)
				end = state.Length;

			if (depth > 0 || currentDepth > 0)
			{
				if (currentDepth == depth)
					continue;

				if (currentDepth > depth)
				{
					while (stack.TryPeek(out var head) && head.depth >= depth)
					{
						stack.Pop();
						var nestQuote = new MfmQuoteNode(results.ToArray(), nested: true);
						while (--currentDepth > head.depth)
							nestQuote = new MfmQuoteNode([nestQuote], nested: true);
						results = head.results.Add(nestQuote);
					}

					if (currentDepth > depth)
					{
						var nestQuote = new MfmQuoteNode(results.ToArray(), nested: true);
						results = new([nestQuote]);

						while (--currentDepth > depth)
						{
							nestQuote = new MfmQuoteNode([nestQuote], nested: true);
							results   = new([nestQuote]);
						}
					}

					continue;
				}

				// currentDepth is < depth
				stack.Push((currentDepth, results));
				results      = new();
				currentDepth = depth;
			}
		}

		while (stack.TryPop(out var head))
		{
			var nestQuote = new MfmQuoteNode(results.ToArray(), nested: true);
			while (--currentDepth > head.depth)
				nestQuote = new MfmQuoteNode([nestQuote], nested: true);
			results = head.results.Add(nestQuote);
		}

		while (currentDepth-- > 0)
			results = new([new MfmQuoteNode(results.ToArray(), nested: true)]);

		state.AddInlineResult(new MfmQuoteNode(results.ToArray()));

		if (lookbehind > 0)
			state.UpdatePendingTextBehindAndSeekToBoundary(lookbehind);
	}

	private static Parser TryParseFn(ParserState state) => state.MatchAhead("$[") ? ParseFnTag : ParseText;

	private static Parser TryParseInlineMath(ParserState state)
		=> state.MatchAhead("\\(") ? ParseInlineMath : ParseText;

	private static Parser ParseInlineMathOrMathBlock(ParserState state)
		=> state.MatchAhead("\\(")
			? ParseInlineMath
			: state.MatchAhead("\\[")
				? ParseMathBlock
				: ParseText;

	private static Parser TryParseCodeBlock(ParserState state)
		=> state.MatchAhead("\n```") || state.MatchAhead("\n\n```") ? ParseCodeBlock : ParseText;

	private static Parser ParseCodeBlockOrInlineCode(ParserState state)
		=> state.IsStart && state.MatchAhead("```") ? ParseCodeBlock : ParseInlineCode;

	private static void ItalicAccumulator(ref ParserState state, int endIdx)
	{
		var type = state.PrevChar switch
		{
			'*' => MfmItalicNode.DelimiterType.Asterisk,
			'_' => MfmItalicNode.DelimiterType.Underscore,
			_   => MfmItalicNode.DelimiterType.HtmlTag
		};

		state.AddInlineResult(new MfmItalicNode(state.Recurse(endIdx), type));
	}

	private static void BoldAccumulator(ref ParserState state, int endIdx)
	{
		var type = state.PrevChar switch
		{
			'*' => MfmBoldNode.DelimiterType.Asterisk,
			'_' => MfmBoldNode.DelimiterType.Underscore,
			_   => MfmBoldNode.DelimiterType.HtmlTag
		};

		state.AddInlineResult(new MfmBoldNode(state.Recurse(endIdx), type));
	}

	private static void StrikeAccumulator(ref ParserState state, int endIdx)
	{
		var type = state.PrevChar switch
		{
			'~' => MfmStrikeNode.DelimiterType.Tilde,
			_   => MfmStrikeNode.DelimiterType.HtmlTag
		};

		state.AddInlineResult(new MfmStrikeNode(state.Recurse(endIdx), type));
	}

	private static void InlineMathAccumulator(ref ParserState state, int endIdx)
		=> state.AddInlineResult(new MfmInlineMathNode(state.ReadTo(endIdx).ToString()));

	private static void MathBlockAccumulator(ref ParserState state, int endIdx)
		=> state.AddBlockResult(new MfmMathBlockNode(state.ReadTo(endIdx).ToString()));

	private static void CodeBlockAccumulator(ref ParserState state, int endIdx)
	{
		if (endIdx == state.Position)
			return;

		string? lang = null;
		if (state.CurrentChar != '\n')
		{
			var newlineIdx = state.IndexOf('\n', endIdx);
			if (newlineIdx != -1)
			{
				lang = state.ReadTo(newlineIdx).ToString();
				state.SeekTo(newlineIdx);
			}
		}

		state.Seek(1);
		state.AddBlockResult(new MfmCodeBlockNode(state.ReadTo(endIdx).ToString(), lang));
	}

	private static void CenterAccumulator(ref ParserState state, int endIdx)
	{
		if (state.CurrentChar == '\n' && state.Position < endIdx)
			state.Seek(1);
		if (state.Position < endIdx && state.ReadAt(endIdx - 1) == '\n')
			endIdx--;

		state.AddBlockResult(new MfmCenterNode(state.Recurse(endIdx)));
	}

	private static void SmallAccumulator(ref ParserState state, int endIdx)
		=> state.AddInlineResult(new MfmSmallNode(state.Recurse(endIdx)));

	private static void PlainAccumulator(ref ParserState state, int endIdx)
		=> state.AddInlineResult(new MfmPlainNode(state.ReadTo(endIdx).ToString()));

	private static void FnAccumulator(ref ParserState state, int endIdx)
	{
		var descriptorEndIdx = state.IndexOf(' ', endIdx);
		if (descriptorEndIdx == -1 || endIdx - descriptorEndIdx <= 1)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(2);
			return;
		}

		var nameAndArgs = state.Slice(descriptorEndIdx);
		if (nameAndArgs.Length == 0 || nameAndArgs.ContainsAnyExcept(FnDescriptorAllowedChars))
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(2);
			return;
		}

		var argsIdx = nameAndArgs.IndexOf('.');
		var name    = argsIdx != -1 ? nameAndArgs[..argsIdx] : nameAndArgs;

		if (name.Length == 0 || name.ContainsAnyExcept(FnKeyAllowedChars))
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(2);
			return;
		}

		Dictionary<string, string?>? args = null;

		if (argsIdx != -1)
		{
			var argsSlice = nameAndArgs[++argsIdx..];
			if (
				argsSlice.Length == 0
				|| argsSlice[0] == ','
				|| argsSlice[^1] == ','
				|| argsSlice.EndsWith('=')
				|| argsSlice.ContainsAny(FnArgsExcludeSequences)
			)
			{
				state.UpdatePendingTextBehindAndSeekToBoundary(2);
				return;
			}

			args = [];
			var argsSplit = argsSlice.Split(',');
			foreach (var range in argsSplit)
			{
				var arg       = argsSlice[range];
				var argSepIdx = arg.IndexOf('=');

				if (argSepIdx == -1)
				{
					if (arg.ContainsAnyExcept(FnKeyAllowedChars))
					{
						state.UpdatePendingTextBehindAndSeekToBoundary(2);
						return;
					}

					args[arg.ToString()] = null;
					continue;
				}

				var key   = arg[..argSepIdx];
				var value = arg[++argSepIdx..];
				if (key.ContainsAnyExcept(FnKeyAllowedChars) || value.ContainsAnyExcept(FnArgValueAllowedChars))
				{
					state.UpdatePendingTextBehindAndSeekToBoundary(2);
					return;
				}

				args[key.ToString()] = value.ToString();
			}
		}

		state.SeekTo(descriptorEndIdx);
		state.Seek(1);
		state.AddInlineResult(new MfmFnNode(name.ToString(), args, state.Recurse(endIdx)));
		state.SeekTo(endIdx);
		state.Seek(1);
	}

	private static MarkupChar   _italicAsterisk   = new('*', "**", ItalicAccumulator);
	private static MarkupChar   _italicUnderscore = new('_', "__", ItalicAccumulator);
	private static MarkupString _boldAsterisk     = new("**", BoldAccumulator);
	private static MarkupString _boldUnderscore   = new("__", BoldAccumulator);
	private static MarkupString _strikeTilde      = new("~~", StrikeAccumulator);
	private static MarkupTag    _italicTag        = new("<i>", "</i>", ItalicAccumulator);
	private static MarkupTag    _boldTag          = new("<b>", "</b>", BoldAccumulator);
	private static MarkupTag    _strikeTag        = new("<s>", "</s>", StrikeAccumulator);
	private static MarkupTag    _smallTag         = new("<small>", "</small>", SmallAccumulator);
	private static MarkupTag    _fnTag            = new("$[", "]", FnAccumulator, seekToEnd: false);
	private static MarkupTag    _plainTag         = new("<plain>", "</plain>", PlainAccumulator, allowNesting: false);

	private static MarkupTag _centerTag =
		new("<center>", "</center>", CenterAccumulator, requireStartOfLine: true);

	private static MarkupTag _inlineMathTag =
		new("\\(", "\\)", InlineMathAccumulator, sameLine: true, allowNesting: false);

	private static MarkupTag _mathBlockTag =
		new("\\[", "\\]", MathBlockAccumulator, allowNesting: false, requireStartOfLine: true);

	private static MarkupTag _codeBlockTag =
		new("```", "\n```", CodeBlockAccumulator, allowNesting: false, consumeMaxLeadingNewlines: 2);

	// @formatter:off
	private static void ParseItalicAsterisk(ref ParserState state)   => ParseMarkupNode(ref state, ref _italicAsterisk);
	private static void ParseItalicUnderscore(ref ParserState state) => ParseMarkupNode(ref state, ref _italicUnderscore);
	private static void ParseBoldAsterisk(ref ParserState state)     => ParseMarkupNode(ref state, ref _boldAsterisk);
	private static void ParseBoldUnderscore(ref ParserState state)   => ParseMarkupNode(ref state, ref _boldUnderscore);
	private static void ParseStrikeTilde(ref ParserState state)      => ParseMarkupNode(ref state, ref _strikeTilde);
	private static void ParseItalicTag(ref ParserState state)        => ParseMarkupNode(ref state, ref _italicTag);
	private static void ParseBoldTag(ref ParserState state)          => ParseMarkupNode(ref state, ref _boldTag);
	private static void ParseStrikeTag(ref ParserState state)        => ParseMarkupNode(ref state, ref _strikeTag);
	private static void ParseSmallTag(ref ParserState state)         => ParseMarkupNode(ref state, ref _smallTag);
	private static void ParseFnTag(ref ParserState state)            => ParseMarkupNode(ref state, ref _fnTag);
	private static void ParsePlainTag(ref ParserState state)         => ParseMarkupNode(ref state, ref _plainTag);
	private static void ParseCenterTag(ref ParserState state)        => ParseMarkupNode(ref state, ref _centerTag);
	private static void ParseInlineMath(ref ParserState state)       => ParseMarkupNode(ref state, ref _inlineMathTag);
	private static void ParseMathBlock(ref ParserState state)        => ParseMarkupNode(ref state, ref _mathBlockTag);
	private static void ParseCodeBlock(ref ParserState state)        => ParseMarkupNode(ref state, ref _codeBlockTag);
	// @formatter:on

	#region MarkupNode Helpers

	private readonly struct MarkupChar(
		char delim,
		string except,
		Accumulator accumulator,
		bool sameLine = true
	)
	{
		public readonly char        Delim      = delim;
		public readonly string      Except     = except;
		public readonly Accumulator Accumulate = accumulator;
		public readonly bool        SameLine   = sameLine;
	}

	private static void ParseMarkupNode(ref ParserState state, ref MarkupChar desc)
	{
		const int delimLength = 1;
		state.Seek(delimLength);

		var endIdx =
			state.IndexOfExcept(desc.Delim, desc.Except, desc.SameLine ? state.IndexOfOrNullCached('\n') : null);

		if (
			endIdx == -1
			|| (state.Position > delimLength
			    && !AsciiSymbolsAndWhitespaceChars.Contains(state.ReadAt(state.Position - delimLength - 1)))
			|| (endIdx < state.Length - delimLength
			    && !AsciiSymbolsAndWhitespaceChars.Contains(state.ReadAt(endIdx + delimLength)))
		)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return;
		}

		desc.Accumulate(ref state, endIdx);
		state.SeekTo(endIdx + delimLength);
	}

	private readonly struct MarkupString(string delim, Accumulator accumulator, bool sameLine = true)
	{
		public readonly string      Delim       = delim;
		public readonly Accumulator Accumulate  = accumulator;
		public readonly ushort      DelimLength = (ushort)delim.Length;
		public readonly bool        SameLine    = sameLine;
	}

	private static void ParseMarkupNode(ref ParserState state, ref MarkupString desc)
	{
		state.Seek(desc.DelimLength);

		var endIdx = desc.SameLine
			? state.IndexOf(desc.Delim, state.IndexOfOrNullCached('\n') ?? state.Length)
			: state.IndexOf(desc.Delim);

		if (
			endIdx == -1
			|| (state.Position > desc.DelimLength
			    && !AsciiSymbolsAndWhitespaceChars.Contains(state.ReadAt(state.Position - desc.DelimLength - 1)))
			|| (endIdx < state.Length - desc.DelimLength
			    && !AsciiSymbolsAndWhitespaceChars.Contains(state.ReadAt(endIdx + desc.DelimLength)))
		)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(desc.DelimLength);
			return;
		}

		desc.Accumulate(ref state, endIdx);
		state.SeekTo(endIdx + desc.DelimLength);
	}

	private readonly struct MarkupTag(
		string openTag,
		string closeTag,
		Accumulator accumulator,
		bool sameLine = false,
		bool seekToEnd = true,
		bool allowNesting = true,
		bool requireStartOfLine = false,
		ushort consumeMaxLeadingNewlines = 0,
		ushort consumeMaxTrailingNewlines = 0
	)
	{
		public readonly string      CloseTag   = closeTag;
		public readonly Accumulator Accumulate = accumulator;

		public readonly ushort OpenTagLength  = (ushort)openTag.Length;
		public readonly ushort CloseTagLength = (ushort)closeTag.Length;
		public readonly ushort OpenTagInfoIdx = ParserState.GetOpenTagIdx(openTag);

		public readonly bool SameLine           = sameLine;
		public readonly bool SeekToEnd          = seekToEnd;
		public readonly bool AllowNesting       = allowNesting;
		public readonly bool RequireStartOfLine = requireStartOfLine;

		public readonly ushort ConsumeMaxLeadingNewlines  = consumeMaxLeadingNewlines;
		public readonly ushort ConsumeMaxTrailingNewlines = consumeMaxTrailingNewlines;
	}

	private static void ParseMarkupNode(ref ParserState state, ref MarkupTag tag)
	{
		if (state.HasUnmatchedTag(tag.CloseTag) || (tag.RequireStartOfLine && !state.MatchNewlineBehind(true)))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return;
		}

		var consumedNewlines          = 0;
		var consumeMaxLeadingNewlines = tag.ConsumeMaxLeadingNewlines;
		if (consumeMaxLeadingNewlines > 0)
		{
			while (consumeMaxLeadingNewlines > 0 && state.MatchAhead('\n'))
			{
				consumeMaxLeadingNewlines--;
				consumedNewlines++;
				state.Seek(1);
			}
		}

		state.Seek(tag.OpenTagLength);

		var end            = -1;
		var searchSpaceEnd = tag.SameLine ? state.IndexOfCached('\n') : -1;

		if (searchSpaceEnd == -1)
			searchSpaceEnd = state.Length;

		if (tag.AllowNesting)
		{
			var info     = state.RecursionInfo.Luts[tag.OpenTagInfoIdx];
			var lut      = info.AsSpan();
			var startIdx = state.RecursionInfo.Pointers[tag.OpenTagInfoIdx];
			var offset   = state.Offset;
			var pos      = state.Position + offset;
			var until    = searchSpaceEnd + offset;

			var closeTagLutIdx = lut.FindIndex(CloseFlag, ++startIdx, null, pos, until);
			var closeTagIdx    = closeTagLutIdx == -1 ? -1 : lut[closeTagLutIdx] & IndexBitmask;

			var openTagLutIdx = closeTagIdx - pos > tag.OpenTagLength
				? lut.FindIndex(OpenFlag, startIdx, closeTagLutIdx, pos)
				: -1;

			var openTagIdx = openTagLutIdx == -1 ? -1 : lut[openTagLutIdx] & IndexBitmask;

			if (openTagLutIdx != -1)
				state.RecursionInfo.Pointers[tag.OpenTagInfoIdx] = openTagLutIdx;
			else if (closeTagLutIdx != -1)
				state.RecursionInfo.Pointers[tag.OpenTagInfoIdx] = closeTagLutIdx;

			if (closeTagIdx != -1)
			{
				if (openTagIdx == -1)
				{
					end = closeTagIdx - offset;
				}
				else
				{
					var tagStack = 1;

					var i = openTagLutIdx;
					while (i < lut.Length)
					{
						var candidate    = lut[i];
						var candidateIdx = candidate & IndexBitmask;
						if (candidateIdx >= until)
							break;

						var candidateOpen = (candidate & OpenBitmask) is OpenFlag;
						tagStack += candidateOpen ? 1 : -1;

						if (tagStack == 0)
						{
							end = candidateIdx - offset;
							break;
						}

						if (!candidateOpen)
							closeTagIdx = candidateIdx - offset;

						i++;
					}

					if (tagStack > RecursionLimit)
					{
						state.UpdatePendingTextAndSeekTo(searchSpaceEnd, tag.OpenTagLength);
						return;
					}

					if (tagStack > 0)
						end = closeTagIdx;
				}
			}
		}
		else
		{
			var closeTagIdx = state.IndexOf(tag.CloseTag, searchSpaceEnd);
			if (closeTagIdx != -1)
				end = closeTagIdx;
		}

		if (end == -1)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(tag.OpenTagLength + consumedNewlines);
			if (!tag.SameLine) state.AddUnmatchedTag(tag.CloseTag);
			return;
		}

		tag.Accumulate(ref state, end);

		if (tag.SeekToEnd)
			state.SeekTo(end + tag.CloseTagLength);

		var consumeMaxTrailingNewlines = tag.ConsumeMaxTrailingNewlines;
		if (consumeMaxTrailingNewlines > 0)
		{
			while (consumeMaxTrailingNewlines > 0 && state.MatchAhead('\n'))
			{
				consumeMaxTrailingNewlines--;
				state.Seek(1);
			}
		}
	}

	#endregion MarkupNode Helpers
}
