using System.Buffers;
using JetBrains.Annotations;

namespace Iceshrimp.MfmSharp;

using Parser = Func<MfmParser.ParserState, MfmParser.ParserState>;
using Accumulator = Func<MfmParser.ParserState, int, MfmParser.ParserState>;

[PublicAPI]
public static class MfmParser
{
	private const int LengthLimit     = 100_000;
	private const int RecursionLimit  = 20;
	private const int LookupThreshold = 500;

	public static List<MfmNode> Parse(ReadOnlySpan<char> input) => Parse(input, false);

	public static List<MfmNode> Parse(ReadOnlySpan<char> input, bool simple)
	{
		input = input.Trim();
		if (input.Length == 0) return [];

		var processed = input.ToString().ReplaceLineEndings("\n");
		if (processed.Length == 0) return [];
		if (processed.Length > LengthLimit) return [new MfmTextNode(processed)];
		var state = new ParserState(processed, simple ? ParseMode.Simple : ParseMode.Full);
		while (!state.IsEos)
			state = simple ? ParseNodeSimple(state) : ParseNode(state);
		return state.GetResults();
	}

	internal enum ParseMode
	{
		Full,
		Inline,
		Simple
	}

	[PublicAPI]
	internal ref struct ParserState(ReadOnlySpan<char> input, ParseMode mode = ParseMode.Full, int depth = 0)
	{
		// Basic private state
		private readonly ReadOnlySpan<char> _stream      = input;
		private readonly List<MfmNode>      _results     = [];
		private          bool               _closed      = false;
		private          int                _position    = 0;
		private          Range?             _pendingText = null;
		private          bool               _skipLookup  = input.Length < LookupThreshold;

		// Cache for (possibly) expensive lookups
		private Dictionary<string, int>? _lookup = null;

		// Helper expression-bodied properties
		public int  Position    => _position;
		public int  Depth       => depth;
		public int  Length      => _stream.Length;
		public int  Remaining   => Length - _position;
		public int  LastIdx     => _stream.Length - 1;
		public char CurrentChar => _stream[_position];
		public char PrevChar    => _stream[_position - 1];
		public bool IsEos       => _position == Length;
		public bool IsLast      => _position == LastIdx;
		public bool IsStart     => _position == 0;

		public ParseMode Mode => mode;

		public SearchValues<char> BoundaryChars => mode switch
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

			if (_position > _stream.Length)
				_position = Length;
		}

		public void SeekTo(int dest)
		{
			if (dest < _position)
				throw new InvalidOperationException("Backtracking is not allowed");

			_position = Math.Min(dest, Length);
		}

		public void SeekToEnd() => _position = Length;

		// Recursion helper method
		public MfmInlineNode[] Recurse(int end)
		{
			if (Mode is ParseMode.Simple)
				throw new InvalidOperationException("Cannot recurse in simple mode");

			var stream = _stream[_position..end];
			if (stream.Length == 0) return [];
			if (depth == RecursionLimit || !stream.ContainsAny(BoundaryChars))
				return [new MfmTextNode(stream.ToString())];

			var state = new ParserState(stream, ParseMode.Inline, depth + 1);
			while (!state.IsEos)
				state = ParseNode(state);

			return state.GetResults().Cast<MfmInlineNode>().ToArray();
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
			if (idx == -1) UpdatePendingTextAndSeekToEnd();
			else UpdatePendingTextAndSeekTo(idx);
		}

		public void UpdatePendingTextAndSeekToEnd(int lookbehind = 0)
		{
			UpdatePendingText(Length, lookbehind);
			SeekToEnd();
		}

		private void MaterializePendingText()
		{
			if (_pendingText is not { } range) return;
			_results.Add(new MfmTextNode(_stream[range].ToString()));
			_pendingText = null;
		}

		// Result methods
		public void AddResult(MfmNode node)
		{
			MaterializePendingText();
			_results.Add(node);
		}

		public List<MfmNode> GetResults()
		{
			if (_closed) throw new InvalidOperationException("This ParserState struct has already been closed.");
			_closed = true;
			MaterializePendingText();
			return _results;
		}

