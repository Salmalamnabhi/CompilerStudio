namespace CompilerStudio.Core;

// Entry points for each compiler phase. Every call starts from a clean
// result, and unexpected exceptions are reported as "Internal" errors.
public static class Compiler
{
    public static CompilationResult Lex(string source) => Run(source, (lexer, _) =>
    {
        // Drain the entire token stream
        while (lexer.NextToken().Type != TokenTypes.EndOfInput) { }
    });

    public static CompilationResult Parse(string source) => Run(source, (lexer, r) =>
        new Parser(lexer, r).ParseProgram());

    public static CompilationResult Translate(string source) => Run(source, (lexer, r) =>
        new Translator(lexer, r).TrProgram());

    private static CompilationResult Run(string source, Action<Lexer, CompilationResult> phase)
    {
        var result = new CompilationResult(source);
        try   { phase(new Lexer(source, result), result); }
        catch (Exception ex) { result.AddError("Internal", 0, 0, ex.Message); }
        return result;
    }
}
