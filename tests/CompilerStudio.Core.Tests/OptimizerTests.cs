using CompilerStudio.Core;

namespace CompilerStudio.Core.Tests;

public class OptimizerTests
{
    private static List<Quadruple> Compile(string src)
    {
        var r = Compiler.Translate(src);
        Assert.True(r.Succeeded, string.Join("\n", r.Errors.Select(e => e.Header)));
        return r.Quadruples.All().ToList();
    }

    private static string[] Optimized(string src) =>
        Optimizer.Optimize(Compile(src)).Select(q => q.ToString()).ToArray();

    [Fact]
    public void Folds_and_propagates_constants()
    {
        Assert.Equal(new[]
        {
            "( = , 10 , null , $x$ )",
            "( = , 15 , null , $y$ )",
        }, Optimized("int $x$, $y$;\n$x$ = 10;\n$y$ = $x$ + 5;"));
    }

    [Fact]
    public void Removes_the_branch_that_can_never_run()
    {
        Assert.Equal(new[]
        {
            "( = , 3 , null , $a$ )",
            "( = , 1 , null , $b$ )",
        }, Optimized("int $a$, $b$;\n$a$ = 3;\nif ($a$ < 5) then $b$ = 1; else $b$ = 2;"));
    }

    [Fact]
    public void Merges_temps_into_assignments_and_retargets_jumps()
    {
        Assert.Equal(new[]
        {
            "( = , 0 , null , $i$ )",
            "( = , 0 , null , $sum$ )",
            "( < , $i$ , 5 , T1 )",
            "( jnz , T1 , null , 5 )",
            "( j , null , null , 9 )",
            "( * , $i$ , 2 , T2 )",
            "( + , $sum$ , T2 , $sum$ )",
            "( + , $i$ , 1 , $i$ )",
            "( j , null , null , 2 )",
        }, Optimized("int $i$, $sum$;\n$i$ = 0;\n$sum$ = 0;\nwhile ($i$ < 5) do begin $sum$ = $sum$ + $i$ * 2; $i$ = $i$ + 1; end;"));
    }

    [Fact]
    public void Does_not_propagate_values_into_a_loop()
    {
        // $i$ is 0 before the loop but changes inside it, so the condition must stay
        var quads = Optimized("int $i$;\n$i$ = 0;\nwhile ($i$ < 3) do $i$ = $i$ + 1;");

        Assert.Contains("( < , $i$ , 3 , T1 )", quads);
    }

    [Fact]
    public void Leaves_the_input_unchanged()
    {
        var code = Compile("int $x$;\n$x$ = 1 + 2;");
        var before = code.Select(q => q.ToString()).ToList();

        Optimizer.Optimize(code);

        Assert.Equal(before, code.Select(q => q.ToString()));
    }

    public static TheoryData<string> Programs => new()
    {
        "int $x$, $y$;\n$x$ = 10;\n$y$ = $x$ + 5;",
        "int $x$;\n$x$ = (2 + 3) * 4 + 1;",
        "int $a$, $b$;\n$a$ = 3;\nif ($a$ < 5) then $b$ = 1; else $b$ = 2;",
        "int $a$, $b$;\n$a$ = 9;\nif ($a$ == 9) then begin $b$ = $a$ * 2; $a$ = 0; end; else $b$ = 2;",
        "int $a$, $b$;\n$a$ = 9;\nif ($a$ != 9) then $b$ = 1; else begin $b$ = $a$ + $a$; end;",
        "int $i$, $sum$;\n$i$ = 0;\n$sum$ = 0;\nwhile ($i$ < 5) do begin $sum$ = $sum$ + $i$ * 2; $i$ = $i$ + 1; end;",
        "int $i$;\n$i$ = 10;\nwhile ($i$ < 3) do $i$ = $i$ + 1;",
        "int $i$, $j$, $n$;\n$i$ = 0;\n$n$ = 0;\nwhile ($i$ < 3) do begin $j$ = 0; while ($j$ < 4) do begin $n$ = $n$ + 1; $j$ = $j$ + 1; end; $i$ = $i$ + 1; end;",
        "int $i$, $p$;\n$i$ = 1;\n$p$ = 1;\nwhile ($i$ <= 5) do begin if ($i$ >= 3) then $p$ = $p$ * $i$; else $p$ = $p$ + 0; $i$ = $i$ + 1; end;",
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public void Optimized_code_gives_the_same_results(string src)
    {
        var code = Compile(src);
        var optimized = Optimizer.Optimize(code);

        var expected = Interpreter.Run(code);
        var actual   = Interpreter.Run(optimized);

        Assert.True(expected.Succeeded, expected.Error);
        Assert.True(actual.Succeeded, actual.Error);
        Assert.Equal(expected.Variables.OrderBy(p => p.Key), actual.Variables.OrderBy(p => p.Key));
        Assert.True(optimized.Count <= code.Count);
        Assert.True(actual.Steps <= expected.Steps);
    }
}
