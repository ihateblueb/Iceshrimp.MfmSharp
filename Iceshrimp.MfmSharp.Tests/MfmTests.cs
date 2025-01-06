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

	private static void AssertEquals(string input, List<IMfmNode> expected, string? canonical = null)
	{
		var res = MfmParser.Parse(input);
		res.Should().Equal(expected, MfmNodeEqual);
		res.Serialize().Should().BeEquivalentTo(canonical ?? input);
	}

	[TestMethod]
	public void TestParseText()
	{
		var input = new string('a', 100_000);
		AssertEquals(input, [new MfmTextNode(input)]);
	}

	[TestMethod]
	public void TestParseBoundaryChars() => AssertEquals("test:", ["test:".ToMfm()]);

	[TestMethod]
	public void TestParseEmptyTag()
		=> AssertEquals("<b></b>", [new MfmBoldNode([], MfmBoldNode.DelimiterType.HtmlTag)]);

	[TestMethod]
	public void TestParseEmptyNestedTag()
		=> AssertEquals("<b><b></b></b>",
		[
			new MfmBoldNode([new MfmBoldNode([], MfmBoldNode.DelimiterType.HtmlTag)],
			                MfmBoldNode.DelimiterType.HtmlTag)
		]);

	[TestMethod]
	public void TestParseNestedTag()
	{
		AssertEquals("<b><b>a</b></b>",
		[
			new MfmBoldNode([new MfmBoldNode(["a".ToMfm()], MfmBoldNode.DelimiterType.HtmlTag)],
			                MfmBoldNode.DelimiterType.HtmlTag)
		]);

		AssertEquals("<b><b><b>a</b></b></b>",
		[
			new MfmBoldNode(
			[
				new MfmBoldNode(
				[
					new MfmBoldNode(
					[ //
						"a".ToMfm()
					], MfmBoldNode.DelimiterType.HtmlTag)
				], MfmBoldNode.DelimiterType.HtmlTag)
			], MfmBoldNode.DelimiterType.HtmlTag)
		]);

		AssertEquals("<b><b><b><b>a</b></b></b></b>",
		[
			new MfmBoldNode(
			[
				new MfmBoldNode(
				[
					new MfmBoldNode(
					[
						new MfmBoldNode(
						[ //
							"a".ToMfm()
						], MfmBoldNode.DelimiterType.HtmlTag)
					], MfmBoldNode.DelimiterType.HtmlTag)
				], MfmBoldNode.DelimiterType.HtmlTag)
			], MfmBoldNode.DelimiterType.HtmlTag)
		]);
	}

	[TestMethod]
	public void TestParseUnmatchedNestedTag()
	{
		AssertEquals("<b><b>a</b>", [new MfmBoldNode(["<b>a".ToMfm()], MfmBoldNode.DelimiterType.HtmlTag)]);
	}

	[TestMethod]
	public void TestParseItalic()
	{
		List<IMfmNode> expected =
		[
			"test ".ToMfm(),
			new MfmItalicNode(["test".ToMfm()], MfmItalicNode.DelimiterType.Asterisk),
			" test".ToMfm()
		];

		AssertEquals("test *test* test", expected);

		expected =
		[
			"test ".ToMfm(),
			new MfmItalicNode(["test".ToMfm()], MfmItalicNode.DelimiterType.Underscore),
			" test".ToMfm()
		];

		AssertEquals("test _test_ test", expected);

		expected = ["test *test\ntest* test".ToMfm()];
		AssertEquals("test *test\ntest* test", expected);

		expected = ["test _test\ntest_ test".ToMfm()];
		AssertEquals("test _test\ntest_ test", expected);

		expected =
		[
			"test ".ToMfm(),
			new MfmItalicNode(["test".ToMfm()], MfmItalicNode.DelimiterType.HtmlTag),
			" test".ToMfm()
		];

		AssertEquals("test <i>test</i> test", expected);

		expected =
		[
			"test ".ToMfm(),
			new MfmItalicNode(["test\ntest".ToMfm()], MfmItalicNode.DelimiterType.HtmlTag),
			" test".ToMfm()
		];

		AssertEquals("test <i>test\ntest</i> test", expected);

		// False positives
		AssertEquals("test*test*test", ["test*test*test".ToMfm()]);
		AssertEquals("test_test_test", ["test_test_test".ToMfm()]);

		AssertEquals("test*test* test", ["test*test* test".ToMfm()]);
		AssertEquals("test_test_ test", ["test_test_ test".ToMfm()]);

		AssertEquals("test *test*test", ["test *test*test".ToMfm()]);
		AssertEquals("test _test_test", ["test _test_test".ToMfm()]);

		AssertEquals("* test\n* test2\n* test3", ["* test\n* test2\n* test3".ToMfm()]);
	}

	[TestMethod]
	public void TestParseBold()
	{
		List<IMfmNode> expected =
		[
			"test ".ToMfm(), new MfmBoldNode(["test".ToMfm()], MfmBoldNode.DelimiterType.Asterisk), " test".ToMfm()
		];

		AssertEquals("test **test** test", expected);

		expected =
		[
			"test ".ToMfm(),
			new MfmBoldNode(["test".ToMfm()], MfmBoldNode.DelimiterType.Underscore),
			" test".ToMfm()
		];

		AssertEquals("test __test__ test", expected);

		expected = ["test **test\ntest** test".ToMfm()];
		AssertEquals("test **test\ntest** test", expected);

		expected = ["test __test\ntest__ test".ToMfm()];
		AssertEquals("test __test\ntest__ test", expected);

		expected =
		[
			"test ".ToMfm(), new MfmBoldNode(["test".ToMfm()], MfmBoldNode.DelimiterType.HtmlTag), " test".ToMfm()
		];

		AssertEquals("test <b>test</b> test", expected);

		expected =
		[
			"test ".ToMfm(),
			new MfmBoldNode(["test\ntest".ToMfm()], MfmBoldNode.DelimiterType.HtmlTag),
			" test".ToMfm()
		];

		AssertEquals("test <b>test\ntest</b> test", expected);

		// False positives
		AssertEquals("test**test**test", ["test**test**test".ToMfm()]);
		AssertEquals("test__test__test", ["test__test__test".ToMfm()]);

		AssertEquals("test**test** test", ["test**test** test".ToMfm()]);
		AssertEquals("test__test__ test", ["test__test__ test".ToMfm()]);

		AssertEquals("test **test**test", ["test **test**test".ToMfm()]);
		AssertEquals("test __test__test", ["test __test__test".ToMfm()]);
	}

	[TestMethod]
	public void TestParseBoldItalic()
	{
		// @formatter:off
		List<IMfmNode> expected =
		[
			new MfmItalicNode([
				"italic ".ToMfm(),
				new MfmBoldNode(["bold".ToMfm()], MfmBoldNode.DelimiterType.Asterisk),
				" italic".ToMfm()
			], MfmItalicNode.DelimiterType.Asterisk)
		];
		// @formatter:on

		AssertEquals("*italic **bold** italic*", expected);
	}

	[TestMethod]
	public void TestParseStrike()
	{
		List<IMfmNode> expected =
		[
			"test ".ToMfm(), new MfmStrikeNode(["test".ToMfm()], MfmStrikeNode.DelimiterType.Tilde), " test".ToMfm()
		];

		AssertEquals("test ~~test~~ test", expected);

		expected = ["test ~~test\ntest~~ test".ToMfm()];
		AssertEquals("test ~~test\ntest~~ test", expected);

		expected =
		[
			"test ".ToMfm(),
			new MfmStrikeNode(["test".ToMfm()], MfmStrikeNode.DelimiterType.HtmlTag),
			" test".ToMfm()
		];

		AssertEquals("test <s>test</s> test", expected);

		expected =
		[
			"test ".ToMfm(),
			new MfmStrikeNode(["test\ntest".ToMfm()], MfmStrikeNode.DelimiterType.HtmlTag),
			" test".ToMfm()
		];

		AssertEquals("test <s>test\ntest</s> test", expected);

		// False positives
		AssertEquals("test~~test~~test", ["test~~test~~test".ToMfm()]);
		AssertEquals("test~~test~~ test", ["test~~test~~ test".ToMfm()]);
		AssertEquals("test ~~test~~test", ["test ~~test~~test".ToMfm()]);
	}

	[TestMethod]
	public void TestParseHashtag()
	{
		// General hashtag handling
		AssertEquals("#test", [new MfmHashtagNode("test")]);
		AssertEquals("#test's", [new MfmHashtagNode("test"), "'s".ToMfm()]);
		AssertEquals("#t-e_s-t.", [new MfmHashtagNode("t-e_s-t"), ".".ToMfm()]);

		// False positives
		AssertEquals("#", ["#".ToMfm()]);
		AssertEquals("##", ["##".ToMfm()]);
		AssertEquals("test # test", ["test # test".ToMfm()]);

		// Whitespace handling
		AssertEquals("#test test", [new MfmHashtagNode("test"), " test".ToMfm()]);
		AssertEquals("test #test", ["test ".ToMfm(), new MfmHashtagNode("test")]);
		AssertEquals("test #test test", ["test ".ToMfm(), new MfmHashtagNode("test"), " test".ToMfm()]);
	}

	[TestMethod]
	public void TestParseEmojiCode()
	{
		List<IMfmNode> expected =
		[
			new MfmEmojiCodeNode("test"),
			" test ".ToMfm(),
			new MfmEmojiCodeNode("test"),
			new MfmEmojiCodeNode("test"),
			" :".ToMfm(),
			new MfmEmojiCodeNode("test"),
			": :test*test: ".ToMfm(),
			new MfmEmojiCodeNode("test")
		];

		AssertEquals(":test: test :test::test: ::test:: :test*test: :test:", expected);
		AssertEquals(":test\ntest:", [":test\ntest:".ToMfm()]);
		AssertEquals(":test\n:", [":test\n:".ToMfm()]);
	}

	[TestMethod]
	public void TestParseInlineCode()
	{
		List<IMfmNode> expected =
		[
			new MfmInlineCodeNode("test"),
			" test ".ToMfm(),
			new MfmInlineCodeNode("test"),
			new MfmInlineCodeNode("test"),
			" `".ToMfm(),
			new MfmInlineCodeNode("test"),
			" ".ToMfm(),
			new MfmInlineCodeNode("test")
		];

		AssertEquals("`test` test `test``test` ``test` `test`", expected);
		AssertEquals("`test\ntest`", ["`test\ntest`".ToMfm()]);
		AssertEquals("`test\n`", ["`test\n`".ToMfm()]);
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
		AssertEquals("test ```lang\nhello\n```", ["test ```lang\nhello\n```".ToMfm()]);
		AssertEquals("test ```hello``` test", ["test ``".ToMfm(), new MfmInlineCodeNode("hello"), "`` test".ToMfm()]);
	}

	[TestMethod]
	public void TestParseCenter()
	{
		AssertEquals("<center>test</center>", [new MfmCenterNode(["test".ToMfm()])], "<center>\ntest\n</center>");
		AssertEquals("<center>test\ntest</center>", [new MfmCenterNode(["test\ntest".ToMfm()])],
		             "<center>\ntest\ntest\n</center>");

		AssertEquals("*<center>test</center>*",
		             [new MfmItalicNode(["<center>test</center>".ToMfm()], MfmItalicNode.DelimiterType.Asterisk)]);

		AssertEquals("test <center>test</center> test", ["test <center>test</center> test".ToMfm()]);
	}

	[TestMethod]
	public void TestParseInlineMath()
	{
		AssertEquals("\\(test\\)", [new MfmInlineMathNode("test")]);
		AssertEquals("\\(test\ntest\\)", ["\\(test\ntest\\)".ToMfm()]);
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
		AssertEquals("<small>test</small>", [new MfmSmallNode(["test".ToMfm()])]);
		AssertEquals("<small>test\ntest</small>", [new MfmSmallNode(["test\ntest".ToMfm()])]);

		List<IMfmNode> expected =
		[
			new MfmItalicNode([new MfmSmallNode(["test".ToMfm()])], MfmItalicNode.DelimiterType.Asterisk)
		];

		AssertEquals("*<small>test</small>*", expected);
	}

	[TestMethod]
	public void TestParsePlain()
	{
		AssertEquals("<plain>test</plain>", [new MfmPlainNode("test")]);
		AssertEquals("<plain>test\ntest</plain>", [new MfmPlainNode("test\ntest")]);

		List<IMfmNode> expected = [new MfmItalicNode([new MfmPlainNode("test")], MfmItalicNode.DelimiterType.Asterisk)];
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
		AssertEquals("https://example.org/te)st", [new MfmUrlNode("https://example.org/te", false), ")st".ToMfm()]);

		AssertEquals("(https://example.org)", ["(".ToMfm(), new MfmUrlNode("https://example.org/", false), ")".ToMfm()],
		             "(https://example.org/)");

		AssertEquals("(https://example.org/(asd))",
		             ["(".ToMfm(), new MfmUrlNode("https://example.org/(asd)", false), ")".ToMfm()]);
		AssertEquals("(https://example.org/((asd)))",
		             ["(".ToMfm(), new MfmUrlNode("https://example.org/((asd))", false), ")".ToMfm()]);
		AssertEquals("(https://example.org/((asd))",
		             ["(".ToMfm(), new MfmUrlNode("https://example.org/((asd))", false)]);

		// Newline handling
		AssertEquals("https://test.com/asd\nasd", [new MfmUrlNode("https://test.com/asd", false), "\nasd".ToMfm()]);

		// Whitespace handling
		AssertEquals("test http://example.org test",
		             ["test ".ToMfm(), new MfmUrlNode("http://example.org/", false), " test".ToMfm()],
		             "test http://example.org/ test");

		AssertEquals("http://example.org test", [new MfmUrlNode("http://example.org/", false), " test".ToMfm()],
		             "http://example.org/ test");

		AssertEquals("test http://example.org", ["test ".ToMfm(), new MfmUrlNode("http://example.org/", false)],
		             "test http://example.org/");

		// Urlencode handling
		AssertEquals("https://example.org/with%20space", [new MfmUrlNode("https://example.org/with%20space", false)]);
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
		AssertEquals("(<https://example.org>)",
		             ["(".ToMfm(), new MfmUrlNode("https://example.org/", true), ")".ToMfm()],
		             "(<https://example.org/>)");

		AssertEquals("(<https://example.org/(asd)>)",
		             ["(".ToMfm(), new MfmUrlNode("https://example.org/(asd)", true), ")".ToMfm()]);
		AssertEquals("(<https://example.org/((asd))>)",
		             ["(".ToMfm(), new MfmUrlNode("https://example.org/((asd))", true), ")".ToMfm()]);
		AssertEquals("(<https://example.org/((asd)>)",
		             ["(".ToMfm(), new MfmUrlNode("https://example.org/((asd)", true), ")".ToMfm()]);

		// Newline handling
		AssertEquals("<https://test.com/asd\nasd>",
		             ["<".ToMfm(), new MfmUrlNode("https://test.com/asd", false), "\nasd>".ToMfm()]);

		// Whitespace handling
		AssertEquals("test <http://example.org> test",
		             ["test ".ToMfm(), new MfmUrlNode("http://example.org/", true), " test".ToMfm()],
		             "test <http://example.org/> test");

		AssertEquals("<http://example.org> test", [new MfmUrlNode("http://example.org/", true), " test".ToMfm()],
		             "<http://example.org/> test");

		AssertEquals("test <http://example.org>", ["test ".ToMfm(), new MfmUrlNode("http://example.org/", true)],
		             "test <http://example.org/>");

		// Urlencode handling
		AssertEquals("<https://example.org/with%20space>", [new MfmUrlNode("https://example.org/with%20space", true)]);
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
		AssertEquals("[test]https://example.org/a)",
		             ["[test]".ToMfm(), new MfmUrlNode("https://example.org/a", false), ")".ToMfm()]);

		AssertEquals("[test](https://example.org/a_(test)",
		             ["[test](".ToMfm(), new MfmUrlNode("https://example.org/a_(test)", false)]);

		AssertEquals("([test](https://example.org))",
		             ["(".ToMfm(), new MfmLinkNode("https://example.org/", "test", false), ")".ToMfm()],
		             "([test](https://example.org/))");

		AssertEquals("([test](https://example.org/(asd)))",
		             ["(".ToMfm(), new MfmLinkNode("https://example.org/(asd)", "test", false), ")".ToMfm()]);

		AssertEquals("([test](https://example.org/((asd))))",
		             ["(".ToMfm(), new MfmLinkNode("https://example.org/((asd))", "test", false), ")".ToMfm()]);

		AssertEquals("([test](https://example.org/((asd)))",
		             ["(".ToMfm(), new MfmLinkNode("https://example.org/((asd))", "test", false)]);

		// Newline handling
		AssertEquals("[test](https://test.com/asd\nasd)",
		             ["[test](".ToMfm(), new MfmUrlNode("https://test.com/asd", false), "\nasd)".ToMfm()]);

		AssertEquals("[test\ntest](https://test.com/asd)",
		             ["[test\ntest](".ToMfm(), new MfmUrlNode("https://test.com/asd", false), ")".ToMfm()]);

		// Whitespace handling
		AssertEquals("test [test](http://example.org) test",
		             ["test ".ToMfm(), new MfmLinkNode("http://example.org/", "test", false), " test".ToMfm()],
		             "test [test](http://example.org/) test");

		AssertEquals("[test](http://example.org) test",
		             [new MfmLinkNode("http://example.org/", "test", false), " test".ToMfm()],
		             "[test](http://example.org/) test");

		AssertEquals("test [test](http://example.org)",
		             ["test ".ToMfm(), new MfmLinkNode("http://example.org/", "test", false)],
		             "test [test](http://example.org/)");

		// Urlencode handling
		AssertEquals("[test](https://example.org/with%20space)",
		             [new MfmLinkNode("https://example.org/with%20space", "test", false)]);
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
		             ["(".ToMfm(), new MfmLinkNode("https://example.org/", "test", true), ")".ToMfm()],
		             "(?[test](https://example.org/))");

		AssertEquals("(?[test](https://example.org/(asd)))",
		             ["(".ToMfm(), new MfmLinkNode("https://example.org/(asd)", "test", true), ")".ToMfm()]);

		AssertEquals("(?[test](https://example.org/((asd))))",
		             ["(".ToMfm(), new MfmLinkNode("https://example.org/((asd))", "test", true), ")".ToMfm()]);

		AssertEquals("(?[test](https://example.org/((asd)))",
		             ["(".ToMfm(), new MfmLinkNode("https://example.org/((asd))", "test", true)]);

		// Newline handling
		AssertEquals("?[test](https://test.com/asd\nasd)",
		             ["?[test](".ToMfm(), new MfmUrlNode("https://test.com/asd", false), "\nasd)".ToMfm()]);

		AssertEquals("?[test\ntest](https://test.com/asd)",
		             ["?[test\ntest](".ToMfm(), new MfmUrlNode("https://test.com/asd", false), ")".ToMfm()]);

		// Whitespace handling
		AssertEquals("test ?[test](http://example.org) test",
		             ["test ".ToMfm(), new MfmLinkNode("http://example.org/", "test", true), " test".ToMfm()],
		             "test ?[test](http://example.org/) test");

		AssertEquals("?[test](http://example.org) test",
		             [new MfmLinkNode("http://example.org/", "test", true), " test".ToMfm()],
		             "?[test](http://example.org/) test");

		AssertEquals("test ?[test](http://example.org)",
		             ["test ".ToMfm(), new MfmLinkNode("http://example.org/", "test", true)],
		             "test ?[test](http://example.org/)");

		// Urlencode handling
		AssertEquals("?[test](https://example.org/with%20space)",
		             [new MfmLinkNode("https://example.org/with%20space", "test", true)]);
	}

	[TestMethod]
	public void TestParseMention()
	{
		// General mention handling
		AssertEquals("@t", [new MfmMentionNode("t", null)]);
		AssertEquals("@test", [new MfmMentionNode("test", null)]);
		AssertEquals("@test@instance.tld", [new MfmMentionNode("test", "instance.tld")]);
		AssertEquals("@test_", [new MfmMentionNode("test_", null)]);
		AssertEquals("@_test", [new MfmMentionNode("_test", null)]);
		AssertEquals("@test_@ins-tance.tld", [new MfmMentionNode("test_", "ins-tance.tld")]);
		AssertEquals("@_test@xn--mastodn-f1a.de", [new MfmMentionNode("_test", "xn--mastodn-f1a.de")]);
		AssertEquals("@_test@-xn--mastodn-f1a.de", ["@_test@-xn--mastodn-f1a.de".ToMfm()]);

		// False positives
		AssertEquals("@", ["@".ToMfm()]);
		AssertEquals("@@", ["@@".ToMfm()]);
		AssertEquals("@@test", ["@@test".ToMfm()]);
		AssertEquals("@test@", ["@test@".ToMfm()]);
		AssertEquals("test @ test", ["test @ test".ToMfm()]);
		AssertEquals("test @test@ test", ["test @test@ test".ToMfm()]);
		AssertEquals("@test:test.com", ["@test:test.com".ToMfm()]);

		// Trailing colon
		AssertEquals("@test:", [new MfmMentionNode("test", null), ":".ToMfm()]);
		AssertEquals("@test:\ntest", [new MfmMentionNode("test", null), ":\ntest".ToMfm()]);
		AssertEquals("@test@test.com:", [new MfmMentionNode("test", "test.com"), ":".ToMfm()]);
		AssertEquals("@test@test.com:\ntest", [new MfmMentionNode("test", "test.com"), ":\ntest".ToMfm()]);

		// Trailing dot
		AssertEquals("@test@asdf.com.", [new MfmMentionNode("test", "asdf.com"), ".".ToMfm()]);
		AssertEquals("@test.", [new MfmMentionNode("test", null), ".".ToMfm()]);

		// Trailing apostrophe
		AssertEquals("@test'", [new MfmMentionNode("test", null), "'".ToMfm()]);
		AssertEquals("@test@test.com'", [new MfmMentionNode("test", "test.com"), "'".ToMfm()]);

		// Trailing apostrophe followed by ascii
		AssertEquals("@test's", [new MfmMentionNode("test", null), "'s".ToMfm()]);
		AssertEquals("@test@test.com's", [new MfmMentionNode("test", "test.com"), "'s".ToMfm()]);

		// Trailing double quote
		AssertEquals("@test\"", [new MfmMentionNode("test", null), "\"".ToMfm()]);
		AssertEquals("@test@test.com\"", [new MfmMentionNode("test", "test.com"), "\"".ToMfm()]);

		// Whitespace handling
		AssertEquals("test @test", ["test ".ToMfm(), new MfmMentionNode("test", null)]);
		AssertEquals("@test test", [new MfmMentionNode("test", null), " test".ToMfm()]);
		AssertEquals("test @test test", ["test ".ToMfm(), new MfmMentionNode("test", null), " test".ToMfm()]);

		// Newline handling
		AssertEquals("@te\nst", [new MfmMentionNode("te", null), "\nst".ToMfm()]);
		AssertEquals("@test@instan\nce.tld", ["@test@instan\nce.tld".ToMfm()]);

		// Parenthesis handling
		AssertEquals("(@test@domain.tld)", ["(".ToMfm(), new MfmMentionNode("test", "domain.tld"), ")".ToMfm()]);
	}

	[TestMethod]
	public void TestParseQuote()
	{
		AssertEquals(">test", [new MfmQuoteNode(["test".ToMfm()])], "> test");
		AssertEquals("> test", [new MfmQuoteNode(["test".ToMfm()])]);
		AssertEquals("> test\n> test", [new MfmQuoteNode(["test".ToMfm(), "\n".ToMfm(), "test".ToMfm()])]);
		AssertEquals(">test\n>\n>test",
		             [
			             new MfmQuoteNode([
				             "test".ToMfm(), //
				             "\n".ToMfm(),
				             "\n".ToMfm(),
				             "test".ToMfm()
			             ])
		             ],
		             "> test\n> \n> test");
		AssertEquals(">\n>test", [">\n".ToMfm(), new MfmQuoteNode(["test".ToMfm()])], ">\n> test");
		AssertEquals(">test\n>", [new MfmQuoteNode(["test".ToMfm()]), ">".ToMfm()], "> test\n\n>");
		AssertEquals(">test\n", [new MfmQuoteNode(["test".ToMfm()])], "> test");
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
		List<IMfmNode> expected =
		[
			"this is plain text > this is not a quote >this is also not a quote\n".ToMfm(),
			new MfmQuoteNode([
				"this is a quote".ToMfm(),
				"\n".ToMfm(),
				"this is part of the same quote".ToMfm(),
				"\n".ToMfm(),
				"this too".ToMfm()
			]),
			"this is some plain text inbetween\n".ToMfm(),
			new MfmQuoteNode([
				"this is a second quote".ToMfm(),
				"\n".ToMfm(),
				"this is part of the second quote".ToMfm()
			]),
			new MfmQuoteNode(["this is a third quote".ToMfm()]),
			"and this is some plain text to close it off".ToMfm()
		];
		// @formatter:on

		AssertEquals(input, expected, canonical);
	}

	[TestMethod]
	public void TestParseNestedQuote()
	{
		// regular nestquote
		AssertEquals(">test\n>>test\n>test",
		             [
			             new MfmQuoteNode([
				             "test".ToMfm(),
				             new MfmQuoteNode([
					             "test".ToMfm() //
				             ]),
				             "test".ToMfm()
			             ])
		             ],
		             "> test\n>> test\n> test");

		// depth > 0
		AssertEquals(">>test", [
			             new MfmQuoteNode([
				             new MfmQuoteNode([
					             "test".ToMfm() //
				             ], true)
			             ])
		             ],
		             ">> test");

		// first line with depth > 0
		AssertEquals(">>test\n>test",
		             [
			             new MfmQuoteNode([
				             new MfmQuoteNode([
					             "test".ToMfm() //
				             ], true),
				             "test".ToMfm()
			             ])
		             ],
		             ">> test\n> test");

		// only lines with depth > 0
		AssertEquals(">>test\n>>test",
		             [
			             new MfmQuoteNode(
			             [
				             new MfmQuoteNode(
				             [
					             "test".ToMfm(), //
					             "\n".ToMfm(),
					             "test".ToMfm()
				             ])
			             ])
		             ],
		             ">> test\n>> test");

		// nesting limit - should be accepted
		AssertEquals(">>>>>test",
		[
			new MfmQuoteNode([
				new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode(["test".ToMfm()])])])])
			])
		], ">>>>> test");

		// nesting limit - should be rejected
		AssertEquals(">>>>>>test",
		[
			new MfmQuoteNode([
				new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode([new MfmQuoteNode([">test".ToMfm()])])])])
			])
		]);

		// nesting limit - should be rejected
		// @formatter:off
		AssertEquals(">test\n>>test\n>>>test\n>>>>test\n>>>>>test\n>>>>>>test",
		[
			new MfmQuoteNode([
				"test".ToMfm(),
				new MfmQuoteNode([
					"test".ToMfm(),
					new MfmQuoteNode([
						"test".ToMfm(),
						new MfmQuoteNode([
							"test".ToMfm(),
							new MfmQuoteNode([
								"test".ToMfm(),
								"\n".ToMfm(),
								">test".ToMfm()
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
				"test".ToMfm(),
				new MfmQuoteNode([
					"test".ToMfm(),
					new MfmQuoteNode([
						"test".ToMfm(),
						new MfmQuoteNode([
							"test".ToMfm(),
							new MfmQuoteNode([
								"test".ToMfm(),
								"\n".ToMfm(),
								">test".ToMfm(),
								"\n".ToMfm(),
								"test".ToMfm()
							]),
							"test".ToMfm()
						]),
						"test".ToMfm()
					]),
					"test".ToMfm()
				]),
				"test".ToMfm()
			])
		],
		"> test\n>> test\n>>> test\n>>>> test\n>>>>> test\n>>>>>>test\n>>>>> test\n>>>> test\n>>> test\n>> test\n> test");
		// @formatter:on

		// gaps in nesting sequence
		AssertEquals(">test\n>>>test\n>test",
		             [
			             new MfmQuoteNode(
			             [
				             "test".ToMfm(),
				             new MfmQuoteNode(
				             [
					             new MfmQuoteNode(
					             [ //
						             "test".ToMfm()
					             ])
				             ]),
				             "test".ToMfm()
			             ])
		             ],
		             "> test\n>>> test\n> test");

		// more gaps
		AssertEquals(">test\n>>>test\n>>>>>test\n>>>test\n>test",
		[
			new MfmQuoteNode(
			[ //
				"test".ToMfm(),
				new MfmQuoteNode(
				[ //
					new MfmQuoteNode(
					[ //
						"test".ToMfm(),
						new MfmQuoteNode(
						[ //
							new MfmQuoteNode(
							[ //
								"test".ToMfm()
							])
						]),
						"test".ToMfm()
					])
				]),
				"test".ToMfm()
			])
		], "> test\n>>> test\n>>>>> test\n>>> test\n> test");

		// gap at start
		AssertEquals(">>>test\n>test",
		             [ //
			             new MfmQuoteNode(
			             [ //
				             new MfmQuoteNode(
				             [ //
					             new MfmQuoteNode(
					             [ //
						             "test".ToMfm()
					             ])
				             ]),
				             "test".ToMfm()
			             ])
		             ],
		             ">>> test\n> test");

		// gaps at start and end
		AssertEquals(">>>test\n>test\n>>>test",
		[
			new MfmQuoteNode(
			[ //
				new MfmQuoteNode(
				[ //
					new MfmQuoteNode(
					[ //
						"test".ToMfm()
					])
				]),
				"test".ToMfm(),
				new MfmQuoteNode(
				[ //
					new MfmQuoteNode(
					[ //
						"test".ToMfm()
					])
				])
			])
		], ">>> test\n> test\n>>> test");
	}

	[TestMethod]
	public void TestParseFn()
	{
		// General fn handling
		AssertEquals("$[test test]", [new MfmFnNode("test", null, ["test".ToMfm()])]);
		AssertEquals("$[test123 test]", [new MfmFnNode("test123", null, ["test".ToMfm()])]);
		AssertEquals("$[test.a test]", [new MfmFnNode("test", new() { ["a"]   = null }, ["test".ToMfm()])]);
		AssertEquals("$[test.a=b test]", [new MfmFnNode("test", new() { ["a"] = "b" }, ["test".ToMfm()])]);
		AssertEquals("$[test.a=B test]", [new MfmFnNode("test", new() { ["a"] = "B" }, ["test".ToMfm()])]);
		AssertEquals("$[test.a=1 test]", [new MfmFnNode("test", new() { ["a"] = "1" }, ["test".ToMfm()])]);
		AssertEquals("$[test.a=b,c=e test]",
		[ //
			new MfmFnNode("test", new() { ["a"] = "b", ["c"] = "e" }, ["test".ToMfm()])
		]);
		AssertEquals("$[test.a,c=e test]",
		[ //
			new MfmFnNode("test", new() { ["a"] = null, ["c"] = "e" }, ["test".ToMfm()])
		]);
		AssertEquals("$[test.a=b,c test]",
		[ //
			new MfmFnNode("test", new() { ["a"] = "b", ["c"] = null }, ["test".ToMfm()])
		]);

		// False positives
		AssertEquals("$", ["$".ToMfm()]);
		AssertEquals("$[]", ["$[]".ToMfm()]);
		AssertEquals("$[test]", ["$[test]".ToMfm()]);
		AssertEquals("$[test ]", ["$[test ]".ToMfm()]);

		// invalid dot
		AssertEquals("$[.test test]", ["$[.test test]".ToMfm()]);
		AssertEquals("$[test. test]", ["$[test. test]".ToMfm()]);
		AssertEquals("$[test.a. test]", ["$[test.a. test]".ToMfm()]);
		AssertEquals("$[test.a.a test]", ["$[test.a.a test]".ToMfm()]);

		// invalid comma
		AssertEquals("$[,test test]", ["$[,test test]".ToMfm()]);
		AssertEquals("$[test, test]", ["$[test, test]".ToMfm()]);

		// invalid equals
		AssertEquals("$[=test test]", ["$[=test test]".ToMfm()]);
		AssertEquals("$[test= test]", ["$[test= test]".ToMfm()]);
		AssertEquals("$[test=a test]", ["$[test=a test]".ToMfm()]);
		AssertEquals("$[test.a= test]", ["$[test.a= test]".ToMfm()]);
		AssertEquals("$[test.a=b= test]", ["$[test.a=b= test]".ToMfm()]);
		AssertEquals("$[test.a=b=c test]", ["$[test.a=b=c test]".ToMfm()]);

		// url handling
		AssertEquals("$[media https://test.com/file.webp]",
		[ //
			new MfmFnNode("media", null, [new MfmUrlNode("https://test.com/file.webp", false)])
		]);

		// Whitespace handling
		AssertEquals("test $[test test] test",
		[ //
			"test ".ToMfm(), new MfmFnNode("test", null, ["test".ToMfm()]), " test".ToMfm()
		]);
		AssertEquals("test $[test test]", ["test ".ToMfm(), new MfmFnNode("test", null, ["test".ToMfm()])]);
		AssertEquals("$[test test] test", [new MfmFnNode("test", null, ["test".ToMfm()]), " test".ToMfm()]);

		// Newline handling
		AssertEquals("$[test te\nst]", [new MfmFnNode("test", null, ["te\nst".ToMfm()])]);
		AssertEquals("$[test\n test]", ["$[test\n test]".ToMfm()]);
		AssertEquals("$[test.\na=b test]", ["$[test.\na=b test]".ToMfm()]);
		AssertEquals("$[test.a\n=b test]", ["$[test.a\n=b test]".ToMfm()]);
		AssertEquals("$[test.a=\nb test]", ["$[test.a=\nb test]".ToMfm()]);
		AssertEquals("$[test.a=b\n test]", ["$[test.a=b\n test]".ToMfm()]);
		AssertEquals("$[test.a=b \ntest]", [new MfmFnNode("test", new() { ["a"] = "b" }, ["\ntest".ToMfm()])]);

		// Nesting
		AssertEquals("$[test1 $[test2 inner]]",
		[ //
			new MfmFnNode("test1", null, [new MfmFnNode("test2", null, ["inner".ToMfm()])])
		]);
		AssertEquals("$[test1 $[test2 $[test3 inner]]]",
		[
			new MfmFnNode("test1", null,
			[ //
				new MfmFnNode("test2", null,
				[ //
					new MfmFnNode("test3", null, ["inner".ToMfm()])
				])
			])
		]);
		AssertEquals("$[scale.x=10,y=10 $[scale.x=10,y=110 $[scale.x=10,y=10 hi]]]",
		[
			new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
			[
				new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "110" },
				              [new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" }, ["hi".ToMfm()])])
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
							             "\u2b1b".ToMfm()
						             ])
					             ])
				             ]),
				             "\n".ToMfm(),
				             new MfmFnNode("position", new() { ["y"] = "9" },
				             [
					             new MfmEmojiCodeNode("neocat"),
					             new string(' ', 50).ToMfm(),
					             new MfmEmojiCodeNode("neocat_aww")
				             ]),
				             "\n".ToMfm(),
				             new MfmFnNode("position", new() { ["y"] = "4.3" },
				             [
					             new MfmFnNode("border", new() { ["radius"] = "20" },
					             [ //
						             new MfmTextNode(new string(' ', 71))
					             ]),
					             "\n".ToMfm()
				             ]),
				             "\n".ToMfm(),
				             // @formatter:off
				             new MfmFnNode("followmouse", new() { ["x"] = null },
				             [
					             new MfmFnNode("position", new() { ["y"] = ".8" },
					             [
						             new MfmFnNode("scale", new() { ["x"] = "5", ["y"] = "5" },
						             [
							             " ".ToMfm(),
							             new MfmFnNode("scale", new() { ["x"] = "0.5", ["y"] = "0.5" },
							             [
								             "\n\u26aa".ToMfm(),
								             new MfmFnNode("position", new() { ["x"] = "-16.5" },
								             [
									             new MfmFnNode("scale", new() { ["x"] = "10" },
									             [
										             new MfmFnNode("scale", new() { ["x"] = "10" },
										             [
											             "\u2b1b".ToMfm()
										             ])										             
									             ])
								             ]),
								             new MfmFnNode("position", new() { ["x"] = "16.5", ["y"] = "-1.3" },
								             [
									             new MfmFnNode("scale", new() { ["x"] = "10" },
									             [
										             new MfmFnNode("scale", new() { ["x"] = "10" },
										             [
											             "\u2b1b".ToMfm()
										             ])
									             ])
								             ])
							             ])
						             ])
					             ])
				             ]),
				             // @formatter:on
				             "\n\n\n".ToMfm(),
				             new MfmFnNode("position", new() { ["x"] = "-88" },
				             [
					             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
					             [
						             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "110" },
						             [
							             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
							             [ //
								             "\u2b1b".ToMfm()
							             ])
						             ])
					             ])
				             ]),
				             "\n".ToMfm(),
				             new MfmFnNode("position", new() { ["x"] = "88" },
				             [
					             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
					             [
						             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "110" },
						             [
							             new MfmFnNode("scale", new() { ["x"] = "10", ["y"] = "10" },
							             [ //
								             "\u2b1b".ToMfm()
							             ])
						             ])
					             ])
				             ]),
				             "\n\n".ToMfm(),
				             new MfmFnNode("position", new() { ["y"] = "-10" },
				             [ //
					             "Neocat Awwww Slider".ToMfm()
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

		IMfmInlineNode GetExpected(int count, int remaining = limit + 1)
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

	private static bool MfmNodeEqual(IMfmNode a, IMfmNode b)
	{
		if (a.GetType() != b.GetType()) return false;

		if (!a.Children.SequenceEqual(b.Children, MfmNodeEquality.Instance))
			return false;

		switch (a)
		{
			case MfmTextNode textNode when ((MfmTextNode)b).Text != textNode.Text:
				return false;
			case MfmItalicNode ax:
			{
				var bx = (MfmItalicNode)b;
				if (bx.Type != ax.Type) return false;
				break;
			}
			case MfmBoldNode ax:
			{
				var bx = (MfmBoldNode)b;
				if (bx.Type != ax.Type) return false;
				break;
			}
			case MfmStrikeNode ax:
			{
				var bx = (MfmStrikeNode)b;
				if (bx.Type != ax.Type) return false;
				break;
			}
			case MfmMentionNode ax:
			{
				var bx = (MfmMentionNode)b;
				if (bx.User != ax.User) return false;
				if (bx.Host != ax.Host) return false;
				break;
			}
			case MfmCodeBlockNode ax:
			{
				var bx = (MfmCodeBlockNode)b;
				if (ax.Code != bx.Code) return false;
				if (ax.Lang != bx.Lang) return false;
				break;
			}
			case MfmInlineCodeNode ax:
			{
				var bx = (MfmInlineCodeNode)b;
				if (ax.Code != bx.Code) return false;
				break;
			}
			case MfmMathBlockNode ax:
			{
				var bx = (MfmMathBlockNode)b;
				if (ax.Formula != bx.Formula) return false;
				break;
			}
			case MfmInlineMathNode ax:
			{
				var bx = (MfmInlineMathNode)b;
				if (ax.Formula != bx.Formula) return false;
				break;
			}
			case MfmEmojiCodeNode ax:
			{
				var bx = (MfmEmojiCodeNode)b;
				if (ax.Name != bx.Name) return false;
				break;
			}
			case MfmHashtagNode ax:
			{
				var bx = (MfmHashtagNode)b;
				if (ax.Hashtag != bx.Hashtag) return false;
				break;
			}
			case MfmUrlNode ax:
			{
				var bx = (MfmUrlNode)b;
				if (ax.Url != bx.Url) return false;
				if (ax.Brackets != bx.Brackets) return false;
				break;
			}
			case MfmLinkNode ax:
			{
				var bx = (MfmLinkNode)b;
				if (ax.Url != bx.Url) return false;
				if (ax.Silent != bx.Silent) return false;
				break;
			}
			case MfmFnNode ax:
			{
				var bx = (MfmFnNode)b;
				if (ax.Name != bx.Name) return false;
				if ((ax.Args == null) != (bx.Args == null)) return false;
				if (ax.Args == null || bx.Args == null) return true;
				if (ax.Args.Count != bx.Args.Count) return false;
				// ReSharper disable once UsageOfDefaultStructEquality
				if (ax.Args.Except(bx.Args).Any()) return false;

				break;
			}
		}

		return true;
	}

	private class MfmNodeEquality : IEqualityComparer<IMfmNode>
	{
		public static readonly MfmNodeEquality Instance = new();

		public bool Equals(IMfmNode? x, IMfmNode? y)
		{
			if (x == null && y == null) return true;
			if (x == null && y != null) return false;
			if (x != null && y == null) return false;

			return MfmNodeEqual(x!, y!);
		}

		public int GetHashCode(IMfmNode obj)
		{
			return obj.GetHashCode();
		}
	}
}
