using System.Diagnostics.CodeAnalysis;
using System.Text;
using JetBrains.Annotations;

namespace Iceshrimp.MfmSharp.Examples;

[PublicAPI]
public static class MfmExamples
{
	private const int Limit = 100_000;

	public static string RealisticShortPlainText()
	{
		var sb = new StringBuilder();
		while (sb.Length < 500)
			sb.Append("test ");
		return sb.ToString();
	}

	public static string RealisticShortWithMarkup()
	{
		var sb = new StringBuilder();
		while (sb.Length < 500)
			sb.Append("test test test test test test test test *test*");
		return sb.ToString();
	}

	public static string RealisticLongPlainText()
	{
		var sb = new StringBuilder();
		while (sb.Length < 8192)
			sb.Append("test ");
		return sb.ToString();
	}

	public static string RealisticLongWithMarkup()
	{
		var sb = new StringBuilder();
		while (sb.Length < 8192)
			sb.Append("test test test test test test test test *test*");
		return sb.ToString();
	}

	public static string RealisticMfmArt() => """
	                                          <center>
	                                          $[scale.x=10,y=10 $[scale.x=10,y=110 $[scale.x=10,y=10 ⬛]]]
	                                          $[position.y=9 :neocat:                                                  :neocat_aww:]
	                                          $[position.y=4.3 $[border.radius=20                                                                        ]
	                                          ]
	                                          $[followmouse.x $[position.y=.8 $[scale.x=5,y=5  $[scale.x=0.5,y=0.5 
	                                          ⚪$[position.x=-16.5 $[scale.x=10 $[scale.x=10 ⬛]]]$[position.x=16.5,y=-1.3 $[scale.x=10 $[scale.x=10 ⬛]]]]]]]


	                                          $[position.x=-88 $[scale.x=10,y=10 $[scale.x=10,y=110 $[scale.x=10,y=10 ⬛]]]]
	                                          $[position.x=88 $[scale.x=10,y=10 $[scale.x=10,y=110 $[scale.x=10,y=10 ⬛]]]]

	                                          $[position.y=-10 Neocat Awwww Slider]
	                                          </center>
	                                          """;

	public static string JustSpaces()
	{
		return new string(' ', Limit);
	}

	public static string JustAs()
	{
		return new string('a', Limit);
	}

	public static string JustAngleBrackets()
	{
		return new string('<', Limit);
	}

	public static string UnmatchedBoldNode()
	{
		var sb = new StringBuilder();
		for (var i = 0; i < 20; i++) sb.Append("<b>");
		sb.Append(new string(' ', 20));
		return sb.ToString();
	}

	public static string UnmatchedBoldNodePadded()
	{
		var sb = new StringBuilder();
		for (var i = 0; i < 20; i++) sb.Append("<b>");
		sb.Append(new string(' ', Limit - sb.Length));
		return sb.ToString();
	}

