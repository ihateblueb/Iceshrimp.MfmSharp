using System.Reflection;
using System.Text;
using FluentAssertions;
using Iceshrimp.MfmSharp.Examples;

namespace Iceshrimp.MfmSharp.Tests;

[TestClass]
public class MfmTests
{
	[TestInitialize]
	public void Initialize() => AssertionOptions.FormattingOptions.MaxDepth = 100;

	private static void AssertEquals(string input, List<MfmNode> expected, string? canonical = null)
	{
		var res = MfmParser.Parse(input);
		res.Should().Equal(expected);
		res.Serialize().Should().BeEquivalentTo(canonical ?? input);
	}

	[TestMethod]
	public void TestParseText()
	{
		var input = new string('a', 100_000);
		AssertEquals(input, [new MfmTextNode(input)]);
	}

	[TestMethod]
	public void TestParseBoundaryChars() => AssertEquals("test:", ["test:"]);

	[TestMethod]
	public void TestParseEmptyTag()
		=> AssertEquals("<b></b>", [new MfmBoldNode([], MfmBoldNode.DelimiterType.HtmlTag)]);

	[TestMethod]
	public void TestParseNestedTag()
	{
		AssertEquals("<b><b>a</b></b>",
		[
			new MfmBoldNode([new MfmBoldNode(["a"], MfmBoldNode.DelimiterType.HtmlTag)],
			                MfmBoldNode.DelimiterType.HtmlTag)
		]);

		AssertEquals("<b><b><b>a</b></b></b>",
		[
			new MfmBoldNode(
			[ //
				new MfmBoldNode(
				[ //
					new MfmBoldNode(
					[ //
						"a"
					], MfmBoldNode.DelimiterType.HtmlTag)
				], MfmBoldNode.DelimiterType.HtmlTag)
			], MfmBoldNode.DelimiterType.HtmlTag)
		]);

		AssertEquals("<b><b><b><b>a</b></b></b></b>",
		[
			new MfmBoldNode(
			[ //
				new MfmBoldNode(
				[ //
					new MfmBoldNode(
					[ //
						new MfmBoldNode(
						[ //
							"a"
						], MfmBoldNode.DelimiterType.HtmlTag)
					], MfmBoldNode.DelimiterType.HtmlTag)
				], MfmBoldNode.DelimiterType.HtmlTag)
			], MfmBoldNode.DelimiterType.HtmlTag)
		]);
	}

	[TestMethod]
	public void TestParseUnmatchedNestedTag()
	{
		AssertEquals("<b><b>a</b>",
		             [new MfmBoldNode(["<b>a"], MfmBoldNode.DelimiterType.HtmlTag)]);
	}

	[TestMethod]
	public void TestParseItalic()
	{
		List<MfmNode> expected = ["test ", new MfmItalicNode(["test"], MfmItalicNode.DelimiterType.Asterisk), " test"];

		AssertEquals("test *test* test", expected);

		expected = ["test ", new MfmItalicNode(["test"], MfmItalicNode.DelimiterType.Underscore), " test"];

		AssertEquals("test _test_ test", expected);

		expected = ["test *test\ntest* test"];
		AssertEquals("test *test\ntest* test", expected);

		expected = ["test _test\ntest_ test"];
		AssertEquals("test _test\ntest_ test", expected);

		expected = ["test ", new MfmItalicNode(["test"], MfmItalicNode.DelimiterType.HtmlTag), " test"];

		AssertEquals("test <i>test</i> test", expected);

		expected = ["test ", new MfmItalicNode(["test\ntest"], MfmItalicNode.DelimiterType.HtmlTag), " test"];

		AssertEquals("test <i>test\ntest</i> test", expected);

		// False positives
		AssertEquals("test*test*test", ["test*test*test"]);
		AssertEquals("test_test_test", ["test_test_test"]);

		AssertEquals("test*test* test", ["test*test* test"]);
		AssertEquals("test_test_ test", ["test_test_ test"]);

		AssertEquals("test *test*test", ["test *test*test"]);
		AssertEquals("test _test_test", ["test _test_test"]);

		AssertEquals("* test\n* test2\n* test3", ["* test\n* test2\n* test3"]);
	}

