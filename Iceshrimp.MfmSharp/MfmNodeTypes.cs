using System.Text;

namespace Iceshrimp.MfmSharp;

public abstract class MfmNode(MfmNode[] children)
{
	public MfmNode[] Children => children;

	public static implicit operator MfmNode(string text) => new MfmTextNode(text);
}

public abstract class MfmInlineNode(MfmInlineNode[] children) : MfmNode(children.Cast<MfmNode>().ToArray())
{
	public static implicit operator MfmInlineNode(string text) => new MfmTextNode(text);
}

public abstract class MfmBlockNode(MfmInlineNode[] children) : MfmNode(children.Cast<MfmNode>().ToArray());

public class MfmTextNode(string text) : MfmInlineNode([]), IEquatable<MfmTextNode>
{
	public string Text => text;

	public override string ToString()                 => text;
	public override int    GetHashCode()              => text.GetHashCode();
	public          bool   Equals(MfmTextNode? other) => text == other?.Text;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmTextNode)obj);
	}

	public static bool operator ==(MfmTextNode? left, MfmTextNode? right) => Equals(left, right);
	public static bool operator !=(MfmTextNode? left, MfmTextNode? right) => !Equals(left, right);

	public static implicit operator MfmTextNode(string text) => new(text);
}

public class MfmItalicNode(
	MfmInlineNode[] children,
	MfmItalicNode.DelimiterType type
) : MfmInlineNode(children), IEquatable<MfmItalicNode>
{
	public enum DelimiterType
	{
		Asterisk,
		Underscore,
		HtmlTag
	}

	public DelimiterType Type => type;

	public override string ToString() => type switch
	{
		DelimiterType.Asterisk   => $"*{Children.Serialize(trim: false)}*",
		DelimiterType.Underscore => $"_{Children.Serialize(trim: false)}_",
		DelimiterType.HtmlTag    => $"<i>{Children.Serialize(trim: false)}</i>",
		_                        => throw new ArgumentOutOfRangeException(nameof(type), type, null)
	};

	public override int  GetHashCode()                => HashCode.Combine(Children, type);
	public          bool Equals(MfmItalicNode? other) => type == other?.Type && Children.SequenceEqual(other.Children);

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmItalicNode)obj);
	}

	public static bool operator ==(MfmItalicNode? left, MfmItalicNode? right) => Equals(left, right);
	public static bool operator !=(MfmItalicNode? left, MfmItalicNode? right) => !Equals(left, right);
}

public class MfmBoldNode(
	MfmInlineNode[] children,
	MfmBoldNode.DelimiterType type
) : MfmInlineNode(children), IEquatable<MfmBoldNode>
{
	public enum DelimiterType
	{
		Asterisk,
		Underscore,
		HtmlTag
	}

	public DelimiterType Type => type;

	public override string ToString() => type switch
	{
		DelimiterType.Asterisk   => $"**{Children.Serialize(trim: false)}**",
		DelimiterType.Underscore => $"__{Children.Serialize(trim: false)}__",
		DelimiterType.HtmlTag    => $"<b>{Children.Serialize(trim: false)}</b>",
		_                        => throw new ArgumentOutOfRangeException(nameof(type), type, null)
	};

	public override int  GetHashCode()              => HashCode.Combine(Children, type);
	public          bool Equals(MfmBoldNode? other) => type == other?.Type && Children.SequenceEqual(other.Children);

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmBoldNode)obj);
	}

	public static bool operator ==(MfmBoldNode? left, MfmBoldNode? right) => Equals(left, right);
	public static bool operator !=(MfmBoldNode? left, MfmBoldNode? right) => !Equals(left, right);
}

public class MfmStrikeNode(
	MfmInlineNode[] children,
	MfmStrikeNode.DelimiterType type
) : MfmInlineNode(children), IEquatable<MfmStrikeNode>
{
	public enum DelimiterType
	{
		Tilde,
		HtmlTag
	}

	public DelimiterType Type => type;

	public override string ToString() => type switch
	{
		DelimiterType.Tilde   => $"~~{Children.Serialize(trim: false)}~~",
		DelimiterType.HtmlTag => $"<s>{Children.Serialize(trim: false)}</s>",
		_                     => throw new ArgumentOutOfRangeException(nameof(type), type, null)
	};

	public override int  GetHashCode()                => HashCode.Combine(Children, type);
	public          bool Equals(MfmStrikeNode? other) => type == other?.Type && Children.SequenceEqual(other.Children);

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmStrikeNode)obj);
	}

	public static bool operator ==(MfmStrikeNode? left, MfmStrikeNode? right) => Equals(left, right);
	public static bool operator !=(MfmStrikeNode? left, MfmStrikeNode? right) => !Equals(left, right);
}

public class MfmInlineCodeNode(string code) : MfmInlineNode([]), IEquatable<MfmInlineCodeNode>
{
	public string Code => code;

	public override string ToString() => $"`{code}`";

