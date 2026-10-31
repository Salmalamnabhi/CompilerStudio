using CompilerStudio.Cli;

namespace CompilerStudio.Cli.Tests;

public class CliAppTests
{
    private static (int Code, string Out, string Err) RunWithInput(string stdin, params string[] args)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        int code = CliApp.Run(args, new StringReader(stdin), o, e);
        return (code, o.ToString(), e.ToString());
    }

    private static (int Code, string Out, string Err) Run(params string[] args) => RunWithInput("", args);

    private static string Example(string name) => Path.Combine(AppContext.BaseDirectory, "examples", name);

    [Fact]
    public void Help_prints_usage_and_succeeds()
    {
        var (code, output, _) = Run("--help");

        Assert.Equal(CliApp.Success, code);
        Assert.Contains("Usage: cstudio", output);
    }

    [Fact]
    public void No_arguments_is_a_usage_error()
    {
        Assert.Equal(CliApp.UsageError, Run().Code);
    }

    [Theory]
    [InlineData("run", "x.src")]
    [InlineData("compile")]
    [InlineData("compile", "a.src", "b.src")]
    [InlineData("compile", "a.src", "--verbose")]
    public void Bad_arguments_are_usage_errors(params string[] args)
    {
        var (code, _, err) = Run(args);

        Assert.Equal(CliApp.UsageError, code);
        Assert.StartsWith("cstudio: ", err);
    }

    [Fact]
    public void Missing_file_is_a_usage_error()
    {
        var (code, _, err) = Run("compile", "does_not_exist.src");

        Assert.Equal(CliApp.UsageError, code);
        Assert.Contains("cannot read 'does_not_exist.src'", err);
    }

    [Theory]
    [InlineData("arithmetic.src")]
    [InlineData("if_else.src")]
    [InlineData("while_loop.src")]
    public void Valid_examples_compile(string name)
    {
        var (code, output, err) = Run("compile", Example(name));

        Assert.Equal(CliApp.Success, code);
        Assert.Equal("", err);
        Assert.StartsWith("(0)", output);
    }

    [Fact]
    public void Compile_prints_quadruples()
    {
        var (_, output, _) = Run("compile", Example("arithmetic.src"));

        Assert.Equal(new[]
        {
            "(0)   ( = , 10 , null , $x$ )",
            "(1)   ( + , $x$ , 5 , T1 )",
            "(2)   ( = , T1 , null , $y$ )",
        }, output.TrimEnd().Split(Environment.NewLine));
    }

    [Fact]
    public void Lex_prints_one_token_per_line()
    {
        var (code, output, _) = RunWithInput("int $x$;", "lex", "-");

        Assert.Equal(CliApp.Success, code);
        var lines = output.TrimEnd().Split(Environment.NewLine);
        Assert.Equal(3, lines.Length);
        Assert.Equal("   1:1    keyword        int", lines[0]);
        Assert.Equal("   1:5    identifier     $x$", lines[1]);
    }

    [Fact]
    public void Parse_prints_productions()
    {
        var (code, output, _) = RunWithInput("int $x$;\n$x$ = 1;", "parse", "-");

        Assert.Equal(CliApp.Success, code);
        Assert.StartsWith("⟨program⟩", output);
    }

    [Fact]
    public void Errors_go_to_stderr_with_location_and_caret()
    {
        var (code, _, err) = Run("compile", Example("errors.src"));

        Assert.Equal(CliApp.CompileError, code);
        var lines = err.Split(Environment.NewLine);
        Assert.EndsWith("errors.src:2:1: syntax error: expected [Semicolon], found [Identifier] \"$x$\"", lines[0]);
        Assert.Equal("  $x$ = 1 @ 2;", lines[1]);
        Assert.Equal("  ^", lines[2]);
    }

    [Fact]
    public void Symbols_option_prints_tables()
    {
        var (_, output, _) = Run("compile", Example("arithmetic.src"), "--symbols");

        Assert.Contains("Identifiers:", output);
        Assert.Contains("$y$          int    15", output);
        Assert.Contains("Constants: 10, 5", output);
        Assert.Contains("Temps:     T1", output);
    }
}