	[TestMethod]
	public void TestParseBold()
	{
		List<MfmNode> expected = ["test ", new MfmBoldNode(["test"], MfmBoldNode.DelimiterType.Asterisk), " test"];

		AssertEquals("test **test** test", expected);

		expected = ["test ", new MfmBoldNode(["test"], MfmBoldNode.DelimiterType.Underscore), " test"];

		AssertEquals("test __test__ test", expected);

		expected = ["test **test\ntest** test"];
		AssertEquals("test **test\ntest** test", expected);

		expected = ["test __test\ntest__ test"];
		AssertEquals("test __test\ntest__ test", expected);

		expected = ["test ", new MfmBoldNode(["test"], MfmBoldNode.DelimiterType.HtmlTag), " test"];

		AssertEquals("test <b>test</b> test", expected);

		expected = ["test ", new MfmBoldNode(["test\ntest"], MfmBoldNode.DelimiterType.HtmlTag), " test"];

		AssertEquals("test <b>test\ntest</b> test", expected);

		// False positives
		AssertEquals("test**test**test", ["test**test**test"]);
		AssertEquals("test__test__test", ["test__test__test"]);

		AssertEquals("test**test** test", ["test**test** test"]);
		AssertEquals("test__test__ test", ["test__test__ test"]);

		AssertEquals("test **test**test", ["test **test**test"]);
		AssertEquals("test __test__test", ["test __test__test"]);
	}

	[TestMethod]
	public void TestParseBoldItalic()
	{
		// @formatter:off
		List<MfmNode> expected =
		[
			new MfmItalicNode([
				"italic ",
				new MfmBoldNode(["bold"], MfmBoldNode.DelimiterType.Asterisk),
				" italic"
			], MfmItalicNode.DelimiterType.Asterisk)
		];
		// @formatter:on

		AssertEquals("*italic **bold** italic*", expected);
	}

	[TestMethod]
	public void TestParseStrike()
	{
		List<MfmNode> expected = ["test ", new MfmStrikeNode(["test"], MfmStrikeNode.DelimiterType.Tilde), " test"];

		AssertEquals("test ~~test~~ test", expected);

		expected = ["test ~~test\ntest~~ test"];
		AssertEquals("test ~~test\ntest~~ test", expected);

		expected = ["test ", new MfmStrikeNode(["test"], MfmStrikeNode.DelimiterType.HtmlTag), " test"];

		AssertEquals("test <s>test</s> test", expected);

		expected = ["test ", new MfmStrikeNode(["test\ntest"], MfmStrikeNode.DelimiterType.HtmlTag), " test"];

		AssertEquals("test <s>test\ntest</s> test", expected);

		// False positives
		AssertEquals("test~~test~~test", ["test~~test~~test"]);
		AssertEquals("test~~test~~ test", ["test~~test~~ test"]);
		AssertEquals("test ~~test~~test", ["test ~~test~~test"]);
	}

	[TestMethod]
	public void TestParseHashtag()
	{
		// General hashtag handling
		AssertEquals("#test", [new MfmHashtagNode("test")]);
		AssertEquals("#test's", [new MfmHashtagNode("test"), "'s"]);
		AssertEquals("#t-e_s-t.", [new MfmHashtagNode("t-e_s-t"), "."]);

		// False positives
		AssertEquals("#", ["#"]);
		AssertEquals("##", ["##"]);
		AssertEquals("test # test", ["test # test"]);

		// Whitespace handling
		AssertEquals("#test test", [new MfmHashtagNode("test"), " test"]);
		AssertEquals("test #test", ["test ", new MfmHashtagNode("test")]);
		AssertEquals("test #test test",
		             ["test ", new MfmHashtagNode("test"), " test"]);
	}

	[TestMethod]
	public void TestParseEmojiCode()
	{
		List<MfmNode> expected =
		[
			new MfmEmojiCodeNode("test"),
			" test ",
			new MfmEmojiCodeNode("test"),
			new MfmEmojiCodeNode("test"),
			" :",
			new MfmEmojiCodeNode("test"),
			": :test*test: ",
			new MfmEmojiCodeNode("test")
		];

		AssertEquals(":test: test :test::test: ::test:: :test*test: :test:", expected);
		AssertEquals(":test\ntest:", [":test\ntest:"]);
		AssertEquals(":test\n:", [":test\n:"]);
	}

	[TestMethod]
	public void TestParseInlineCode()
	{
		List<MfmNode> expected =
		[
			new MfmInlineCodeNode("test"),
			" test ",
			new MfmInlineCodeNode("test"),
			new MfmInlineCodeNode("test"),
			" `",
			new MfmInlineCodeNode("test"),
			" ",
			new MfmInlineCodeNode("test")
		];

		AssertEquals("`test` test `test``test` ``test` `test`", expected);
		AssertEquals("`test\ntest`", ["`test\ntest`"]);
		AssertEquals("`test\n`", ["`test\n`"]);
	}

