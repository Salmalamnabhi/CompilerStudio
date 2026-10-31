namespace CompilerStudio.Core;

public enum TokenType
{
    EndOfInput, Error,
    Identifier, Number,
    Int, If, Then, Else, While, Do, Begin, End,
    Semicolon, Comma, LParen, RParen,
    Plus, Times, Assign, RelOp
}

public record Token(TokenType Type, string Value, int Line, int Col);

public static class TokenInfo
{
    public static TokenType? Keyword(string word) => word switch
    {
        "if"    => TokenType.If,    "int"   => TokenType.Int,
        "then"  => TokenType.Then,  "do"    => TokenType.Do,
        "while" => TokenType.While, "begin" => TokenType.Begin,
        "end"   => TokenType.End,   "else"  => TokenType.Else,
        _       => null
    };

    // Category shown in the Tokens view
    public static string Category(TokenType type) => type switch
    {
        TokenType.Identifier                       => "identifier",
        TokenType.Number                           => "number",
        TokenType.Int or TokenType.If or TokenType.Then or TokenType.Else
            or TokenType.While or TokenType.Do
            or TokenType.Begin or TokenType.End    => "keyword",
        TokenType.Assign                           => "assign_op",
        TokenType.Plus or TokenType.Times          => "math_op",
        TokenType.RelOp                            => "relational_op",
        TokenType.Semicolon or TokenType.Comma     => "separator",
        TokenType.LParen or TokenType.RParen       => "bracket",
        _                                          => type.ToString()
    };
}
