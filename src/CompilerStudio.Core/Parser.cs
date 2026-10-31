using static CompilerStudio.Core.TokenTypes;

namespace CompilerStudio.Core;

// Shared token cursor for the two recursive-descent passes.
public abstract class RecursiveDescentBase
{
    protected readonly CompilationResult _r;
    private readonly Lexer _lexer;
    protected Token _tok;

    protected RecursiveDescentBase(Lexer lexer, CompilationResult result)
    {
        _lexer = lexer;
        _r     = result;
        _tok   = lexer.NextToken();
    }

    protected bool Match(string type)
    {
        if (_tok.Type == type)
        {
            _r.ParseLog.Add($"  ✓  [{type}]  \"{_tok.Value}\"");
            _tok = _lexer.NextToken();
            return true;
        }
        _r.AddError("Syntax", _tok.Line, _tok.Col,
                    $"expected [{type}], found [{_tok.Type}] \"{_tok.Value}\"");
        _r.ParseLog.Add($"  ✗  expected [{type}], got [{_tok.Type}] \"{_tok.Value}\"");
        return false;
    }
}

// Syntax check only: walks the LL(1) grammar and records each production used.
public class Parser : RecursiveDescentBase
{
    public Parser(Lexer lexer, CompilationResult result) : base(lexer, result) { }

    private void Log(string rule) => _r.ParseLog.Add(rule);

    public void ParseProgram()    { Log("⟨program⟩  →  ⟨var_decl⟩ ; ⟨stmts⟩");    ParseVarDecl(); Match(Semicolon); ParseStmts(); }
    private void ParseVarDecl()   { Log("⟨var_decl⟩  →  int ⟨id_list⟩");            Match(Int); ParseIdList(); }
    private void ParseIdList()    { Log("⟨id_list⟩  →  id ⟨id_list'⟩");             Match(Identifier); ParseIdListP(); }
    private void ParseIdListP()
    {
        if (_tok.Type == Comma) { Log("⟨id_list'⟩  →  , id ⟨id_list'⟩"); Match(Comma); Match(Identifier); ParseIdListP(); }
        else Log("⟨id_list'⟩  →  ε");
    }
    private void ParseStmts()  { Log("⟨stmts⟩  →  ⟨stmt⟩ ; ⟨stmts'⟩");  ParseStmt(); Match(Semicolon); ParseStmtsP(); }
    private void ParseStmtsP()
    {
        if (_tok.Type is Identifier or If or While)
        { Log("⟨stmts'⟩  →  ⟨stmt⟩ ; ⟨stmts'⟩"); ParseStmt(); Match(Semicolon); ParseStmtsP(); }
        else Log("⟨stmts'⟩  →  ε");
    }
    private void ParseStmt()
    {
        if      (_tok.Type == Identifier) { Log("⟨stmt⟩  →  ⟨assign⟩");     ParseAssign(); }
        else if (_tok.Type == If)         { Log("⟨stmt⟩  →  ⟨if_stmt⟩");    ParseIf(); }
        else if (_tok.Type == While)      { Log("⟨stmt⟩  →  ⟨while_stmt⟩"); ParseWhile(); }
    }
    private void ParseAssign()  { Log("⟨assign⟩  →  id = ⟨expr⟩"); Match(Identifier); Match(Assign); ParseExpr(); }
    private void ParseExpr()    { Log("⟨expr⟩  →  ⟨term⟩ ⟨expr'⟩"); ParseTerm(); ParseExprP(); }
    private void ParseExprP()
    {
        if (_tok.Type == Plus) { Log("⟨expr'⟩  →  + ⟨term⟩ ⟨expr'⟩"); Match(Plus); ParseTerm(); ParseExprP(); }
        else Log("⟨expr'⟩  →  ε");
    }
    private void ParseTerm()    { Log("⟨term⟩  →  ⟨factor⟩ ⟨term'⟩"); ParseFactor(); ParseTermP(); }
    private void ParseTermP()
    {
        if (_tok.Type == Times) { Log("⟨term'⟩  →  * ⟨factor⟩ ⟨term'⟩"); Match(Times); ParseFactor(); ParseTermP(); }
        else Log("⟨term'⟩  →  ε");
    }
    private void ParseFactor()
    {
        if      (_tok.Type == Identifier) { Log("⟨factor⟩  →  id");          Match(Identifier); }
        else if (_tok.Type == Number)     { Log("⟨factor⟩  →  num");         Match(Number); }
        else if (_tok.Type == LParen)     { Log("⟨factor⟩  →  ( ⟨expr⟩ )"); Match(LParen); ParseExpr(); Match(RParen); }
    }
    private void ParseIf()
    {
        Log("⟨if_stmt⟩  →  if ( ⟨cond⟩ ) then ⟨nested⟩ ; else ⟨nested⟩");
        Match(If); Match(LParen); ParseCond(); Match(RParen);
        Match(Then); ParseNested(); Match(Semicolon); Match(Else); ParseNested();
    }
    private void ParseCond()    { Log("⟨cond⟩  →  ⟨expr⟩ relop ⟨expr⟩"); ParseExpr(); Match(RelOp); ParseExpr(); }
    private void ParseNested()
    {
        if      (_tok.Type is Identifier or If or While) { Log("⟨nested⟩  →  ⟨stmt⟩");     ParseStmt(); }
        else if (_tok.Type == Begin)                     { Log("⟨nested⟩  →  ⟨compound⟩"); ParseCompound(); }
    }
    private void ParseCompound() { Log("⟨compound⟩  →  begin ⟨stmts⟩ end"); Match(Begin); ParseStmts(); Match(End); }
    private void ParseWhile()
    {
        Log("⟨while_stmt⟩  →  while ( ⟨cond⟩ ) do ⟨nested⟩");
        Match(While); Match(LParen); ParseCond(); Match(RParen); Match(Do); ParseNested();
    }
}