	[TestMethod]
	public void TestParseCodeBlock()
	{
		// General code block handling
		AssertEquals("```\nhello\n```", [new MfmCodeBlockNode("hello", null)]);
		AssertEquals("```\nhel\nlo\n```", [new MfmCodeBlockNode("hel\nlo", null)]);
		AssertEquals("```lang\nhello\n```", [new MfmCodeBlockNode("hello", "lang")]);
		AssertEquals("```lang\nhello\n```\n```\nhello\n```",
		             [new MfmCodeBlockNode("hello", "lang"), new MfmCodeBlockNode("hello", null)],
		             "```lang\nhello\n```\n\n```\nhello\n```");

		// Whitespace & newline handling
		AssertEquals("test ```lang\nhello\n```", ["test ```lang\nhello\n```"]);
		AssertEquals("test ```hello``` test",
		             ["test ``", new MfmInlineCodeNode("hello"), "`` test"]);
	}

	[TestMethod]
	public void TestParseCenter()
	{
		AssertEquals("<center>test</center>", [new MfmCenterNode(["test"])],
		             "<center>\ntest\n</center>");
		AssertEquals("<center>test\ntest</center>", [new MfmCenterNode(["test\ntest"])],
		             "<center>\ntest\ntest\n</center>");

		AssertEquals("*<center>test</center>*",
		             [new MfmItalicNode(["<center>test</center>"], MfmItalicNode.DelimiterType.Asterisk)]);

		AssertEquals("test <center>test</center> test", ["test <center>test</center> test"]);
	}

	[TestMethod]
	public void TestParseInlineMath()
	{
		AssertEquals("\\(test\\)", [new MfmInlineMathNode("test")]);
		AssertEquals("\\(test\ntest\\)", ["\\(test\ntest\\)"]);
	}

	[TestMethod]
	public void TestParseMathBlock()
	{
		AssertEquals("\\[test\\]", [new MfmMathBlockNode("test")]);
		AssertEquals("\\[test\ntest\\]", [new MfmMathBlockNode("test\ntest")]);
	}

	[TestMethod]
	public void TestParseSmall()
	{
		AssertEquals("<small>test</small>", [new MfmSmallNode(["test"])]);
		AssertEquals("<small>test\ntest</small>", [new MfmSmallNode(["test\ntest"])]);

		List<MfmNode> expected =
		[
			new MfmItalicNode([new MfmSmallNode(["test"])], MfmItalicNode.DelimiterType.Asterisk)
		];

		AssertEquals("*<small>test</small>*", expected);
	}

	[TestMethod]
	public void TestParsePlain()
	{
		AssertEquals("<plain>test</plain>", [new MfmPlainNode("test")]);
		AssertEquals("<plain>test\ntest</plain>", [new MfmPlainNode("test\ntest")]);

		List<MfmNode> expected = [new MfmItalicNode([new MfmPlainNode("test")], MfmItalicNode.DelimiterType.Asterisk)];
		AssertEquals("*<plain>test</plain>*", expected);
	}

	[TestMethod]
	public void TestParseUrl()
	{
		// General url handling
		AssertEquals("http://example.org", [new MfmUrlNode("http://example.org/", false)], "http://example.org/");
		AssertEquals("https://example.org", [new MfmUrlNode("https://example.org/", false)], "https://example.org/");
		AssertEquals("https://example.org.", [new MfmUrlNode("https://example.org./", false)], "https://example.org./");
		AssertEquals("https://example.org/.", [new MfmUrlNode("https://example.org/", false)], "https://example.org/");
		AssertEquals("https://example.org/asd.", [new MfmUrlNode("https://example.org/asd.", false)]);

		// Parenthesis tracking
		AssertEquals("https://example.org/(test", [new MfmUrlNode("https://example.org/(test", false)]);
		AssertEquals("https://example.org/te)st", [new MfmUrlNode("https://example.org/te", false), ")st"]);

		AssertEquals("(https://example.org)", ["(", new MfmUrlNode("https://example.org/", false), ")"],
		             "(https://example.org/)");

		AssertEquals("(https://example.org/(asd))", ["(", new MfmUrlNode("https://example.org/(asd)", false), ")"]);
		AssertEquals("(https://example.org/((asd)))", ["(", new MfmUrlNode("https://example.org/((asd))", false), ")"]);
		AssertEquals("(https://example.org/((asd))", ["(", new MfmUrlNode("https://example.org/((asd))", false)]);

		// Newline handling
		AssertEquals("https://test.com/asd\nasd", [new MfmUrlNode("https://test.com/asd", false), "\nasd"]);

		// Whitespace handling
		AssertEquals("test http://example.org test", ["test ", new MfmUrlNode("http://example.org/", false), " test"],
		             "test http://example.org/ test");

		AssertEquals("http://example.org test", [new MfmUrlNode("http://example.org/", false), " test"],
		             "http://example.org/ test");

		AssertEquals("test http://example.org", ["test ", new MfmUrlNode("http://example.org/", false)],
		             "test http://example.org/");
	}

