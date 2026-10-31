using CompilerStudio.Core;

namespace CompilerStudio.Core.Tests;

public class InterpreterTests
{
    internal static ExecutionResult RunSource(string src)
    {
        var r = Compiler.Translate(src);
        Assert.True(r.Succeeded, string.Join("\n", r.Errors.Select(e => e.Header)));
        return Interpreter.Run(r.Quadruples.All());
    }

    private static Dictionary<string, int> Vars(ExecutionResult x) => x.Variables.ToDictionary(p => p.Key, p => p.Value);

    [Fact]
    public void Runs_straight_line_code()
    {
        var x = RunSource("int $x$, $y$;\n$x$ = 10;\n$y$ = $x$ + 5 * 2;");

        Assert.True(x.Succeeded);
        Assert.Equal(new Dictionary<string, int> { ["$x$"] = 10, ["$y$"] = 20 }, Vars(x));
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(7, 2)]
    public void Takes_the_right_if_branch(int a, int expectedB)
    {
        var x = RunSource($"int $a$, $b$;\n$a$ = {a};\nif ($a$ < 5) then $b$ = 1; else $b$ = 2;");

        Assert.Equal(expectedB, Vars(x)["$b$"]);
    }

    [Fact]
    public void Runs_while_loop_to_completion()
    {
        var x = RunSource("int $i$, $sum$;\n$i$ = 0;\n$sum$ = 0;\nwhile ($i$ < 5) do begin $sum$ = $sum$ + $i$ * 2; $i$ = $i$ + 1; end;");

        Assert.Equal(new Dictionary<string, int> { ["$i$"] = 5, ["$sum$"] = 20 }, Vars(x));
    }

    [Fact]
    public void Runs_nested_loops()
    {
        var x = RunSource(
            "int $i$, $j$, $n$;\n$i$ = 0;\n$n$ = 0;\n" +
            "while ($i$ < 3) do begin\n" +
            "  $j$ = 0;\n" +
            "  while ($j$ < 4) do begin $n$ = $n$ + 1; $j$ = $j$ + 1; end;\n" +
            "  $i$ = $i$ + 1;\n" +
            "end;");

        Assert.Equal(12, Vars(x)["$n$"]);
    }

    [Fact]
    public void Lists_only_program_variables_in_assignment_order()
    {
        var x = RunSource("int $b$, $a$;\n$a$ = 1 + 2;\n$b$ = $a$ * 2;");

        Assert.Equal(new[] { "$a$", "$b$" }, x.Variables.Select(p => p.Key));
    }

    [Fact]
    public void Stops_infinite_loops_at_the_step_limit()
    {
        var r = Compiler.Translate("int $x$;\n$x$ = 0;\nwhile (1 < 2) do $x$ = $x$ + 1;");

        var x = Interpreter.Run(r.Quadruples.All(), maxSteps: 1000);

        Assert.False(x.Succeeded);
        Assert.Contains("step limit of 1000", x.Error);
        Assert.Equal(1001, x.Steps);
    }

    [Fact]
    public void Reports_read_before_assignment()
    {
        var code = new[] { new Quadruple("+", "$x$", "1", "$y$") };

        var x = Interpreter.Run(code);

        Assert.Equal("'$x$' read before assignment at (0)", x.Error);
    }

    [Fact]
    public void Reports_bad_jump_target()
    {
        var code = new[] { new Quadruple("j", "null", "null", "?") };

        Assert.Equal("bad jump target '?' at (0)", Interpreter.Run(code).Error);
    }
}
