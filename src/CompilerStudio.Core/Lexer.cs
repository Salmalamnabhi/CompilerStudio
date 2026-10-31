using System.Text;
using static CompilerStudio.Core.TokenTypes;

namespace CompilerStudio.Core;

// Hand-written scanner. Tokens are produced on demand, so lexical errors are
// reported in the same order the parser meets them.
public class Lexer
{
    private readonly CompilationResult _r;
    private readonly string _src;
    private int _pos = 0, _line = 1, _col = 1;

    public Lexer(string source, CompilationResult result)
    {
        _src = source + EndOfInput;
        _r   = result;
    }

    private char Peek() => _pos < _src.Length ? _src[_pos] : '#';

    private char Advance()
    {
        char c = _src[_pos++];
        if (c == '\n') { _line++; _col = 1; } else _col++;
        return c;
    }

    public Token NextToken()
    {
        // skip whitespace
        while (Peek() != '#' && Peek() is ' ' or '\t' or '\r' or '\n')
            Advance();

        if (Peek() == '#') return new Token(EndOfInput, "#", _line, _col);

        int tL = _line, tC = _col;
        char c = Advance();

        Token result;

        // $identifier$
        if (c == '$')
        {
            var sb = new StringBuilder("$");
            while (Peek() != '#' && Peek() != '$' && Peek() != '\n')
                sb.Append(Advance());
            if (Peek() == '$') { sb.Append(Advance()); }
            else _r.AddError("Lexical", tL, tC, "unterminated identifier — missing closing '$'");
            string id = sb.ToString();
            _r.Identifiers.Add(id);
            result = new Token(Identifier, id, tL, tC);
        }
        // number
        else if (char.IsDigit(c))
        {
            var sb = new StringBuilder().Append(c);
            while (char.IsDigit(Peek())) sb.Append(Advance());
            string num = sb.ToString();
            _r.Constants.Add(num);
            result = new Token(Number, num, tL, tC);
        }
        // word → keyword or bare identifier error
        else if (char.IsLetter(c))
        {
            var sb = new StringBuilder().Append(c);
            while (char.IsLetterOrDigit(Peek()) || Peek() == '_') sb.Append(Advance());
            string word = sb.ToString();
            string? type = Keyword(word);
            if (type != null) result = new Token(type, word, tL, tC);
            else
            {
                _r.AddError("Lexical", tL, tC, $"bare identifier '{word}' — use $name$ syntax");
                result = new Token(Error, word, tL, tC);
            }
        }
        // operators & delimiters
        else
        {
            result = c switch
            {
                ';' => new Token(Semicolon, ";",  tL, tC),
                ',' => new Token(Comma,     ",",  tL, tC),
                '(' => new Token(LParen,    "(",  tL, tC),
                ')' => new Token(RParen,    ")",  tL, tC),
                '+' => new Token(Plus,      "+",  tL, tC),
                '*' => new Token(Times,     "*",  tL, tC),
                '=' => Peek() == '='
                        ? new Token(RelOp,  Also("=="), tL, tC)
                        : new Token(Assign, "=",        tL, tC),
                ':' => Peek() == '='
                        ? new Token(Assign, Also(":="), tL, tC)
                        : ErrTok(tL, tC, "unexpected ':'"),
                '<' => Peek() == '='
                        ? new Token(RelOp,  Also("<="), tL, tC)
                        : new Token(RelOp,  "<",        tL, tC),
                '>' => Peek() == '='
                        ? new Token(RelOp,  Also(">="), tL, tC)
                        : new Token(RelOp,  ">",        tL, tC),
                '!' => Peek() == '='
                        ? new Token(RelOp,  Also("!="), tL, tC)
                        : ErrTok(tL, tC, "expected '!='"),
                _   => ErrTok(tL, tC, $"unexpected character '{c}'")
            };
        }

        // Collect token for the Tokens view (skip error tokens and EOF)
        if (result.Type != EndOfInput && result.Type != Error)
            _r.Tokens.Add(result);

        return result;
    }

    private string Also(string s) { Advance(); return s; }

    private Token ErrTok(int l, int c, string msg)
    {
        _r.AddError("Lexical", l, c, msg);
        return new Token(Error, "?", l, c);
    }
}
