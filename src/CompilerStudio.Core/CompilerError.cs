namespace CompilerStudio.Core;

public class CompilerError
{
    public string Phase      { get; init; } = "";
    public int    Line       { get; init; }
    public int    Col        { get; init; }
    public string Message    { get; init; } = "";
    public string SourceLine { get; init; } = "";

    public string Header        => $"{Phase} error at line {Line}, column {Col}:  {Message}";
    public string SourceContext => "  " + SourceLine;
    public string Caret         => "  " + new string(' ', Math.Max(0, Col - 1)) + "^";
}