		// Match, read, slice & helper methods
		public bool MatchAhead(char match)
			=> Remaining >= 1 && CurrentChar == match;

		public bool MatchBehind(char match)
			=> Position >= 1 && PrevChar == match;

		public bool MatchAhead(ReadOnlySpan<char> match)
			=> Remaining >= match.Length && _stream.Slice(Position, match.Length).SequenceEqual(match);

		public bool MatchBehind(ReadOnlySpan<char> match)
			=> Position >= match.Length && _stream.Slice(Position - match.Length, match.Length).SequenceEqual(match);

		public bool MatchAnyAhead(SearchValues<char> match)
			=> !IsEos && match.Contains(CurrentChar);

		public bool MatchAnyBehind(SearchValues<char> match)
			=> !IsStart && match.Contains(_stream[_position - 1]);

		public bool MatchWhitespaceAhead(bool matchEos)
			=> (matchEos && IsEos) || (!IsEos && MatchAnyAhead(WhitespaceChars));

		public bool MatchWhitespaceBehind(bool matchStart)
			=> (matchStart && IsStart) || (!IsStart && MatchAnyBehind(WhitespaceChars));

		public bool MatchNewlineBehind(bool matchStart)
			=> (matchStart && IsStart) || (!IsStart && MatchBehind('\n'));

		public ReadOnlySpan<char> ReadToEnd()             => _stream[_position..];
		public ReadOnlySpan<char> ReadTo(int end)         => _stream[_position..end];
		public char               ReadAt(int position)    => _stream[position];
		public ReadOnlySpan<char> Slice(Range range)      => _stream[range];
		public ReadOnlySpan<char> Slice(int end)          => _stream[_position..end];
		public ReadOnlySpan<char> SliceInclusive(int end) => _stream[_position..Math.Min(end, LastIdx)];

		private        int WithPosition(int offset)         => offset is -1 ? -1 : _position + offset;
		private static int WithIndex(int offset, int index) => offset is -1 ? -1 : index + offset;

		// Index lookup methods
		private int? Lookup(string key)
			=> (_lookup ??= []).TryGetValue(key, out var val) && (val < 0 || val >= _position) ? val : null;

		private int SetLookup(string key, int val) => (_lookup ??= [])[key] = val;

		public int IndexOfAny(SearchValues<char> sv, string name, bool skipLookup = false)
		{
			if (_skipLookup || skipLookup || Remaining < LookupThreshold)
				return WithPosition(_stream[_position..].IndexOfAny(sv));

			var key = $"IndexOfAny-{name}";
			return Lookup(key) ?? SetLookup(key, WithPosition(_stream[_position..].IndexOfAny(sv)));
		}

		public int IndexOfAny(SearchValues<char> sv, string name, int end)
		{
			if (_skipLookup || end - Position < LookupThreshold)
				return WithPosition(_stream[_position..end].IndexOfAny(sv));

			var key = $"IndexOfAny-{name}-{end}";
			return Lookup(key) ?? SetLookup(key, WithPosition(_stream[_position..end].IndexOfAny(sv)));
		}

		public int IndexOfAnyBoundaryChar() => Mode switch
		{
			ParseMode.Full   => IndexOfAny(BoundaryCharsFull, nameof(BoundaryCharsFull), skipLookup: true),
			ParseMode.Inline => IndexOfAny(BoundaryCharsInline, nameof(BoundaryCharsInline), skipLookup: true),
			ParseMode.Simple => IndexOfAny(BoundaryCharsSimple, nameof(BoundaryCharsSimple), skipLookup: true),
			_                => throw new ArgumentOutOfRangeException()
		};

		public int IndexOf(ReadOnlySpan<char> span, Range range)
		{
			if (_skipLookup || range.GetOffsetAndLength(Length).Length < LookupThreshold)
				return WithIndex(_stream[range].IndexOf(span), range.Start.Value);

			var key = $"IndexOf-{span}-{range.Start}-{range.End}";
			return Lookup(key) ?? SetLookup(key, WithIndex(_stream[range].IndexOf(span), range.Start.Value));
		}