	[TestMethod]
	public void TestParseUrlBrackets()
	{
		// General url handling
		AssertEquals("<http://example.org>", [new MfmUrlNode("http://example.org/", true)], "<http://example.org/>");
		AssertEquals("<https://example.org>", [new MfmUrlNode("https://example.org/", true)], "<https://example.org/>");
		AssertEquals("<https://example.org.>", [new MfmUrlNode("https://example.org./", true)],
		             "<https://example.org./>");
		AssertEquals("<https://example.org/.>", [new MfmUrlNode("https://example.org/", true)],
		             "<https://example.org/>");
		AssertEquals("<https://example.org/asd.>", [new MfmUrlNode("https://example.org/asd.", true)]);

		// Parenthesis tracking
		AssertEquals("(<https://example.org>)", ["(", new MfmUrlNode("https://example.org/", true), ")"],
		             "(<https://example.org/>)");

		AssertEquals("(<https://example.org/(asd)>)", ["(", new MfmUrlNode("https://example.org/(asd)", true), ")"]);
		AssertEquals("(<https://example.org/((asd))>)",
		             ["(", new MfmUrlNode("https://example.org/((asd))", true), ")"]);
		AssertEquals("(<https://example.org/((asd)>)", ["(", new MfmUrlNode("https://example.org/((asd)", true), ")"]);

		// Newline handling
		AssertEquals("<https://test.com/asd\nasd>", ["<", new MfmUrlNode("https://test.com/asd", false), "\nasd>"]);

		// Whitespace handling
		AssertEquals("test <http://example.org> test", ["test ", new MfmUrlNode("http://example.org/", true), " test"],
		             "test <http://example.org/> test");

		AssertEquals("<http://example.org> test", [new MfmUrlNode("http://example.org/", true), " test"],
		             "<http://example.org/> test");

		AssertEquals("test <http://example.org>", ["test ", new MfmUrlNode("http://example.org/", true)],
		             "test <http://example.org/>");
	}

	[TestMethod]
	public void TestParseLink()
	{
		// General url handling
		AssertEquals("[test](http://example.org)", [new MfmLinkNode("http://example.org/", "test", false)],
		             "[test](http://example.org/)");

		AssertEquals("[test](https://example.org)", [new MfmLinkNode("https://example.org/", "test", false)],
		             "[test](https://example.org/)");
		AssertEquals("[test](https://example.org.)", [new MfmLinkNode("https://example.org./", "test", false)],
		             "[test](https://example.org./)");
		AssertEquals("[test](https://example.org/.)", [new MfmLinkNode("https://example.org/", "test", false)],
		             "[test](https://example.org/)");
		AssertEquals("[test](https://example.org/asd.)", [new MfmLinkNode("https://example.org/asd.", "test", false)]);

		// Parenthesis tracking
		AssertEquals("[test]https://example.org/a)", ["[test]", new MfmUrlNode("https://example.org/a", false), ")"]);

		AssertEquals("[test](https://example.org/a_(test)",
		             ["[test](", new MfmUrlNode("https://example.org/a_(test)", false)]);

		AssertEquals("([test](https://example.org))",
		             ["(", new MfmLinkNode("https://example.org/", "test", false), ")"],
		             "([test](https://example.org/))");

		AssertEquals("([test](https://example.org/(asd)))",
		             ["(", new MfmLinkNode("https://example.org/(asd)", "test", false), ")"]);

		AssertEquals("([test](https://example.org/((asd))))",
		             ["(", new MfmLinkNode("https://example.org/((asd))", "test", false), ")"]);

		AssertEquals("([test](https://example.org/((asd)))",
		             ["(", new MfmLinkNode("https://example.org/((asd))", "test", false)]);

		// Newline handling
		AssertEquals("[test](https://test.com/asd\nasd)",
		             ["[test](", new MfmUrlNode("https://test.com/asd", false), "\nasd)"]);

		AssertEquals("[test\ntest](https://test.com/asd)",
		             ["[test\ntest](", new MfmUrlNode("https://test.com/asd", false), ")"]);

		// Whitespace handling
		AssertEquals("test [test](http://example.org) test",
		             ["test ", new MfmLinkNode("http://example.org/", "test", false), " test"],
		             "test [test](http://example.org/) test");

		AssertEquals("[test](http://example.org) test",
		             [new MfmLinkNode("http://example.org/", "test", false), " test"],
		             "[test](http://example.org/) test");

		AssertEquals("test [test](http://example.org)",
		             ["test ", new MfmLinkNode("http://example.org/", "test", false)],
		             "test [test](http://example.org/)");
	}