	public override int  GetHashCode()                    => code.GetHashCode();
	public          bool Equals(MfmInlineCodeNode? other) => code == other?.Code;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmInlineCodeNode)obj);
	}

	public static bool operator ==(MfmInlineCodeNode? left, MfmInlineCodeNode? right) => Equals(left, right);
	public static bool operator !=(MfmInlineCodeNode? left, MfmInlineCodeNode? right) => !Equals(left, right);
}

public class MfmPlainNode(string text) : MfmInlineNode([new MfmTextNode(text)]), IEquatable<MfmPlainNode>
{
	private string Text => text;

	public override string ToString() => $"<plain>{text}</plain>";

	public override int  GetHashCode()               => text.GetHashCode();
	public          bool Equals(MfmPlainNode? other) => text == other?.Text;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmPlainNode)obj);
	}

	public static bool operator ==(MfmPlainNode? left, MfmPlainNode? right) => Equals(left, right);
	public static bool operator !=(MfmPlainNode? left, MfmPlainNode? right) => !Equals(left, right);
}

public class MfmSmallNode(MfmInlineNode[] children) : MfmInlineNode(children), IEquatable<MfmSmallNode>
{
	public override string ToString() => $"<small>{Children.Serialize(trim: false)}</small>";

	public override int  GetHashCode()               => Children.GetHashCode();
	public          bool Equals(MfmSmallNode? other) => other is not null && Children.SequenceEqual(other.Children);

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmSmallNode)obj);
	}

	public static bool operator ==(MfmSmallNode? left, MfmSmallNode? right) => Equals(left, right);
	public static bool operator !=(MfmSmallNode? left, MfmSmallNode? right) => !Equals(left, right);
}

public class MfmEmojiCodeNode(string name) : MfmInlineNode([]), IEquatable<MfmEmojiCodeNode>
{
	public string Name => name;

	public override string ToString() => $":{name}:";

	public override int  GetHashCode()                   => name.GetHashCode();
	public          bool Equals(MfmEmojiCodeNode? other) => name == other?.Name;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmEmojiCodeNode)obj);
	}

	public static bool operator ==(MfmEmojiCodeNode? left, MfmEmojiCodeNode? right) => Equals(left, right);
	public static bool operator !=(MfmEmojiCodeNode? left, MfmEmojiCodeNode? right) => !Equals(left, right);
}

public class MfmHashtagNode(string hashtag) : MfmInlineNode([]), IEquatable<MfmHashtagNode>
{
	public string Hashtag => hashtag;

	public override string ToString() => $"#{hashtag}";

	public override int  GetHashCode()                 => hashtag.GetHashCode();
	public          bool Equals(MfmHashtagNode? other) => hashtag == other?.Hashtag;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmHashtagNode)obj);
	}

	public static bool operator ==(MfmHashtagNode? left, MfmHashtagNode? right) => Equals(left, right);
	public static bool operator !=(MfmHashtagNode? left, MfmHashtagNode? right) => !Equals(left, right);
}

public class MfmMentionNode(string user, string? host) : MfmInlineNode([]), IEquatable<MfmMentionNode>
{
	public string  User => user;
	public string? Host => host;
	public string  Acct => host is null ? user : $"{user}@{host}";

	public override string ToString() => $"@{Acct}";

	public override int  GetHashCode()                 => HashCode.Combine(user, host);
	public          bool Equals(MfmMentionNode? other) => user == other?.User && Acct == other.Acct;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmMentionNode)obj);
	}

	public static bool operator ==(MfmMentionNode? left, MfmMentionNode? right) => Equals(left, right);
	public static bool operator !=(MfmMentionNode? left, MfmMentionNode? right) => !Equals(left, right);
}

public class MfmUrlNode(string url, bool brackets) : MfmInlineNode([]), IEquatable<MfmUrlNode>
{
	public string Url      => url;
	public bool   Brackets => brackets;

	public override string ToString() => brackets ? $"<{url}>" : url;

	public override int  GetHashCode()             => HashCode.Combine(url, brackets);
	public          bool Equals(MfmUrlNode? other) => url == other?.Url && brackets == other.Brackets;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmUrlNode)obj);
	}

	public static bool operator ==(MfmUrlNode? left, MfmUrlNode? right) => Equals(left, right);
	public static bool operator !=(MfmUrlNode? left, MfmUrlNode? right) => !Equals(left, right);
}

public class MfmLinkNode(string url, string text, bool silent) : MfmInlineNode([]), IEquatable<MfmLinkNode>
{
	public string Url    => url;
	public string Text   => text;
	public bool   Silent => silent;

	public override string ToString() => (silent ? "?" : "") + $"[{text}]({url})";

	public override int GetHashCode() => HashCode.Combine(url, text, silent);
	public bool Equals(MfmLinkNode? other) => url == other?.Url && text == other.Text && silent == other.Silent;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmLinkNode)obj);
	}

	public static bool operator ==(MfmLinkNode? left, MfmLinkNode? right) => Equals(left, right);
	public static bool operator !=(MfmLinkNode? left, MfmLinkNode? right) => !Equals(left, right);
}

public class MfmInlineMathNode(string formula) : MfmInlineNode([]), IEquatable<MfmInlineMathNode>
{
	public string Formula => formula;

