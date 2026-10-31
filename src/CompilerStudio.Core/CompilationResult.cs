namespace CompilerStudio.Core;

// Everything one compiler run produces. The lexer, parser and translator all
// write into the same result, so the UI (or a test) can inspect each phase.
public class CompilationResult
{
    public IdentifierTable     Identifiers { get; } = new();
    public ConstantTable       Constants   { get; } = new();
    public TempTable           Temps       { get; } = new();
    public QuadrupleTable      Quadruples  { get; } = new();
    public List<CompilerError> Errors      { get; } = new();
    public List<Token>         Tokens      { get; } = new();
    public List<string>        ParseLog    { get; } = new();

    public string[] SourceLines { get; }
    public bool     Succeeded   => Errors.Count == 0;

    public CompilationResult(string source) => SourceLines = source.Split('\n');

    public string GetSourceLine(int line)
    {
        if (line < 1 || line > SourceLines.Length) return "";
        return SourceLines[line - 1].TrimEnd('\r');
    }

    public void AddError(string phase, int line, int col, string msg)
        => Errors.Add(new CompilerError
            { Phase = phase, Line = line, Col = col, Message = msg, SourceLine = GetSourceLine(line) });
}