	[TestMethod]
	public void TestParseLinkSilent()
	{
		// General url handling
		AssertEquals("?[test](http://example.org)", [new MfmLinkNode("http://example.org/", "test", true)],
		             "?[test](http://example.org/)");

		AssertEquals("?[test](https://example.org)", [new MfmLinkNode("https://example.org/", "test", true)],
		             "?[test](https://example.org/)");
		AssertEquals("?[test](https://example.org.)", [new MfmLinkNode("https://example.org./", "test", true)],
		             "?[test](https://example.org./)");
		AssertEquals("?[test](https://example.org/.)", [new MfmLinkNode("https://example.org/", "test", true)],
		             "?[test](https://example.org/)");
		AssertEquals("?[test](https://example.org/asd.)", [new MfmLinkNode("https://example.org/asd.", "test", true)]);

		// Parenthesis tracking
		AssertEquals("(?[test](https://example.org))",
		             ["(", new MfmLinkNode("https://example.org/", "test", true), ")"],
		             "(?[test](https://example.org/))");

		AssertEquals("(?[test](https://example.org/(asd)))",
		             ["(", new MfmLinkNode("https://example.org/(asd)", "test", true), ")"]);

		AssertEquals("(?[test](https://example.org/((asd))))",
		             ["(", new MfmLinkNode("https://example.org/((asd))", "test", true), ")"]);

		AssertEquals("(?[test](https://example.org/((asd)))",
		             ["(", new MfmLinkNode("https://example.org/((asd))", "test", true)]);

		// Newline handling
		AssertEquals("?[test](https://test.com/asd\nasd)",
		             ["?[test](", new MfmUrlNode("https://test.com/asd", false), "\nasd)"]);

		AssertEquals("?[test\ntest](https://test.com/asd)",
		             ["?[test\ntest](", new MfmUrlNode("https://test.com/asd", false), ")"]);

		// Whitespace handling
		AssertEquals("test ?[test](http://example.org) test",
		             ["test ", new MfmLinkNode("http://example.org/", "test", true), " test"],
		             "test ?[test](http://example.org/) test");

		AssertEquals("?[test](http://example.org) test",
		             [new MfmLinkNode("http://example.org/", "test", true), " test"],
		             "?[test](http://example.org/) test");

		AssertEquals("test ?[test](http://example.org)",
		             ["test ", new MfmLinkNode("http://example.org/", "test", true)],
		             "test ?[test](http://example.org/)");
	}

	[TestMethod]
	public void TestParseMention()
	{
		// General mention handling
		AssertEquals("@test", [new MfmMentionNode("test", null)]);
		AssertEquals("@test@instance.tld", [new MfmMentionNode("test", "instance.tld")]);
		AssertEquals("@test_", [new MfmMentionNode("test_", null)]);
		AssertEquals("@_test", [new MfmMentionNode("_test", null)]);
		AssertEquals("@test_@ins-tance.tld", [new MfmMentionNode("test_", "ins-tance.tld")]);
		AssertEquals("@_test@xn--mastodn-f1a.de", [new MfmMentionNode("_test", "xn--mastodn-f1a.de")]);
		AssertEquals("@_test@-xn--mastodn-f1a.de", ["@_test@-xn--mastodn-f1a.de"]);

		// False positives
		AssertEquals("@", ["@"]);
		AssertEquals("@@", ["@@"]);
		AssertEquals("@@test", ["@@test"]);
		AssertEquals("@test@", ["@test@"]);
		AssertEquals("test @ test", ["test @ test"]);
		AssertEquals("test @test@ test", ["test @test@ test"]);
		AssertEquals("@test:test.com", ["@test:test.com"]);

		// Trailing colon
		AssertEquals("@test:", [new MfmMentionNode("test", null), ":"]);
		AssertEquals("@test:\ntest", [new MfmMentionNode("test", null), ":\ntest"]);
		AssertEquals("@test@test.com:", [new MfmMentionNode("test", "test.com"), ":"]);
		AssertEquals("@test@test.com:\ntest", [new MfmMentionNode("test", "test.com"), ":\ntest"]);

		// Trailing dot
		AssertEquals("@test@asdf.com.", [new MfmMentionNode("test", "asdf.com"), "."]);
		AssertEquals("@test.", [new MfmMentionNode("test", null), "."]);

		// Whitespace handling
		AssertEquals("test @test", ["test ", new MfmMentionNode("test", null)]);
		AssertEquals("@test test", [new MfmMentionNode("test", null), " test"]);
		AssertEquals("test @test test", ["test ", new MfmMentionNode("test", null), " test"]);

		// Newline handling
		AssertEquals("@te\nst", [new MfmMentionNode("te", null), "\nst"]);
		AssertEquals("@test@instan\nce.tld", ["@test@instan\nce.tld"]);

		// Parenthesis handling
		AssertEquals("(@test@domain.tld)", ["(", new MfmMentionNode("test", "domain.tld"), ")"]);
	}