	public override string ToString() => $@"\({formula}\)";

	public override int GetHashCode() => formula.GetHashCode();

	public bool Equals(MfmInlineMathNode? other) => formula == other?.Formula;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmInlineMathNode)obj);
	}

	public static bool operator ==(MfmInlineMathNode? left, MfmInlineMathNode? right) => Equals(left, right);
	public static bool operator !=(MfmInlineMathNode? left, MfmInlineMathNode? right) => !Equals(left, right);
}

public class MfmFnNode(
	string name,
	Dictionary<string, string?>? args,
	MfmInlineNode[] children
) : MfmInlineNode(children), IEquatable<MfmFnNode>
{
	public string                       Name => name;
	public Dictionary<string, string?>? Args => args;

	private string SerializedArgs => args is { Count: > 0 }
		? $".{string.Join(',', args.Select(p => p.Value != null ? $"{p.Key}={p.Value}" : $"{p.Key}"))}"
		: "";

	public override string ToString() => $"$[{name}{SerializedArgs} {Children.Serialize(trim: false)}]";

	public override int GetHashCode() => HashCode.Combine(name, args, Children);

	public bool Equals(MfmFnNode? other)
	{
		if (other == null) return false;
		if (name != other.Name) return false;
		if ((args == null) != (other.Args == null)) return false;
		if (args == null || other.Args == null) return true;
		if (args.Count != other.Args.Count) return false;
		// ReSharper disable once UsageOfDefaultStructEquality
		return !args.Except(other.Args).Any();
	}

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmFnNode)obj);
	}

	public static bool operator ==(MfmFnNode? left, MfmFnNode? right) => Equals(left, right);
	public static bool operator !=(MfmFnNode? left, MfmFnNode? right) => !Equals(left, right);
}

public class MfmQuoteNode(MfmInlineNode[] children, bool nested = false)
	: MfmInlineNode(children), IEquatable<MfmQuoteNode>
{
	public override string ToString()
	{
		var res = string.Join('\n', Children.Serialize(trim: true)
		                                    .Split('\n')
		                                    .Select(p => p.StartsWith('>') ? $">{p}" : $"> {p}"));

		return nested ? '\n' + res + '\n' : res;
	}

	public override int  GetHashCode()               => Children.GetHashCode();
	public          bool Equals(MfmQuoteNode? other) => other is not null && Children.SequenceEqual(other.Children);

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmQuoteNode)obj);
	}

	public static bool operator ==(MfmQuoteNode? left, MfmQuoteNode? right) => Equals(left, right);
	public static bool operator !=(MfmQuoteNode? left, MfmQuoteNode? right) => !Equals(left, right);
}

public class MfmCodeBlockNode(string code, string? lang) : MfmBlockNode([]), IEquatable<MfmCodeBlockNode>
{
	public string  Code => code;
	public string? Lang => lang;

	public override string ToString() => $"\n```{lang ?? ""}\n{code}\n```\n";

	public override int  GetHashCode()                   => HashCode.Combine(code, lang);
	public          bool Equals(MfmCodeBlockNode? other) => code == other?.Code && lang == other.Lang;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmCodeBlockNode)obj);
	}

	public static bool operator ==(MfmCodeBlockNode? left, MfmCodeBlockNode? right) => Equals(left, right);
	public static bool operator !=(MfmCodeBlockNode? left, MfmCodeBlockNode? right) => !Equals(left, right);
}

public class MfmMathBlockNode(string formula) : MfmBlockNode([]), IEquatable<MfmMathBlockNode>
{
	public string Formula => formula;

	public override string ToString() => $@"\[{formula}\]";

	public override int  GetHashCode()                   => HashCode.Combine(formula);
	public          bool Equals(MfmMathBlockNode? other) => formula == other?.Formula;

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmMathBlockNode)obj);
	}

	public static bool operator ==(MfmMathBlockNode? left, MfmMathBlockNode? right) => Equals(left, right);
	public static bool operator !=(MfmMathBlockNode? left, MfmMathBlockNode? right) => !Equals(left, right);
}

public class MfmCenterNode(MfmInlineNode[] children) : MfmBlockNode(children), IEquatable<MfmCenterNode>
{
	public override string ToString() => $"<center>\n{Children.Serialize(trim: false)}\n</center>";

	public override int  GetHashCode()                => Children.GetHashCode();
	public          bool Equals(MfmCenterNode? other) => other is not null && Children.SequenceEqual(other.Children);

	public override bool Equals(object? obj)
	{
		if (obj is null) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != GetType()) return false;
		return Equals((MfmCenterNode)obj);
	}

	public static bool operator ==(MfmCenterNode? left, MfmCenterNode? right) => Equals(left, right);
	public static bool operator !=(MfmCenterNode? left, MfmCenterNode? right) => !Equals(left, right);
}

public static class MfmExtensions
{
	public static string Serialize(this IEnumerable<MfmNode> nodes, bool trim = true)
	{
		var sb = new StringBuilder();
		foreach (var node in nodes) sb.Append(node);
		return trim ? sb.ToString().Trim() : sb.ToString();
	}
}