		public int IndexOf(ReadOnlySpan<char> span, int end)
		{
			if (_skipLookup || end - Position < LookupThreshold)
				return WithPosition(_stream[_position..end].IndexOf(span));

			var key = $"IndexOf-{span}-{end}";
			return Lookup(key) ?? SetLookup(key, WithPosition(_stream[_position..end].IndexOf(span)));
		}

		public int IndexOf(ReadOnlySpan<char> span)
		{
			if (_skipLookup || Remaining < LookupThreshold)
				return WithPosition(_stream[_position..].IndexOf(span));

			var key = $"IndexOf-{span}-{LastIdx}";
			return Lookup(key) ?? SetLookup(key, WithPosition(_stream[_position..].IndexOf(span)));
		}

		public int IndexOf(char c, Range range)
		{
			if (_skipLookup || range.GetOffsetAndLength(Length).Length < LookupThreshold)
				return WithIndex(_stream[range].IndexOf(c), range.Start.Value);

			var key = $"IndexOf-{c}-{range.Start}-{range.End}";
			return Lookup(key) ?? SetLookup(key, WithIndex(_stream[range].IndexOf(c), range.Start.Value));
		}

		public int IndexOf(char c, int? end)
		{
			end ??= Length;

			if (_skipLookup || end.Value - Position < LookupThreshold)
				return WithPosition(_stream[_position..end.Value].IndexOf(c));

			var key = $"IndexOf-{c}-{end}";
			return Lookup(key) ?? SetLookup(key, WithPosition(_stream[_position..end.Value].IndexOf(c)));
		}

		public int IndexOf(char c)
		{
			if (_skipLookup || Remaining < LookupThreshold)
				WithPosition(_stream[_position..].IndexOf(c));

			var key = $"IndexOf-{c}-{LastIdx}";
			return Lookup(key) ?? SetLookup(key, WithPosition(_stream[_position..].IndexOf(c)));
		}

		public int LastIndexOf(char c, int? end)
		{
			end ??= Length;

			if (_skipLookup || end.Value - Position < LookupThreshold)
				WithPosition(_stream[_position..end.Value].LastIndexOf(c));

			var key = $"IndexOf-{c}-{end}";
			return Lookup(key) ?? SetLookup(key, WithPosition(_stream[_position..end.Value].LastIndexOf(c)));
		}

		public int? IndexOfOrNull(char c) => IndexOf(c) is var idx && idx < 0 ? null : idx;

		public int IndexOfExcept(char match, string except, int? end = null)
		{
			if (_skipLookup || Position - (end ?? LastIdx) < LookupThreshold)
				IndexOfExceptInner(match, except, end);

			var key = $"IndexOfExcept-{match}-{except}-{end ?? LastIdx}";
			return Lookup(key) ?? SetLookup(key, IndexOfExceptInner(match, except, end));
		}

		private int IndexOfExceptInner(char match, ReadOnlySpan<char> except, int? end = null)
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
	private static ParserState ParseNode(ParserState state)
	{
		if (state.IsEos) return state;
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

		state = parser(state);

		if (state.Position == position)
		{
			#if DEBUG
			throw new Exception("Infinite loop detected!");
			#else
			state.UpdatePendingTextAndSeekToBoundary();
			#endif
		}

		return state;
	}

	private static ParserState ParseNodeSimple(ParserState state)
	{
		if (state.IsEos) return state;
		var position = state.Position;

		var parser = state.CurrentChar switch
		{
			':'                                  => ParseEmojiCode,
			'<' when state.MatchAhead("<plain>") => ParsePlainTag,
			_                                    => ParseText
		};

		state = parser(state);

		if (state.Position == position)
		{
			#if DEBUG
			throw new Exception("Infinite loop detected!");
			#else
			state.UpdatePendingTextAndSeekToBoundary();
			#endif
		}

		return state;
	}

	private static Parser ParseAsterisk(ParserState state)
		=> state.MatchAhead("**") ? ParseBoldAsterisk : ParseItalicAsterisk;

	private static Parser ParseUnderscore(ParserState state)
		=> state.MatchAhead("__") ? ParseBoldUnderscore : ParseItalicUnderscore;

	private static Parser ParseTilde(ParserState state)
		=> state.MatchAhead("~~") ? ParseStrikeTilde : ParseText;