	public static string UnmatchedBoldNodeMany()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "<b>".Length) sb.Append("<b>");
		return sb.ToString();
	}

	public static string UnmatchedBoldNodeManyNested()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "*<b>*".Length) sb.Append("*<b>*");
		return sb.ToString();
	}

	public static string UnmatchedBoldNodeManyQuoted()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "> <b>\n".Length - "</b>a".Length) sb.Append("> <b>\n");
		sb.Append("</b>a");
		return sb.ToString();
	}

	public static string UnmatchedBoldNodeManyNestedRepeating()
	{
		var sb  = new StringBuilder();
		var sb2 = new StringBuilder();
		for (var i = 0; i < 20; i++) sb2.Append("<b>");
		for (var i = 0; i < 20; i++) sb2.Append("</b>");
		var str = sb2.ToString();
		while (sb.Length < Limit - str.Length) sb.Append(str);
		return sb.ToString();
	}

	public static string MatchedBoldNodePadded()
	{
		var sb = new StringBuilder();
		for (var i = 0; i < 20; i++) sb.Append("<b>");
		sb.Append(new string(' ', Limit - sb.Length * 2 - 20));
		for (var i = 0; i < 20; i++) sb.Append("</b>");
		return sb.ToString();
	}

	public static string MatchedBoldNodeMany()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "<b>a</b>".Length) sb.Append("<b>a</b>");
		return sb.ToString();
	}

	public static string UnmatchedItalicPatternNodeMany()
	{
		var sb = new StringBuilder();
		sb.Append('*');
		sb.Append(new string(' ', Limit - sb.Length));
		return sb.ToString();
	}

	public static string ManyUrlUnmatched()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "<https://a".Length) sb.Append("<https://a");
		return sb.ToString();
	}

	public static string ManyUrlMatched()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "<https://a>".Length) sb.Append("<https://a>");
		return sb.ToString();
	}

	public static string ManyUrlStandalone()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "https://a ".Length) sb.Append("https://a ");
		return sb.ToString();
	}

	public static string ManyUrlUnmatchedInvalid()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "<https://".Length) sb.Append("<https://");
		return sb.ToString();
	}

	public static string ManyUrlMatchedInvalid()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "<https://>".Length) sb.Append("<https://>");
		return sb.ToString();
	}

	public static string ManyUrlStandaloneInvalid()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "https:// ".Length) sb.Append("https:// ");
		return sb.ToString();
	}

	public static string LongUrl()
	{
		var sb = new StringBuilder();
		sb.Append("https://a.com/");
		sb.Append(new string('(', 200));
		sb.Append(new string('a', Limit - sb.Length - 2));
		sb.Append(')');
		sb.Append('a');
		return sb.ToString();
	}

	public static string ManyUrlBrackets()
	{
		var sb = new StringBuilder();
		sb.Append("https://a.com/");
		sb.Append(new string('(', 49995));
		sb.Append('a');
		sb.Append(new string(')', 49994));
		sb.Append('a');
		return sb.ToString();
	}

	public static string ManyUrlBracketsAlt()
	{
		var sb = new StringBuilder();
		sb.Append("https://a.com/");
		sb.Append(new string('(', 49994));
		sb.Append('a');
		sb.Append(new string(')', 49995));
		sb.Append('a');
		return sb.ToString();
	}

	public static string ManyUrlBracketsAlt2()
	{
		var sb = new StringBuilder();
		sb.Append("https://a.com/");
		while (sb.Length < Limit - "()".Length)
			sb.Append("()");
		return sb.ToString();
	}

	public static string RegularBoldTag()
	{
		var sb = new StringBuilder();
		sb.Append("<b>sdjfhskjdhf</b><b>sdjfhskjdhf</b><b>sdjfhskjdhf</b><b>sdjfhskjdhf</b><b>sdjfhskjdhf</b><b>sdjfhskjdhf</b><b>sdjfhskjdhf</b>");
		sb.Append(new string(' ', Limit - sb.Length));
		return sb.ToString();
	}

	public static string FnShenanigans()
	{
		var sb = new StringBuilder();
		for (var i = 0; i < 20; i++) sb.Append("$[spin ");
		sb.Append(new string(' ', 2000));
		sb.Append("$[spin fox]");
		return sb.ToString();
	}

	public static string FnShenanigansAlt()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "$[spin ]".Length * 20 - 1)
		{
			for (var i = 0; i < 20; i++) sb.Append("$[spin ");
			sb.Append('h');
			for (var i = 0; i < 20; i++) sb.Append(']');
		}

		return sb.ToString();
	}

	public static string FnBoldIterating()
	{
		var sb = new StringBuilder();
		while (sb.Length < 99000)
		{
			for (var i = 0; i < 20; i++) sb.Append("$[spin <b>");
			for (var i = 0; i < 20; i++) sb.Append("</b>]");
		}

		return sb.ToString();
	}

	public static string ManyInlineCode()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - "`a`".Length) sb.Append("`a`");
		return sb.ToString();
	}

	public static string ManyQuote()
	{
		var sb = new StringBuilder();
		while (sb.Length < Limit - ">t\n".Length) sb.Append(">t\n");
		return sb.ToString();
	}
}
