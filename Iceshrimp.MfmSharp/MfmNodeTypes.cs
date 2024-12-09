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

public class MfmTextNode(string text) : IMfmInlineNode
{
	public readonly string Text = text;

	public override string ToString() => Text;
}

public class MfmItalicNode(IMfmInlineNode[] children, MfmItalicNode.DelimiterType type) : IMfmInlineNode
{
	public enum DelimiterType
	{
		Asterisk,
		Underscore,
		HtmlTag
	}

	public readonly DelimiterType Type = type;

	IMfmInlineNode[] IMfmNode.Children => children;

	public override string ToString() => Type switch
	{
		DelimiterType.Asterisk   => $"*{children.Serialize(trim: false)}*",
		DelimiterType.Underscore => $"_{children.Serialize(trim: false)}_",
		DelimiterType.HtmlTag    => $"<i>{children.Serialize(trim: false)}</i>",
		_                        => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
	};
}

public class MfmBoldNode(IMfmInlineNode[] children, MfmBoldNode.DelimiterType type) : IMfmInlineNode
{
	public enum DelimiterType
	{
		Asterisk,
		Underscore,
		HtmlTag
	}

	public readonly DelimiterType Type = type;

	IMfmInlineNode[] IMfmNode.Children => children;

	public override string ToString() => Type switch
	{
		DelimiterType.Asterisk   => $"**{children.Serialize(trim: false)}**",
		DelimiterType.Underscore => $"__{children.Serialize(trim: false)}__",
		DelimiterType.HtmlTag    => $"<b>{children.Serialize(trim: false)}</b>",
		_                        => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
	};
}

public class MfmStrikeNode(IMfmInlineNode[] children, MfmStrikeNode.DelimiterType type) : IMfmInlineNode
{
	public enum DelimiterType
	{
		Tilde,
		HtmlTag
	}

	public readonly DelimiterType Type = type;

	IMfmInlineNode[] IMfmNode.Children => children;

	public override string ToString() => Type switch
	{
		DelimiterType.Tilde   => $"~~{children.Serialize(trim: false)}~~",
		DelimiterType.HtmlTag => $"<s>{children.Serialize(trim: false)}</s>",
		_                     => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
	};
}

public class MfmInlineCodeNode(string code) : IMfmInlineNode
{
	public readonly string Code = code;

	public override string ToString() => $"`{Code}`";
}

public class MfmPlainNode(string text) : IMfmInlineNode
{
	public readonly string Text = text;

	IMfmInlineNode[] IMfmNode.Children => [new MfmTextNode(Text)];

	public override string ToString() => $"<plain>{Text}</plain>";
}

public class MfmSmallNode(IMfmInlineNode[] children) : IMfmInlineNode
{
	IMfmInlineNode[] IMfmNode.Children => children;

	public override string ToString() => $"<small>{children.Serialize(trim: false)}</small>";
}

public class MfmEmojiCodeNode(string name) : IMfmInlineNode
{
	public readonly string Name = name;

	public override string ToString() => $":{Name}:";
}

public class MfmHashtagNode(string hashtag) : IMfmInlineNode
{
	public readonly string Hashtag = hashtag;

	public override string ToString() => $"#{Hashtag}";
}

[PublicAPI]
public class MfmMentionNode(string user, string? host) : IMfmInlineNode
{
	public readonly string  User = user;
	public readonly string? Host = host;
	public string  Acct => Host is null ? User : $"{User}@{Host}";

	public override string ToString() => $"@{Acct}";
}

public class MfmUrlNode(string url, bool brackets) : IMfmInlineNode
{
	public readonly string Url      = url;
	public readonly bool   Brackets = brackets;

	public override string ToString() => Brackets ? $"<{Url}>" : Url;
}

public class MfmLinkNode(string url, string text, bool silent) : IMfmInlineNode
{
	public readonly string Url    = url;
	public readonly string Text   = text;
	public readonly bool   Silent = silent;

	public override string ToString() => (Silent ? "?" : "") + $"[{Text}]({Url})";
}

public class MfmInlineMathNode(string formula) : IMfmInlineNode
{
	public readonly string Formula = formula;

	public override string ToString() => $@"\({Formula}\)";
}

public class MfmFnNode(string name, Dictionary<string, string?>? args, IMfmInlineNode[] children) : IMfmInlineNode
{
	public readonly string                       Name = name;
	public readonly Dictionary<string, string?>? Args = args;

	IMfmInlineNode[] IMfmNode.Children => children;

	private string SerializedArgs => Args is { Count: > 0 }
		? $".{string.Join(',', Args.Select(p => p.Value != null ? $"{p.Key}={p.Value}" : $"{p.Key}"))}"
		: "";

	public override string ToString() => $"$[{Name}{SerializedArgs} {children.Serialize(trim: false)}]";
}

public class MfmQuoteNode(IMfmInlineNode[] children, bool nested = false) : IMfmInlineNode
{
	IMfmInlineNode[] IMfmNode.Children => children;

	public override string ToString()
	{
		var res = string.Join('\n', children.Serialize(trim: true)
		                                    .Split('\n')
		                                    .Select(p => p.StartsWith('>') ? $">{p}" : $"> {p}"));

		return nested ? '\n' + res + '\n' : res;
	}
}

public class MfmCodeBlockNode(string code, string? lang) : IMfmBlockNode
{
	public readonly string  Code = code;
	public readonly string? Lang = lang;

	public override string ToString() => $"\n```{Lang ?? ""}\n{Code}\n```\n";
}

public class MfmMathBlockNode(string formula) : IMfmBlockNode
{
	public readonly string Formula = formula;

	public override string ToString() => $@"\[{Formula}\]";
}

public class MfmCenterNode(IMfmInlineNode[] children) : IMfmBlockNode
{
	IMfmInlineNode[] IMfmNode.Children => children;

	public override string ToString() => $"<center>\n{children.Serialize(trim: false)}\n</center>";
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