	private static ParserState SkipChar(ParserState state)
	{
		state.Seek(1);
		return state;
	}

	private static ParserState ParseText(ParserState state)
	{
		// First, we find out how much text we can parse
		var endIdx = state.IndexOfAnyBoundaryChar();

		// No more boundary chars - we can consume the rest of the stream
		if (endIdx == -1)
		{
			state.UpdatePendingTextAndSeekToEnd();
			return state;
		}

		if (state.Position == endIdx) endIdx++;
		state.UpdatePendingTextAndSeekTo(endIdx);
		return state;
	}

	private static Parser ParseInlineTag(ParserState state)
	{
		if (state.MatchAhead("<b>"))
			return ParseBoldTag;
		if (state.MatchAhead("<i>"))
			return ParseItalicTag;
		if (state.MatchAhead("<s>"))
			return ParseStrikeTag;
		if (state.MatchAhead("<plain>"))
			return ParsePlainTag;
		if (state.MatchAhead("<small>"))
			return ParseSmallTag;
		if (state.MatchAhead("<https://") || state.MatchAhead("<http://"))
			return ParseUrlBrackets;

		return s =>
		{
			s.UpdatePendingTextAndSeekToBoundary();
			return s;
		};
	}

	private static Parser ParseTag(ParserState state)
		=> state.MatchAhead("<center>") ? ParseCenterTag : ParseInlineTag(state);

	private static ParserState ParseHashtag(ParserState state)
	{
		const int delimLength = 1;
		if (!state.MatchWhitespaceBehind(true) || state.MatchWhitespaceAhead(true))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return state;
		}

