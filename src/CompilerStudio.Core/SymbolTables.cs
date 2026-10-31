namespace CompilerStudio.Core;

public class IdentifierEntry
{
    public string Name  { get; set; } = "";
    public string Type  { get; set; } = "";
    public string Value { get; set; } = "";
    public IdentifierEntry(string name) => Name = name;
    public int AsInt() => int.TryParse(Value, out int v) ? v : -1;
}

public class IdentifierTable
{
    private readonly List<IdentifierEntry> _t = new();
    public bool   Exists(string n) => _t.Any(e => e.Name == n);
    public bool   Add(string n)    { if (Exists(n)) return false; _t.Add(new IdentifierEntry(n)); return true; }
    private int   Idx(string n)    { for (int i = 0; i < _t.Count; i++) if (_t[i].Name == n) return i; return -1; }
    public string GetVal(string n) { int i = Idx(n); return i < 0 ? "" : _t[i].Value; }
    public int    GetInt(string n) { int i = Idx(n); return i < 0 ? -1 : _t[i].AsInt(); }
    public bool   SetVal(string n, string v) { int i = Idx(n); if (i < 0) return false; _t[i].Value = v; return true; }
    public string GetType(string n) { int i = Idx(n); return i < 0 ? "" : _t[i].Type; }
    public bool   SetType(string n, string t)
    { int i = Idx(n); if (i < 0 || _t[i].Type != "") return false; _t[i].Type = t; return true; }
    public List<IdentifierEntry> All() => new(_t);
}

public class ConstantTable
{
    private readonly List<string> _t = new();
    public bool Exists(string v) => _t.Contains(v);
    public void Add(string v)    { if (!Exists(v)) _t.Add(v); }
    public List<string> All()    => new(_t);
}

public class TempTable
{
    private readonly List<string> _t = new();
    public IdentifierEntry Create()
    { string n = "T" + (_t.Count + 1); _t.Add(n); return new IdentifierEntry(n); }
    public List<string> All() => new(_t);
}

public class Quadruple
{
    public string Op     { get; }
    public string A      { get; }
    public string B      { get; }
    public string Result { get; set; }
    public Quadruple(string op, string a, string b, string result) { Op = op; A = a; B = b; Result = result; }
    public override string ToString() => $"( {Op} , {A} , {B} , {Result} )";
}

public class QuadrupleTable
{
    private readonly List<Quadruple> _t = new();
    public int  NXQ   => _t.Count;
    public void Add(string op, string a, string b, string r) => _t.Add(new Quadruple(op, a, b, r));
    public bool SetR(int i, string r) { if (i < 0 || i >= _t.Count) return false; _t[i].Result = r; return true; }
    public string GetR(int i) => _t[i].Result;
    public int    Last()      => _t.Count - 1;
    public IReadOnlyList<Quadruple> All() => _t;
    public List<string> Lines() => Format(_t);

    public static List<string> Format(IReadOnlyList<Quadruple> code)
    { var l = new List<string>(); for (int i = 0; i < code.Count; i++) l.Add($"({i})   {code[i]}"); return l; }
}
