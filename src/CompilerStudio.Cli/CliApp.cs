using CompilerStudio.Core;

namespace CompilerStudio.Cli;

// Command-line front end. Kept free of Console calls so tests can pass in
// their own readers and writers.
public static class CliApp
{
    public const int Success      = 0;
    public const int CompileError = 1;
    public const int UsageError   = 2;

    public const string Usage =
        """
        Usage: cstudio <command> <file|-> [--optimize] [--symbols]

        Commands:
          lex        print the token stream
          parse      check syntax and print the productions used
          compile    check semantics and print the quadruples
          run        compile, execute, and print the final variable values

        Options:
          --optimize optimize the quadruples (compile and run)
          --symbols  also print the identifier, constant and temp tables
          -          read the source from standard input

        Exit codes: 0 success, 1 compile or runtime errors, 2 usage or file errors
        """;

    public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var positional = args.Where(a => !a.StartsWith("--")).ToList();
        var flags      = args.Where(a =>  a.StartsWith("--")).ToList();

        if (flags.Contains("--help") || positional.Count == 0)
        {
            stdout.WriteLine(Usage);
            return positional.Count == 0 && !flags.Contains("--help") ? UsageError : Success;
        }

        var unknown = flags.Except(new[] { "--symbols", "--optimize" }).FirstOrDefault();
        if (unknown != null)       return Fail(stderr, $"unknown option '{unknown}'");
        if (positional.Count != 2) return Fail(stderr, "expected a command and a file");

        string command = positional[0], path = positional[1];
        Func<string, CompilationResult>? phase = command switch
        {
            "lex"     => Compiler.Lex,
            "parse"   => Compiler.Parse,
            "compile" => Compiler.Translate,
            "run"     => Compiler.Translate,
            _         => null
        };
        if (phase == null) return Fail(stderr, $"unknown command '{command}'");

        string source;
        try   { source = path == "-" ? stdin.ReadToEnd() : File.ReadAllText(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return Fail(stderr, $"cannot read '{path}': {ex.Message}"); }

        var r = phase(source);
        IReadOnlyList<Quadruple> code = flags.Contains("--optimize")
            ? Optimizer.Optimize(r.Quadruples.All())
            : r.Quadruples.All();
        ExecutionResult? run = command == "run" && r.Succeeded ? Interpreter.Run(code) : null;

        switch (command)
        {
            case "lex":
                foreach (var t in r.Tokens)
                    stdout.WriteLine($"{t.Line,4}:{t.Col,-4} {TokenInfo.Category(t.Type),-14} {t.Value}");
                break;
            case "parse":
                foreach (var l in r.ParseLog) stdout.WriteLine(l);
                break;
            case "compile":
                foreach (var l in QuadrupleTable.Format(code)) stdout.WriteLine(l);
                break;
            case "run" when run != null:
                foreach (var (name, value) in run.Variables) stdout.WriteLine($"{name} = {value}");
                break;
        }

        if (flags.Contains("--symbols")) WriteSymbols(r, stdout);

        foreach (var e in r.Errors)
        {
            stderr.WriteLine($"{path}:{e.Line}:{e.Col}: {e.Phase.ToLowerInvariant()} error: {e.Message}");
            if (e.Line > 0)
            {
                stderr.WriteLine(e.SourceContext);
                stderr.WriteLine(e.Caret);
            }
        }

        if (run is { Succeeded: false })
            stderr.WriteLine($"{path}: runtime error: {run.Error}");

        return r.Succeeded && (run is null or { Succeeded: true }) ? Success : CompileError;
    }

    private static void WriteSymbols(CompilationResult r, TextWriter w)
    {
        w.WriteLine();
        w.WriteLine("Identifiers:");
        foreach (var e in r.Identifiers.All())
            w.WriteLine($"  {e.Name,-12} {(e.Type == "" ? "-" : e.Type),-6} {(e.Value == "" ? "-" : e.Value)}");
        w.WriteLine($"Constants: {string.Join(", ", r.Constants.All())}");
        w.WriteLine($"Temps:     {string.Join(", ", r.Temps.All())}");
    }

    private static int Fail(TextWriter stderr, string msg)
    {
        stderr.WriteLine($"cstudio: {msg}");
        stderr.WriteLine("Run 'cstudio --help' for usage.");
        return UsageError;
    }
}