		state.Seek(delimLength);
		var endIdx = state.IndexOfAny(HashtagBoundaryChars, nameof(HashtagBoundaryChars));
		if (endIdx == state.Position || state.Remaining < 1)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return state;
		}

		if (endIdx == -1)
		{
			state.AddResult(new MfmHashtagNode(state.ReadToEnd().ToString()));
			state.SeekToEnd();
			return state;
		}

		state.AddResult(new MfmHashtagNode(state.ReadTo(endIdx).ToString()));
		state.SeekTo(endIdx);

		return state;
	}

	private static ParserState ParseEmojiCode(ParserState state)
	{
		const int  delimLength = 1;
		const char delim       = ':';

		state.Seek(delimLength);

		var endIdx = state.IndexOf(delim);
		if (endIdx == -1 || endIdx == state.Position || state.Slice(endIdx).ContainsAnyExcept(EmojiCodeAllowedChars))
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return state;
		}

		state.AddResult(new MfmEmojiCodeNode(state.ReadTo(endIdx).ToString()));
		state.SeekTo(endIdx);
		state.Seek(delimLength);

		return state;
	}

	private static ParserState ParseInlineCode(ParserState state)
	{
		const int  delimLength = 1;
		const char delim       = '`';

		state.Seek(delimLength);

		var endIdx = state.IndexOf(delim, state.IndexOfOrNull('\n'));
		if (endIdx == -1 || endIdx == state.Position)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return state;
		}

		state.AddResult(new MfmInlineCodeNode(state.ReadTo(endIdx).ToString()));
		state.SeekTo(endIdx);
		state.Seek(delimLength);

		return state;
	}

	private static ParserState ParseUrl(ParserState state)
	{
		if (!state.MatchAhead("https://") && !state.MatchAhead("http://"))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return state;
		}

		var end = state.IndexOfAny(WhitespaceChars, nameof(WhitespaceChars));

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
			}
		}

		if (Uri.TryCreate(state.ReadTo(end).ToString(), UriKind.Absolute, out var uri))
		{
			state.AddResult(new MfmUrlNode(uri.ToString(), false));
			state.SeekTo(end);
		}
		else
		{
			state.UpdatePendingTextAndSeekTo(end);
		}

		return state;
	}

	private static ParserState ParseUrlBrackets(ParserState state)
	{
		state.Seek(1);

		var end = state.IndexOf('>');
		if (end == -1 || state.IndexOf('\n', end) != -1)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(1);
			return state;
		}

		if (Uri.TryCreate(state.ReadTo(end).ToString(), UriKind.Absolute, out var uri))
		{
			state.AddResult(new MfmUrlNode(uri.ToString(), true));
			state.SeekTo(end);
			state.Seek(1);
		}
		else
		{
			state.UpdatePendingTextAndSeekTo(end + 1, 1);
		}

		return state;
	}

	private static ParserState ParseLink(ParserState state)
	{
		var silent = state.MatchAhead('?');
		if (silent && !state.MatchAhead("?["))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return state;
		}

		var delimLength = silent ? 2 : 1;
		state.Seek(delimLength);

		var textEnd = state.IndexOf(']');
		if (
			textEnd == -1
			|| textEnd == state.Position
			|| state.IndexOf('\n', textEnd) != -1
			|| textEnd > state.LastIdx - "(http://)".Length
			|| state.ReadAt(textEnd + 1) != '('
		)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return state;
		}

		var linkStart = textEnd + 2;
		var linkEnd   = state.IndexOfAny(WhitespaceChars, nameof(WhitespaceChars));

		if (linkEnd == -1)
			linkEnd = state.Length;

		var closeBracketIdx = state.IndexOf(')', linkStart..linkEnd);
		if (closeBracketIdx == -1)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
			return state;
		}

		var openBracketIdx = state.IndexOf('(', closeBracketIdx);
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
				if (bracketStack == 0)
					linkEnd = i + openBracketIdx;
			}

			if (bracketStack > 0)
			{
				state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
				return state;
			}
		}

		if (Uri.TryCreate(state.Slice(linkStart..linkEnd).ToString(), UriKind.Absolute, out var uri))
		{
			state.AddResult(new MfmLinkNode(uri.ToString(), state.ReadTo(textEnd).ToString(), silent));
			state.SeekTo(linkEnd);
			state.Seek(1);
		}
		else
		{
			state.UpdatePendingTextAndSeekTo(linkEnd + 1, delimLength);
		}

		return state;
	}

	private static ParserState ParseMention(ParserState state)
	{
		if (!state.MatchWhitespaceBehind(true) && !ParenthesisChars.Contains(state.PrevChar))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return state;
		}

		state.Seek(1);

		var end = state.IndexOfAny(WhitespaceChars, nameof(WhitespaceChars));
		if (end == -1)
			end = state.Length;

		if (end - state.Position <= 1)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(1);
			return state;
		}

		var hostPartIdx = state.IndexOf('@', end);
		var localSlice  = state.Slice(hostPartIdx == -1 ? end : hostPartIdx);
		var nextCharIdx = state.Position + localSlice.Length + 1;

		if (
			hostPartIdx == -1
			&& localSlice.Length > 1
			&& (".-".Contains(localSlice[^1])
			    || (localSlice[^1] == ':'
			        && (state.Remaining == localSlice.Length
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
			return state;
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
				return state;
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
				return state;
			}
		}

		var user = localSlice.ToString();
		var host = hostSlice.Length > 0 ? hostSlice.ToString() : null;
		state.AddResult(new MfmMentionNode(user, host));
		state.SeekTo(end);

		return state;
	}

	private static ParserState ParseQuote(ParserState state)
	{
		const int quoteRecursionLimit = 4;

		if (state.Remaining < 2 || !state.MatchNewlineBehind(true))
		{
			state.UpdatePendingTextAndSeekToBoundary();
			return state;
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
			return state;
		}

		if (state.CurrentChar == ' ')
		{
			state.Seek(1);
			lookbehind++;
		}

		if (state.CurrentChar == '\n')
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(lookbehind);
			return state;
		}

		lookbehind = 0;

		var end = state.IndexOf('\n');
		if (end == -1)
			end = state.Length;

		// @formatter:off
		List<MfmInlineNode> results = [];
		Stack<(int depth, List<MfmInlineNode> results)> stack = [];
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
			end = state.IndexOf('\n');

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
						results = head.results;
						head.results.Add(nestQuote);
					}

					if (currentDepth > depth)
					{
						var nestQuote = new MfmQuoteNode(results.ToArray(), nested: true);
						results = [nestQuote];

						while (--currentDepth > depth)
						{
							nestQuote = new MfmQuoteNode([nestQuote], nested: true);
							results   = [nestQuote];
						}
					}

					continue;
				}

				// currentDepth is < depth
				stack.Push((currentDepth, results));
				results      = [];
				currentDepth = depth;
			}
		}

		while (stack.TryPop(out var head))
		{
			var nestQuote = new MfmQuoteNode(results.ToArray(), nested: true);
			while (--currentDepth > head.depth)
				nestQuote = new MfmQuoteNode([nestQuote], nested: true);
			results = head.results;
			head.results.Add(nestQuote);
		}

		while (currentDepth-- > 0)
			results = [new MfmQuoteNode(results.ToArray(), nested: true)];

		state.AddResult(new MfmQuoteNode(results.ToArray()));

		if (lookbehind > 0)
			state.UpdatePendingTextBehindAndSeekToBoundary(lookbehind);

		return state;
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

	private static readonly Accumulator ItalicAccumulator = (state, endIdx) =>
	{
		var type = state.PrevChar switch
		{
			'*' => MfmItalicNode.DelimiterType.Asterisk,
			'_' => MfmItalicNode.DelimiterType.Underscore,
			_   => MfmItalicNode.DelimiterType.HtmlTag
		};

		state.AddResult(new MfmItalicNode(state.Recurse(endIdx), type));
		return state;
	};

	private static readonly Accumulator BoldAccumulator = (state, endIdx) =>
	{
		var type = state.PrevChar switch
		{
			'*' => MfmBoldNode.DelimiterType.Asterisk,
			'_' => MfmBoldNode.DelimiterType.Underscore,
			_   => MfmBoldNode.DelimiterType.HtmlTag
		};

		state.AddResult(new MfmBoldNode(state.Recurse(endIdx), type));
		return state;
	};

	private static readonly Accumulator StrikeAccumulator = (state, endIdx) =>
	{
		var type = state.PrevChar switch
		{
			'~' => MfmStrikeNode.DelimiterType.Tilde,
			_   => MfmStrikeNode.DelimiterType.HtmlTag
		};

		state.AddResult(new MfmStrikeNode(state.Recurse(endIdx), type));
		return state;
	};

	private static readonly Accumulator InlineMathAccumulator = (state, endIdx) =>
	{
		state.AddResult(new MfmInlineMathNode(state.ReadTo(endIdx).ToString()));
		return state;
	};

	private static readonly Accumulator MathBlockAccumulator = (state, endIdx) =>
	{
		state.AddResult(new MfmMathBlockNode(state.ReadTo(endIdx).ToString()));
		return state;
	};

	private static readonly Accumulator CodeBlockAccumulator = (state, endIdx) =>
	{
		string? lang = null;
		if (state.CurrentChar != '\n')
		{
			var newlineIdx = state.IndexOf('\n');
			lang = state.ReadTo(newlineIdx).ToString();
			state.SeekTo(newlineIdx);
		}

		state.Seek(1);
		state.AddResult(new MfmCodeBlockNode(state.ReadTo(endIdx).ToString(), lang));
		return state;
	};

	private static readonly Accumulator CenterAccumulator = (state, endIdx) =>
	{
		if (state.CurrentChar == '\n')
			state.Seek(1);
		if (state.ReadAt(endIdx - 1) == '\n')
			endIdx--;

		state.AddResult(new MfmCenterNode(state.Recurse(endIdx)));
		return state;
	};

	private static readonly Accumulator SmallAccumulator = (state, endIdx) =>
	{
		state.AddResult(new MfmSmallNode(state.Recurse(endIdx)));
		return state;
	};

	private static readonly Accumulator PlainAccumulator = (state, endIdx) =>
	{
		state.AddResult(new MfmPlainNode(state.ReadTo(endIdx).ToString()));
		return state;
	};

	private static readonly Accumulator FnAccumulator = (state, endIdx) =>
	{
		var descriptorEndIdx = state.IndexOf(' ', endIdx);
		if (descriptorEndIdx == -1 || endIdx - descriptorEndIdx <= 1)
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(2);
			return state;
		}

		var nameAndArgs = state.Slice(descriptorEndIdx);
		if (nameAndArgs.Length == 0 || nameAndArgs.ContainsAnyExcept(FnDescriptorAllowedChars))
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(2);
			return state;
		}

		var argsIdx = nameAndArgs.IndexOf('.');
		var name    = argsIdx != -1 ? nameAndArgs[..argsIdx] : nameAndArgs;

		if (name.Length == 0 || name.ContainsAnyExcept(FnKeyAllowedChars))
		{
			state.UpdatePendingTextBehindAndSeekToBoundary(2);
			return state;
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
				return state;
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
						return state;
					}

					args.Add(arg.ToString(), null);
					continue;
				}

				var key   = arg[..argSepIdx];
				var value = arg[++argSepIdx..];
				if (key.ContainsAnyExcept(FnKeyAllowedChars) || value.ContainsAnyExcept(FnArgValueAllowedChars))
				{
					state.UpdatePendingTextBehindAndSeekToBoundary(2);
					return state;
				}

				args.Add(key.ToString(), value.ToString());
			}
		}

		state.SeekTo(descriptorEndIdx);
		state.Seek(1);
		state.AddResult(new MfmFnNode(name.ToString(), args, state.Recurse(endIdx)));
		state.SeekTo(endIdx);
		state.Seek(1);
		return state;
	};

	private static readonly Parser ParseItalicAsterisk   = GetMarkupNode('*', "**", ItalicAccumulator);
	private static readonly Parser ParseItalicUnderscore = GetMarkupNode('_', "__", ItalicAccumulator);
	private static readonly Parser ParseBoldAsterisk     = GetMarkupNode("**", BoldAccumulator);
	private static readonly Parser ParseBoldUnderscore   = GetMarkupNode("__", BoldAccumulator);
	private static readonly Parser ParseStrikeTilde      = GetMarkupNode("~~", StrikeAccumulator);

	private static readonly Parser ParseItalicTag = GetMarkupTagNode("<i>", "</i>", ItalicAccumulator);
	private static readonly Parser ParseBoldTag   = GetMarkupTagNode("<b>", "</b>", BoldAccumulator);
	private static readonly Parser ParseStrikeTag = GetMarkupTagNode("<s>", "</s>", StrikeAccumulator);
	private static readonly Parser ParseSmallTag  = GetMarkupTagNode("<small>", "</small>", SmallAccumulator);

	private static readonly Parser ParseFnTag =
		GetMarkupTagNode("$[", "]", FnAccumulator, seekToEnd: false);

	private static readonly Parser ParsePlainTag =
		GetMarkupTagNode("<plain>", "</plain>", PlainAccumulator, allowNesting: false);

	private static readonly Parser ParseCenterTag =
		GetMarkupTagNode("<center>", "</center>", CenterAccumulator, requireStartOfLine: true);

	private static readonly Parser ParseInlineMath =
		GetMarkupTagNode("\\(", "\\)", InlineMathAccumulator, sameLine: true, allowNesting: false);

	private static readonly Parser ParseMathBlock =
		GetMarkupTagNode("\\[", "\\]", MathBlockAccumulator, allowNesting: false, requireStartOfLine: true);

	private static readonly Parser ParseCodeBlock =
		GetMarkupTagNode("```", "\n```", CodeBlockAccumulator, allowNesting: false, consumeMaxLeadingNewlines: 2);

	#region MarkupNode Closures

	private static Parser GetMarkupNode(
		char delim, string except, Accumulator accumulator, bool sameLine = true
	)
	{
		const int delimLength = 1;
		return state =>
		{
			state.Seek(delimLength);

			var endIdx = state.IndexOfExcept(delim, except, sameLine ? state.IndexOfOrNull('\n') : null);

			if (
				endIdx == -1
				|| (state.Position > delimLength
				    && !AsciiSymbolsAndWhitespaceChars.Contains(state.ReadAt(state.Position - delimLength - 1)))
				|| (endIdx < state.Length - delimLength
				    && !AsciiSymbolsAndWhitespaceChars.Contains(state.ReadAt(endIdx + delimLength)))
			)
			{
				state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
				return state;
			}

			state = accumulator(state, endIdx);
			state.SeekTo(endIdx + delimLength);
			return state;
		};
	}

	private static Parser GetMarkupNode(
		string delim, Accumulator accumulator, bool sameLine = true
	)
	{
		var delimLength = delim.Length;
		return state =>
		{
			state.Seek(delimLength);

			var endIdx = sameLine
				? state.IndexOf(delim, state.IndexOfOrNull('\n') ?? state.LastIdx)
				: state.IndexOf(delim);

			if (
				endIdx == -1
				|| (state.Position > delimLength
				    && !AsciiSymbolsAndWhitespaceChars.Contains(state.ReadAt(state.Position - delimLength - 1)))
				|| (endIdx < state.Length - delimLength
				    && !AsciiSymbolsAndWhitespaceChars.Contains(state.ReadAt(endIdx + delimLength)))
			)
			{
				state.UpdatePendingTextBehindAndSeekToBoundary(delimLength);
				return state;
			}

			state = accumulator(state, endIdx);
			state.SeekTo(endIdx + delimLength);
			return state;
		};
	}

	private static Parser GetMarkupTagNode(
		string openTag, string closeTag, Accumulator accumulator, bool sameLine = false, bool seekToEnd = true,
		bool allowNesting = true, bool requireStartOfLine = false, int consumeMaxLeadingNewlines = 0,
		int consumeMaxTrailingNewlines = 0
	)
	{
		var openTagLength  = openTag.Length;
		var closeTagLength = closeTag.Length;
		var tags           = SearchValues.Create([openTag, closeTag], StringComparison.Ordinal);

		return state =>
		{
			if (requireStartOfLine && !state.MatchNewlineBehind(true))
			{
				state.UpdatePendingTextAndSeekToBoundary();
				return state;
			}

			var consumedNewlines = 0;
			if (consumeMaxLeadingNewlines > 0)
			{
				while (consumeMaxLeadingNewlines > 0 && state.MatchAhead('\n'))
				{
					consumeMaxLeadingNewlines--;
					consumedNewlines++;
					state.Seek(1);
				}
			}

			state.Seek(openTagLength);

			var end            = -1;
			var searchSpaceEnd = sameLine ? state.IndexOf('\n') : -1;

			if (searchSpaceEnd == -1)
				searchSpaceEnd = state.Length;

			if (allowNesting)
			{
				var closeTagIdx = state.IndexOf(closeTag, searchSpaceEnd);
				if (closeTagIdx != -1)
				{
					var openTagIdx = state.IndexOf(openTag, closeTagIdx);
					if (openTagIdx == -1)
					{
						end = closeTagIdx;
					}
					else
					{
						var tagStack = 1;
						var slice    = state.Slice(++openTagIdx..searchSpaceEnd);

						var i = -1;
						while (tagStack >= 0 && ++i < slice.Length)
						{
							var next = slice[i..].IndexOfAny(tags);
							if (next is -1)
							{
								end = closeTagIdx;
								break;
							}

							i += next;

							if (openTagLength <= closeTagLength)
								tagStack += slice[i..(i + openTagLength)].SequenceEqual(openTag) ? 1 : -1;
							else
								tagStack += slice[i..(i + closeTagLength)].SequenceEqual(closeTag) ? -1 : 1;

							if (tagStack == -1)
								end = i + openTagIdx;
						}
					}
				}
			}
			else
			{
				var closeTagIdx            = state.IndexOf(closeTag, searchSpaceEnd);
				if (closeTagIdx != -1) end = closeTagIdx;
			}

			if (end == -1)
			{
				state.UpdatePendingTextBehindAndSeekToBoundary(openTagLength + consumedNewlines);
				return state;
			}

			state = accumulator(state, end);

			if (seekToEnd)
				state.SeekTo(end + closeTagLength);

			if (consumeMaxTrailingNewlines > 0)
			{
				while (consumeMaxTrailingNewlines > 0 && state.MatchAhead('\n'))
				{
					consumeMaxTrailingNewlines--;
					state.Seek(1);
				}
			}

			return state;
		};
	}

	#endregion MarkupNode Closures
}
