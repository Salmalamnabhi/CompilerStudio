using CompilerStudio.Core;

namespace CompilerStudio.Core.Tests;

public class ParserTests
{
    [Theory]
    [InlineData("int $x$;\n$x$ = 1;")]
    [InlineData("int $x$, $y$;\n$x$ = 10;\n$y$ = $x$ + 5;\n")]
    [InlineData("int $x$;\n$x$ = (1 + 2) * 3;")]
    [InlineData("int $a$, $b$;\n$a$ = 3;\nif ($a$ < 5) then $b$ = 1; else $b$ = 2;")]
    [InlineData("int $i$;\n$i$ = 0;\nwhile ($i$ < 3) do begin $i$ = $i$ + 1; end;")]
    public void Accepts_valid_programs(string src)
    {
        var r = Compiler.Parse(src);

        Assert.True(r.Succeeded, string.Join("\n", r.Errors.Select(e => e.Header)));
    }

    [Fact]
    public void Logs_productions_in_order()
    {
        var r = Compiler.Parse("int $x$;\n$x$ = 1;");

        Assert.Equal("⟨program⟩  →  ⟨var_decl⟩ ; ⟨stmts⟩", r.ParseLog[0]);
        Assert.Equal("⟨var_decl⟩  →  int ⟨id_list⟩",       r.ParseLog[1]);
        Assert.Contains("⟨assign⟩  →  id = ⟨expr⟩", r.ParseLog);
        Assert.Equal("⟨stmts'⟩  →  ε", r.ParseLog[^1]);
    }

    [Fact]
    public void Reports_missing_semicolon_at_next_token()
    {
        var r = Compiler.Parse("int $x$\n$x$ = 1;");

        var err = r.Errors.First();
        Assert.Equal("Syntax", err.Phase);
        Assert.Equal(2, err.Line);
        Assert.Equal(1, err.Col);
        Assert.Contains($"expected [{TokenTypes.Semicolon}]", err.Message);
    }

    [Fact]
    public void Reports_missing_then()
    {
        var r = Compiler.Parse("int $a$;\n$a$ = 1;\nif ($a$ < 2) $a$ = 3; else $a$ = 4;");

        Assert.Contains(r.Errors, e => e.Phase == "Syntax" && e.Message.Contains($"[{TokenTypes.Then}]"));
    }
}
