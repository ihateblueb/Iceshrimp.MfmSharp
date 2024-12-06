using System.Text;
using JetBrains.Annotations;

namespace Iceshrimp.MfmSharp;

[PublicAPI]
public interface IMfmNode
{
	public IMfmInlineNode[] Children => [];
}

public interface IMfmInlineNode : IMfmNode;

public interface IMfmBlockNode : IMfmNode;

public record MfmTextNode(string Text) : IMfmInlineNode
{
	public override string ToString() => Text;

	public static implicit operator MfmTextNode(string text) => new(text);
}

public record MfmItalicNode(
	IMfmInlineNode[] Children,
	MfmItalicNode.DelimiterType Type
) : IMfmInlineNode
{
	public enum DelimiterType
	{
		Asterisk,
		Underscore,
		HtmlTag
	}

	public override string ToString() => Type switch
	{
		DelimiterType.Asterisk   => $"*{Children.Serialize(trim: false)}*",
		DelimiterType.Underscore => $"_{Children.Serialize(trim: false)}_",
		DelimiterType.HtmlTag    => $"<i>{Children.Serialize(trim: false)}</i>",
		_                        => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
	};

	public override int  GetHashCode()                => HashCode.Combine(Children, Type);
	public virtual  bool Equals(MfmItalicNode? other) => Type == other?.Type && Children.SequenceEqual(other.Children);
}

public record MfmBoldNode(
	IMfmInlineNode[] Children,
	MfmBoldNode.DelimiterType Type
) : IMfmInlineNode
{
	public enum DelimiterType
	{
		Asterisk,
		Underscore,
		HtmlTag
	}

	public override string ToString() => Type switch
	{
		DelimiterType.Asterisk   => $"**{Children.Serialize(trim: false)}**",
		DelimiterType.Underscore => $"__{Children.Serialize(trim: false)}__",
		DelimiterType.HtmlTag    => $"<b>{Children.Serialize(trim: false)}</b>",
		_                        => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
	};

	public override int  GetHashCode()              => HashCode.Combine(Children, Type);
	public virtual  bool Equals(MfmBoldNode? other) => Type == other?.Type && Children.SequenceEqual(other.Children);
}

public record MfmStrikeNode(
	IMfmInlineNode[] Children,
	MfmStrikeNode.DelimiterType Type
) : IMfmInlineNode
{
	public enum DelimiterType
	{
		Tilde,
		HtmlTag
	}

	public override string ToString() => Type switch
	{
		DelimiterType.Tilde   => $"~~{Children.Serialize(trim: false)}~~",
		DelimiterType.HtmlTag => $"<s>{Children.Serialize(trim: false)}</s>",
		_                     => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
	};

	public override int  GetHashCode()                => HashCode.Combine(Children, Type);
	public virtual  bool Equals(MfmStrikeNode? other) => Type == other?.Type && Children.SequenceEqual(other.Children);
}

public record MfmInlineCodeNode(string Code) : IMfmInlineNode
{
	public override string ToString() => $"`{Code}`";
}

public record MfmPlainNode(string Text) : IMfmInlineNode
{
	public IMfmInlineNode[] Children => [new MfmTextNode(Text)];

	public override string ToString() => $"<plain>{Text}</plain>";
}

public record MfmSmallNode(IMfmInlineNode[] Children) : IMfmInlineNode
{
	public override string ToString() => $"<small>{Children.Serialize(trim: false)}</small>";

	public override int  GetHashCode()               => Children.GetHashCode();
	public virtual  bool Equals(MfmSmallNode? other) => other != null && Children.SequenceEqual(other.Children);
}

public record MfmEmojiCodeNode(string Name) : IMfmInlineNode
{
	public override string ToString() => $":{Name}:";
}

public record MfmHashtagNode(string Hashtag) : IMfmInlineNode
{
	public override string ToString() => $"#{Hashtag}";
}

[PublicAPI]
public record MfmMentionNode(string User, string? Host) : IMfmInlineNode
{
	public string Acct => Host is null ? User : $"{User}@{Host}";

	public override string ToString() => $"@{Acct}";
}

public record MfmUrlNode(string Url, bool Brackets) : IMfmInlineNode
{
	public override string ToString() => Brackets ? $"<{Url}>" : Url;
}

public record MfmLinkNode(string Url, string Text, bool Silent) : IMfmInlineNode
{
	public override string ToString() => (Silent ? "?" : "") + $"[{Text}]({Url})";
}

public record MfmInlineMathNode(string Formula) : IMfmInlineNode
{
	public override string ToString() => $@"\({Formula}\)";
}

public record MfmFnNode(
	string Name,
	Dictionary<string, string?>? Args,
	IMfmInlineNode[] Children
) : IMfmInlineNode
{
	private string SerializedArgs => Args is { Count: > 0 }
		? $".{string.Join(',', Args.Select(p => p.Value != null ? $"{p.Key}={p.Value}" : $"{p.Key}"))}"
		: "";

	public override string ToString() => $"$[{Name}{SerializedArgs} {Children.Serialize(trim: false)}]";

	public override int GetHashCode() => HashCode.Combine(Name, Args, Children);

	public virtual bool Equals(MfmFnNode? other)
	{
		if (other == null) return false;
		if (Name != other.Name) return false;
		if ((Args == null) != (other.Args == null)) return false;
		if (Args == null || other.Args == null) return true;
		if (Args.Count != other.Args.Count) return false;
		// ReSharper disable once UsageOfDefaultStructEquality
		return !Args.Except(other.Args).Any();
	}
}

public record MfmQuoteNode(IMfmInlineNode[] Children, bool Nested = false) : IMfmInlineNode
{
	public override string ToString()
	{
		var res = string.Join('\n', Children.Serialize(trim: true)
		                                    .Split('\n')
		                                    .Select(p => p.StartsWith('>') ? $">{p}" : $"> {p}"));

		return Nested ? '\n' + res + '\n' : res;
	}

	public override int  GetHashCode()               => Children.GetHashCode();
	public virtual  bool Equals(MfmQuoteNode? other) => other is not null && Children.SequenceEqual(other.Children);
}

public record MfmCodeBlockNode(string Code, string? Lang) : IMfmBlockNode
{
	public override string ToString() => $"\n```{Lang ?? ""}\n{Code}\n```\n";
}

public record MfmMathBlockNode(string Formula) : IMfmBlockNode
{
	public override string ToString() => $@"\[{Formula}\]";
}

public record MfmCenterNode(IMfmInlineNode[] Children) : IMfmBlockNode
{
	public override string ToString() => $"<center>\n{Children.Serialize(trim: false)}\n</center>";

	public override int  GetHashCode()                => Children.GetHashCode();
	public virtual  bool Equals(MfmCenterNode? other) => other != null && Children.SequenceEqual(other.Children);
}

public static class MfmExtensions
{
	public static string Serialize(this IEnumerable<IMfmNode> nodes, bool trim = true)
	{
		var sb = new StringBuilder();
		foreach (var node in nodes) sb.Append(node);
		return trim ? sb.ToString().Trim() : sb.ToString();
	}

	public static IMfmInlineNode ToMfm(this string text) => new MfmTextNode(text);
}