	[TestMethod]
	public void TestParseQuote()
	{
		//AssertEquals(">test", [new MfmQuoteNode(["test"])], "> test");
		//AssertEquals("> test", [new MfmQuoteNode(["test"])]);
		//AssertEquals("> test\n> test", [new MfmQuoteNode(["test", "\n", "test"])]);
		//AssertEquals(">test\n>\n>test", [new MfmQuoteNode(["test", "\n", "\n", "test"])], "> test\n> \n> test");
		//AssertEquals(">\n>test", [">\n", new MfmQuoteNode(["test"])], ">\n> test");
		AssertEquals(">test\n>", [new MfmQuoteNode(["test"]), "\n>"], "> test\n>");
	}

	[TestMethod]
	public void TestParseQuoteInline()
	{
		const string input =
			"""
			this is plain text > this is not a quote >this is also not a quote
			> this is a quote
			> this is part of the same quote
			>this too

			this is some plain text inbetween
			>this is a second quote
			> this is part of the second quote

			> this is a third quote
			and this is some plain text to close it off
			""";

		const string canonical =
			"""
			this is plain text > this is not a quote >this is also not a quote
			> this is a quote
			> this is part of the same quote
			> this too

			this is some plain text inbetween
			> this is a second quote
			> this is part of the second quote

			> this is a third quote
			and this is some plain text to close it off
			""";

		// @formatter:off
		List<MfmNode> expected =
		[
			"this is plain text > this is not a quote >this is also not a quote\n",
			new MfmQuoteNode([
				"this is a quote",
				"\n",
				"this is part of the same quote",
				"\n",
				"this too"
			]),
			"\n\nthis is some plain text inbetween\n",
			new MfmQuoteNode([
				"this is a second quote",
				"\n",
				"this is part of the second quote"
			]),
			"\n\n",
			new MfmQuoteNode(["this is a third quote"]),
			"\nand this is some plain text to close it off"
		];
		// @formatter:on

		AssertEquals(input, expected, canonical);
	}

