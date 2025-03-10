using System.Text;
using JetBrains.Annotations;
using Microsoft.JavaScript.NodeApi;

namespace Iceshrimp.MfmSharp;

[PublicAPI]
[JSExport]
public interface IMfmNode
{
	public IMfmInlineNode[] Children => [];
}

[JSExport]
public interface IMfmInlineNode : IMfmNode;

[JSExport]
public interface IMfmBlockNode : IMfmNode;

[JSExport]
public class MfmTextNode(string text) : IMfmInlineNode
{
	public readonly string Text = text;

	public override string ToString() => Text;
}

[JSExport]
public class MfmItalicNode(IMfmInlineNode[] children, MfmItalicNode.DelimiterType type) : IMfmInlineNode
{
	public enum DelimiterType
	{
		Asterisk,
		Underscore,
		HtmlTag
	}

	public readonly DelimiterType Type = type;

	public IMfmInlineNode[] Children => children;

	public override string ToString() => Type switch
	{
		DelimiterType.Asterisk   => $"*{children.Serialize(trim: false)}*",
		DelimiterType.Underscore => $"_{children.Serialize(trim: false)}_",
		DelimiterType.HtmlTag    => $"<i>{children.Serialize(trim: false)}</i>",
		_                        => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
	};
}

[JSExport]
public class MfmBoldNode(IMfmInlineNode[] children, MfmBoldNode.DelimiterType type) : IMfmInlineNode
{
	public enum DelimiterType
	{
		Asterisk,
		Underscore,
		HtmlTag
	}

	public readonly DelimiterType Type = type;

	public IMfmInlineNode[] Children => children;

	public override string ToString() => Type switch
	{
		DelimiterType.Asterisk   => $"**{children.Serialize(trim: false)}**",
		DelimiterType.Underscore => $"__{children.Serialize(trim: false)}__",
		DelimiterType.HtmlTag    => $"<b>{children.Serialize(trim: false)}</b>",
		_                        => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
	};
}

[JSExport]
public class MfmStrikeNode(IMfmInlineNode[] children, MfmStrikeNode.DelimiterType type) : IMfmInlineNode
{
	public enum DelimiterType
	{
		Tilde,
		HtmlTag
	}

	public readonly DelimiterType Type = type;

	public IMfmInlineNode[] Children => children;

	public override string ToString() => Type switch
	{
		DelimiterType.Tilde   => $"~~{children.Serialize(trim: false)}~~",
		DelimiterType.HtmlTag => $"<s>{children.Serialize(trim: false)}</s>",
		_                     => throw new ArgumentOutOfRangeException(nameof(Type), Type, null)
	};
}

[JSExport]
public class MfmInlineCodeNode(string code) : IMfmInlineNode
{
	public readonly string Code = code;

	public override string ToString() => $"`{Code}`";
}

[JSExport]
public class MfmPlainNode(string text) : IMfmInlineNode
{
	public readonly string Text = text;

	public IMfmInlineNode[] Children => [new MfmTextNode(Text)];

	public override string ToString() => $"<plain>{Text}</plain>";
}

[JSExport]
public class MfmSmallNode(IMfmInlineNode[] children) : IMfmInlineNode
{
	public IMfmInlineNode[] Children => children;

	public override string ToString() => $"<small>{children.Serialize(trim: false)}</small>";
}

[JSExport]
public class MfmEmojiCodeNode(string name) : IMfmInlineNode
{
	public readonly string Name = name;

	public override string ToString() => $":{Name}:";
}

[JSExport]
public class MfmHashtagNode(string hashtag) : IMfmInlineNode
{
	public readonly string Hashtag = hashtag;

	public override string ToString() => $"#{Hashtag}";
}

[JSExport]
[PublicAPI]
public class MfmMentionNode(string user, string? host) : IMfmInlineNode
{
	public readonly string  User = user;
	public readonly string? Host = host;
	public          string  Acct => Host is null ? User : $"{User}@{Host}";

	public override string ToString() => $"@{Acct}";
}

[JSExport]
public class MfmUrlNode(string url, bool brackets) : IMfmInlineNode
{
	public readonly string Url      = url;
	public readonly bool   Brackets = brackets;

	public override string ToString() => Brackets ? $"<{Url}>" : Url;
}

[JSExport]
public class MfmLinkNode(string url, string text, bool silent) : IMfmInlineNode
{
	public readonly string Url    = url;
	public readonly string Text   = text;
	public readonly bool   Silent = silent;

	public override string ToString() => (Silent ? "?" : "") + $"[{Text}]({Url})";
}

[JSExport]
public class MfmInlineMathNode(string formula) : IMfmInlineNode
{
	public readonly string Formula = formula;

	public override string ToString() => $@"\({Formula}\)";
}

[JSExport]
public class MfmFnNode(string name, Dictionary<string, string?>? args, IMfmInlineNode[] children) : IMfmInlineNode
{
	public readonly string                       Name = name;
	public readonly Dictionary<string, string?>? Args = args;

	public IMfmInlineNode[] Children => children;

	private string SerializedArgs => Args is { Count: > 0 }
		? $".{string.Join(',', Args.Select(p => p.Value != null ? $"{p.Key}={p.Value}" : $"{p.Key}"))}"
		: "";

	public override string ToString() => $"$[{Name}{SerializedArgs} {children.Serialize(trim: false)}]";
}

[JSExport]
public class MfmQuoteNode(IMfmInlineNode[] children, bool nested = false) : IMfmInlineNode
{
	public IMfmInlineNode[] Children => children;

	public override string ToString()
	{
		var res = string.Join('\n', children.Serialize(trim: true)
		                                    .Split('\n')
		                                    .Select(p => p.StartsWith('>') ? $">{p}" : $"> {p}"));

		return nested ? '\n' + res + '\n' : res + (res.EndsWith('\n') ? "\n" : "\n\n");
	}
}

[JSExport]
public class MfmCodeBlockNode(string code, string? lang) : IMfmBlockNode
{
	public readonly string  Code = code;
	public readonly string? Lang = lang;

	public override string ToString() => $"\n```{Lang ?? ""}\n{Code}\n```\n";
}

[JSExport]
public class MfmMathBlockNode(string formula) : IMfmBlockNode
{
	public readonly string Formula = formula;

	public override string ToString() => $@"\[{Formula}\]";
}

[JSExport]
public class MfmCenterNode(IMfmInlineNode[] children) : IMfmBlockNode
{
	public IMfmInlineNode[] Children => children;

	public override string ToString() => $"<center>\n{children.Serialize(trim: false)}\n</center>";
}

public static class MfmExtensions
{
	[JSExport("toString")]
	public static string JsToString(this IEnumerable<IMfmNode> nodes) => Serialize(nodes);

	public static string Serialize(this IEnumerable<IMfmNode> nodes, bool trim = true)
	{
		var sb = new StringBuilder();
		foreach (var node in nodes) sb.Append(node);
		return trim ? sb.ToString().Trim() : sb.ToString();
	}

	public static IMfmInlineNode ToMfm(this string text) => new MfmTextNode(text);
}
