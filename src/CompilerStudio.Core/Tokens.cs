namespace CompilerStudio.Core;

public record Token(string Type, string Value, int Line, int Col);

// Token type names used by the lexer and parser. They are kept in one place
// so error messages and grammar checks share the same spelling.
public static class TokenTypes
{
    public const string EndOfInput = "#";
    public const string Error      = "error";

    public const string Identifier = "标识符";
    public const string Number     = "常量";
    public const string Int        = "变量说明";
    public const string If         = "保留字if";
    public const string Then       = "保留字then";
    public const string Else       = "保留字else";
    public const string While      = "保留字while";
    public const string Do         = "保留字do";
    public const string Begin      = "保留字begin";
    public const string End        = "保留字end";
    public const string Semicolon  = "分隔符分号";
    public const string Comma      = "分隔符逗号";
    public const string LParen     = "左括号";
    public const string RParen     = "右括号";
    public const string Plus       = "加法运算符";
    public const string Times      = "乘法运算符";
    public const string Assign     = "赋值号";
    public const string RelOp      = "逻辑运算符";

    public static string? Keyword(string word) => word switch
    {
        "if"    => If,    "int"   => Int,
        "then"  => Then,  "do"    => Do,
        "while" => While, "begin" => Begin,
        "end"   => End,   "else"  => Else,
        _       => null
    };

    // English category shown in the Tokens view
    public static string Category(string type) => type switch
    {
        Identifier                    => "identifier",
        Number                        => "number",
        Int                           => "keyword",
        If or Then or Else or While
            or Do or Begin or End     => "keyword",
        Assign                        => "assign_op",
        Plus or Times                 => "math_op",
        RelOp                         => "relational_op",
        Semicolon or Comma            => "separator",
        LParen or RParen              => "bracket",
        _                             => type
    };
}