	[TestMethod]
	public void TestParseNestedQuote()
	{
		// regular nestquote
		AssertEquals(">test\n>>test\n>test",
		             [new MfmQuoteNode(["test", new MfmQuoteNode(["test"], true), "test"])], "> test\n>> test\n> test");

		// depth > 0
		AssertEquals(">>test", [new MfmQuoteNode([new MfmQuoteNode(["test"], true)])], ">> test");

		// first line with depth > 0
		AssertEquals(">>test\n>test", [new MfmQuoteNode([new MfmQuoteNode(["test"], true), "test"])],
		             ">> test\n> test");

		// only lines with depth > 0
		AssertEquals(">>test\n>>test", [new MfmQuoteNode([new MfmQuoteNode(["test", "\n", "test"])])],
		             ">> test\n>> test");

		// nesting limit - should be accepted
		AssertEquals(">>>>>test",
		[
			new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode(["test"])])])])])
		], ">>>>> test");

		// nesting limit - should be rejected
		AssertEquals(">>>>>>test",
		[
			new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode([">test"])])])])])
		]);

		// nesting limit - should be rejected
		// @formatter:off
		AssertEquals(">test\n>>test\n>>>test\n>>>>test\n>>>>>test\n>>>>>>test",
		[
			new MfmQuoteNode([
				"test",
				new MfmQuoteNode([
					"test",
					new MfmQuoteNode([
						"test",
						new MfmQuoteNode([
							"test",
							new MfmQuoteNode([
								"test",
								"\n",
								">test"
							])
						])
					])
				])
			])
		],
		"> test\n>> test\n>>> test\n>>>> test\n>>>>> test\n>>>>>>test");
		// @formatter:on

		// nesting limit - should be rejected
		// @formatter:off
		AssertEquals(">test\n>>test\n>>>test\n>>>>test\n>>>>>test\n>>>>>>test\n>>>>>test\n>>>>test\n>>>test\n>>test\n>test",
		[
			new MfmQuoteNode([
				"test",
				new MfmQuoteNode([
					"test",
					new MfmQuoteNode([
						"test",
						new MfmQuoteNode([
							"test",
							new MfmQuoteNode([
								"test",
								"\n",
								">test",
								"\n",
								"test"
							]),
							"test"
						]),
						"test"
					]),
					"test"
				]),
				"test"
			])
		],
		"> test\n>> test\n>>> test\n>>>> test\n>>>>> test\n>>>>>>test\n>>>>> test\n>>>> test\n>>> test\n>> test\n> test");
		// @formatter:on

		// gaps in nesting sequence
		AssertEquals(">test\n>>>test\n>test",
		             [new MfmQuoteNode(["test", new MfmQuoteNode([new MfmQuoteNode(["test"])]), "test"])],
		             "> test\n>>> test\n> test");

		// more gaps
		AssertEquals(">test\n>>>test\n>>>>>test\n>>>test\n>test",
		[
			new MfmQuoteNode([
				"test",
				new MfmQuoteNode([new MfmQuoteNode(["test", new MfmQuoteNode([new MfmQuoteNode(["test"])]), "test"])]),
				"test"
			])
		], "> test\n>>> test\n>>>>> test\n>>> test\n> test");

		// gap at start
		AssertEquals(">>>test\n>test",
		             [new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode(["test"])]), "test"])], ">>> test\n> test");

		// gaps at start and end
		AssertEquals(">>>test\n>test\n>>>test",
		[
			new MfmQuoteNode([
				new MfmQuoteNode([new MfmQuoteNode(["test"])]), "test", new MfmQuoteNode([new MfmQuoteNode(["test"])])
			])
		], ">>> test\n> test\n>>> test");
	}

	[TestMethod]
	public void TestParseFn()
	{
		// General fn handling
		AssertEquals("$[test test]", [new MfmFnNode("test", null, ["test"])]);
		AssertEquals("$[test123 test]", [new MfmFnNode("test123", null, ["test"])]);
		AssertEquals("$[test.a test]",
		             [new MfmFnNode("test", new() { ["a"] = null }, ["test"])]);
		AssertEquals("$[test.a=b test]",
		             [new MfmFnNode("test", new() { ["a"] = "b" }, ["test"])]);
		AssertEquals("$[test.a=b,c=e test]",
		             [new MfmFnNode("test", new() { ["a"] = "b", ["c"] = "e" }, ["test"])]);
		AssertEquals("$[test.a,c=e test]",
		             [new MfmFnNode("test", new() { ["a"] = null, ["c"] = "e" }, ["test"])]);
		AssertEquals("$[test.a=b,c test]",
		             [new MfmFnNode("test", new() { ["a"] = "b", ["c"] = null }, ["test"])]);

		// False positives
		AssertEquals("$", ["$"]);
		AssertEquals("$[]", ["$[]"]);
		AssertEquals("$[test]", ["$[test]"]);
		AssertEquals("$[test ]", ["$[test ]"]);

		// invalid dot
		AssertEquals("$[.test test]", ["$[.test test]"]);
		AssertEquals("$[test. test]", ["$[test. test]"]);
		AssertEquals("$[test.a. test]", ["$[test.a. test]"]);
		AssertEquals("$[test.a.a test]", ["$[test.a.a test]"]);

		// invalid comma
		AssertEquals("$[,test test]", ["$[,test test]"]);
		AssertEquals("$[test, test]", ["$[test, test]"]);

		// invalid equals
		AssertEquals("$[=test test]", ["$[=test test]"]);
		AssertEquals("$[test= test]", ["$[test= test]"]);
		AssertEquals("$[test=a test]", ["$[test=a test]"]);
		AssertEquals("$[test.a= test]", ["$[test.a= test]"]);
		AssertEquals("$[test.a=b= test]", ["$[test.a=b= test]"]);
		AssertEquals("$[test.a=b=c test]", ["$[test.a=b=c test]"]);

		// Whitespace handling
		AssertEquals("test $[test test] test", ["test ", new MfmFnNode("test", null, ["test"]), " test"]);
		AssertEquals("test $[test test]", ["test ", new MfmFnNode("test", null, ["test"])]);
		AssertEquals("$[test test] test", [new MfmFnNode("test", null, ["test"]), " test"]);

		// Newline handling
		AssertEquals("$[test te\nst]", [new MfmFnNode("test", null, ["te\nst"])]);
		AssertEquals("$[test\n test]", ["$[test\n test]"]);
		AssertEquals("$[test.\na=b test]", ["$[test.\na=b test]"]);
		AssertEquals("$[test.a\n=b test]", ["$[test.a\n=b test]"]);
		AssertEquals("$[test.a=\nb test]", ["$[test.a=\nb test]"]);
		AssertEquals("$[test.a=b\n test]", ["$[test.a=b\n test]"]);
		AssertEquals("$[test.a=b \ntest]", [new MfmFnNode("test", new() { ["a"] = "b" }, ["\n test"])]);

		// Nesting
		AssertEquals("$[test1 $[test2 inner]]",
		             [new MfmFnNode("test1", null, [new MfmFnNode("test2", null, ["inner"])])]);
		AssertEquals("$[test1 $[test2 $[test3 inner]]]",
		[
			new MfmFnNode("test1", null,
			[ //
				new MfmFnNode("test2", null,
				[ //
					new MfmFnNode("test3", null, ["inner"])
				])
			])
		]);

		AssertEquals("$[scale.x=10,y=10 $[scale.x=10,y=110 $[scale.x=10,y=10 hi]]]", [
			new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
			[
				new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "110" },
				              [new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" }, ["hi"])])
			])
		]);

		// MFM art (by https://github.com/ChaoticLeah)
		AssertEquals(
		             """
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
		             """,
		             [
			             new MfmCenterNode([
				             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
				             [
					             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "110" },
					             [
						             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
						             [ //
							             "\u2b1b"
						             ])
					             ])
				             ]),
				             "\n",
				             new MfmFnNode("position", new() { ["y"] = "9" },
				             [
					             new MfmTextNode($":neocat:{new string(' ', 50)}:neocat_aww:")
				             ]),
				             "\n",
				             new MfmFnNode("position", new() { ["y"] = "4.3" },
				             [
					             new MfmFnNode("border", new() { ["radius"] = "20" },
					             [ //
						             new MfmTextNode(new string(' ', 71))
					             ]),
					             "\n"
				             ]),
				             "\n",
				             // @formatter:off
				             new MfmFnNode("followmouse", new() { ["x"] = null },
				             [
					             new MfmFnNode("position", new() { ["y"] = ".8" },
					             [
						             new MfmFnNode("scale", new() { ["x"] = "5", ["y"] = "5" },
						             [
							             " ",
							             new MfmFnNode("scale", new() { ["x"] = "0.5", ["y"] = "0.5" },
							             [
								             " \n\u26aa",
								             new MfmFnNode("position", new() { ["x"] = "-16.5" },
								             [
									             new MfmFnNode("scale", new() { ["x"] = "10" },
									             [
										             new MfmFnNode("scale", new() { ["x"] = "10" },
										             [
											             "\u2b1b⚪"
										             ])										             
									             ])
								             ]),
								             new MfmFnNode("position", new() { ["x"] = "16.5", ["y"] = "-1.3" },
								             [
									             new MfmFnNode("scale", new() { ["x"] = "10" },
									             [
										             new MfmFnNode("scale", new() { ["x"] = "10" },
										             [
											             "\u2b1b"
										             ])
									             ])
								             ])
							             ])
						             ])
					             ]),
					             "\n"
				             ]),
				             // @formatter:on
				             "\n\n\n",
				             new MfmFnNode("position", new() { ["x"] = "-88" },
				             [
					             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
					             [
						             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "110" },
						             [
							             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
							             [ //
								             "\u2b1b"
							             ])
						             ])
					             ])
				             ]),
				             "\n",
				             new MfmFnNode("position", new() { ["x"] = "88" },
				             [
					             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
					             [
						             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "110" },
						             [
							             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
							             [ //
								             "\u2b1b"
							             ])
						             ])
					             ])
				             ]),
				             "\n\n",
				             new MfmFnNode("position", new() { ["y"] = "-10" },
				             [ //
					             "Neocat Awwww Slider"
				             ])
			             ])
		             ]);
	}

	[TestMethod]
	public void TestFnRecursionLimit()
	{
		const int iterations = 50;
		const int limit      = 20;

		AssertEquals(GetMfm(iterations), [GetExpected(iterations)]);
		return;

		string GetMfm(int count)
		{
			var sb = new StringBuilder();
			for (var i = 0; i < count; i++)
				sb.Append("$[test ");
			for (var i = 0; i < count; i++)
				sb.Append(']');

			return sb.ToString();
		}

		MfmInlineNode GetExpected(int count, int remaining = limit)
		{
			if (remaining <= 0)
				return new MfmTextNode(GetMfm(count));

			return new MfmFnNode("test", null, [GetExpected(--count, --remaining)]);
		}
	}

	#region MfmExamples

	public static IEnumerable<object[]> ExamplePayloads
		=> typeof(MfmExamples).GetMethods(BindingFlags.Static | BindingFlags.Public).Select(p => (string[]) [p.Name]);

	public static string GetExampleName(MethodInfo _, object[] name) => $"CanParse{name[0]}";

	[TestMethod]
	[DynamicData(nameof(ExamplePayloads), DynamicDataDisplayName = nameof(GetExampleName))]
	public void CanParseExamplePayloads(string name)
		=> MfmParser.Parse((string)typeof(MfmExamples).GetMethod(name)!.Invoke(null, [])!);

	#endregion MfmExamples
}
