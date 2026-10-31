using CompilerStudio.Core;
using static CompilerStudio.Core.TokenTypes;

namespace CompilerStudio.Core.Tests;

public class LexerTests
{
    private static string[] Types(CompilationResult r) => r.Tokens.Select(t => t.Type).ToArray();

    [Fact]
    public void Recognises_keywords_identifiers_numbers_and_delimiters()
    {
        var r = Compiler.Lex("int $x$, $y$;");

        Assert.True(r.Succeeded);
        Assert.Equal(new[] { Int, Identifier, Comma, Identifier, Semicolon }, Types(r));
        Assert.Equal(new[] { "int", "$x$", ",", "$y$", ";" }, r.Tokens.Select(t => t.Value));
    }

    [Theory]
    [InlineData("=",  Assign)]
    [InlineData(":=", Assign)]
    [InlineData("==", RelOp)]
    [InlineData("!=", RelOp)]
    [InlineData("<",  RelOp)]
    [InlineData("<=", RelOp)]
    [InlineData(">",  RelOp)]
    [InlineData(">=", RelOp)]
    [InlineData("+",  Plus)]
    [InlineData("*",  Times)]
    public void Recognises_operators(string op, string expectedType)
    {
        var r = Compiler.Lex(op);

        var tok = Assert.Single(r.Tokens);
        Assert.Equal(expectedType, tok.Type);
        Assert.Equal(op, tok.Value);
    }

    [Fact]
    public void Tracks_line_and_column()
    {
        var r = Compiler.Lex("int $a$;\n  $a$ = 7;");

        var seven = r.Tokens.Single(t => t.Value == "7");
        Assert.Equal(2, seven.Line);
        Assert.Equal(9, seven.Col);
    }

    [Fact]
    public void Fills_identifier_and_constant_tables_without_duplicates()
    {
        var r = Compiler.Lex("$a$ $b$ $a$ 1 2 1");

        Assert.Equal(new[] { "$a$", "$b$" }, r.Identifiers.All().Select(e => e.Name));
        Assert.Equal(new[] { "1", "2" }, r.Constants.All());
    }

    [Fact]
    public void Reports_bare_identifier()
    {
        var r = Compiler.Lex("int x;");

        var err = Assert.Single(r.Errors);
        Assert.Equal("Lexical", err.Phase);
        Assert.Equal(5, err.Col);
        Assert.Contains("bare identifier 'x'", err.Message);
        Assert.DoesNotContain(r.Tokens, t => t.Value == "x");
    }

    [Fact]
    public void Reports_unterminated_identifier()
    {
        var r = Compiler.Lex("int $x\n");

        var err = Assert.Single(r.Errors);
        Assert.Equal("Lexical", err.Phase);
        Assert.Contains("missing closing '$'", err.Message);
    }

    [Theory]
    [InlineData("@",  "unexpected character '@'")]
    [InlineData(":",  "unexpected ':'")]
    [InlineData("!",  "expected '!='")]
    public void Reports_bad_characters(string src, string message)
    {
        var r = Compiler.Lex(src);

        var err = Assert.Single(r.Errors);
        Assert.Equal(message, err.Message);
        Assert.Empty(r.Tokens);
    }

    [Fact]
    public void Error_caret_points_at_the_column()
    {
        var r = Compiler.Lex("int $a$ @");

        var err = Assert.Single(r.Errors);
        Assert.Equal("  int $a$ @", err.SourceContext);
        Assert.Equal("          ^", err.Caret);
    }
}
