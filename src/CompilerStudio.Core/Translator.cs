using static CompilerStudio.Core.TokenTypes;

namespace CompilerStudio.Core;

// Syntax-directed translation: checks declarations and types, and emits
// quadruples (op, arg1, arg2, result) as it parses.
public class Translator : RecursiveDescentBase
{
    private IdentifierTable _ids  => _r.Identifiers;
    private TempTable       _temps => _r.Temps;
    private QuadrupleTable  _ir   => _r.Quadruples;

    public Translator(Lexer lexer, CompilationResult result) : base(lexer, result) { }

    private void SemanticError(string msg) => _r.AddError("Semantic", _tok.Line, _tok.Col, msg);
    private void SyntaxError(string msg)   => _r.AddError("Syntax",   _tok.Line, _tok.Col, msg);

    public bool TrProgram()
    {
        if (!TrVarDecl())    { SemanticError("var declaration failed"); return false; }
        if (!Match(Semicolon)) return false;
        if (!TrStmts())      { SemanticError("statement section failed"); return false; }
        return true;
    }
    private bool TrVarDecl()    { if (!Match(Int)) return false; return TrIdList("int"); }
    private bool TrIdList(string t)
    {
        if (_tok.Type != Identifier) { SyntaxError("expected identifier"); return false; }
        string n = _tok.Value; string ex = _ids.GetType(n);
        if (ex != "" && ex != t) { SemanticError($"type conflict for '{n}'"); return false; }
        if (ex == "") { _ids.SetType(n, t); Match(Identifier); }
        return TrIdListP(t);
    }
    private bool TrIdListP(string t)
    {
        if (_tok.Type == Comma)
        {
            Match(Comma);
            if (_tok.Type != Identifier) { SyntaxError("expected identifier after ','"); return false; }
            string n = _tok.Value; string ex = _ids.GetType(n);
            if (ex != "" && ex != t) { SemanticError($"type conflict for '{n}'"); return false; }
            if (ex == "") { _ids.SetType(n, t); Match(Identifier); }
            return TrIdListP(t);
        }
        return true;
    }
    private bool TrStmts()
    {
        if (!TrStmt())          return false;
        if (!Match(Semicolon))  return false;
        return TrStmtsP();
    }
    private bool TrStmtsP()
    {
        if (_tok.Type is Identifier or If or While)
        { if (!TrStmt()) return false; if (!Match(Semicolon)) return false; return TrStmtsP(); }
        return true;
    }
    private bool TrStmt()
    {
        if      (_tok.Type == Identifier) return TrAssign();
        else if (_tok.Type == If)         return TrIf();
        else if (_tok.Type == While)      return TrWhile();
        SyntaxError($"unexpected token [{_tok.Type}] \"{_tok.Value}\"");
        return false;
    }
    private bool TrAssign()
    {
        if (!_ids.Exists(_tok.Value)) { SemanticError($"undeclared identifier '{_tok.Value}'"); return false; }
        string tgt = _tok.Value; Match(Identifier);
        if (!Match(Assign)) return false;
        var e = TrExpr(); if (e == null) return false;
        _ir.Add("=", e.Name, "null", tgt);
        _ids.SetVal(tgt, e.Value);
        return true;
    }
    private IdentifierEntry? TrExpr()
    {
        var t = TrTerm(); if (t == null) return null; return TrExprP(t);
    }
    private IdentifierEntry? TrExprP(IdentifierEntry left)
    {
        if (_tok.Type != Plus) return left;
        Match(Plus);
        var right = TrTerm(); if (right == null) return null;
        var tmp = _temps.Create();
        _ir.Add("+", left.Name, right.Name, tmp.Name);
        tmp.Type = "int";
        if (int.TryParse(left.Value, out int lv) && int.TryParse(right.Value, out int rv))
            tmp.Value = (lv + rv).ToString();
        return TrExprP(tmp);
    }
    private IdentifierEntry? TrTerm()
    {
        var f = TrFactor(); if (f == null) return null; return TrTermP(f);
    }
    private IdentifierEntry? TrTermP(IdentifierEntry left)
    {
        if (_tok.Type != Times) return left;
        Match(Times);
        var right = TrFactor(); if (right == null) return null;
        var tmp = _temps.Create();
        _ir.Add("*", left.Name, right.Name, tmp.Name);
        tmp.Type = "int";
        if (int.TryParse(left.Value, out int lv) && int.TryParse(right.Value, out int rv))
            tmp.Value = (lv * rv).ToString();
        return TrTermP(tmp);
    }
    private IdentifierEntry? TrFactor()
    {
        if (_tok.Type == Identifier)
        {
            string n = _tok.Value;
            if (!_ids.Exists(n))        { SemanticError($"undeclared '{n}'"); return null; }
            string val = _ids.GetVal(n);
            if (val == "")              { SemanticError($"'{n}' used before assignment"); return null; }
            var e = new IdentifierEntry(n) { Type = _ids.GetType(n), Value = val };
            Match(Identifier); return e;
        }
        if (_tok.Type == Number)
        {
            var e = new IdentifierEntry(_tok.Value) { Type = "int", Value = _tok.Value };
            Match(Number); return e;
        }
        if (_tok.Type == LParen)
        {
            Match(LParen); var e = TrExpr(); Match(RParen); return e;
        }
        SyntaxError($"expected expression, got [{_tok.Type}]");
        return null;
    }
    private bool TrIf()
    {
        Match(If); Match(LParen);
        var cond = TrCond(); if (cond == null) return false;
        int trueTarget = _ir.NXQ + 2;
        _ir.Add("jnz", cond.Name, "null", trueTarget.ToString());
        _ir.Add("j",   "null",    "null", "?");
        int falseIdx = _ir.Last();
        Match(RParen); Match(Then);
        if (!TrNested()) return false;
        _ir.SetR(falseIdx, (_ir.NXQ + 1).ToString());
        _ir.Add("j", "null", "null", "?");
        int endIdx = _ir.Last();
        Match(Semicolon); Match(Else);
        if (!TrNested()) return false;
        _ir.SetR(endIdx, _ir.NXQ.ToString());
        return true;
    }
    private IdentifierEntry? TrCond()
    {
        var left = TrExpr(); if (left == null) return null;
        if (_tok.Type != RelOp) { SyntaxError("expected relational operator"); return null; }
        string op = _tok.Value; Match(RelOp);
        var right = TrExpr(); if (right == null) return null;
        var tmp = _temps.Create(); tmp.Type = "bool";
        _ir.Add(op, left.Name, right.Name, tmp.Name);
        tmp.Value = op switch
        {
            "==" => left.Value == right.Value                    ? "true" : "false",
            "!=" => left.Value != right.Value                    ? "true" : "false",
            "<"  => left.AsInt() <  right.AsInt()               ? "true" : "false",
            ">"  => left.AsInt() >  right.AsInt()               ? "true" : "false",
            "<=" => left.AsInt() <= right.AsInt()               ? "true" : "false",
            ">=" => left.AsInt() >= right.AsInt()               ? "true" : "false",
            _    => "false"
        };
        return tmp;
    }
    private bool TrNested()
    {
        if (_tok.Type is Identifier or If or While) return TrStmt();
        if (_tok.Type == Begin) return TrCompound();
        SyntaxError("expected statement or 'begin'");
        return false;
    }
    private bool TrCompound()
    {
        if (!Match(Begin)) return false;
        if (!TrStmts())    return false;
        return Match(End);
    }
    private bool TrWhile()
    {
        int loopStart = _ir.NXQ;
        if (!Match(While))  return false;
        if (!Match(LParen)) return false;
        var cond = TrCond(); if (cond == null) return false;
        _ir.Add("jnz", cond.Name, "null", (_ir.NXQ + 2).ToString());
        _ir.Add("j",   "null",    "null", "?");
        int exitIdx = _ir.Last();
        if (!Match(RParen)) return false;
        if (!Match(Do))     return false;
        if (!TrNested())    return false;
        _ir.Add("j", "null", "null", loopStart.ToString());
        _ir.SetR(exitIdx, _ir.NXQ.ToString());
        return true;
    }
}
