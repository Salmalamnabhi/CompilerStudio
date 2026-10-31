namespace CompilerStudio.Core;

// Optimizes quadruples while keeping the final value of every $variable$.
// Passes run until nothing changes:
//   1. constant folding and propagation inside each basic block
//   2. removal of temps that are never read
//   3. merging "Tn = a op b; $v$ = Tn" into "$v$ = a op b"
//   4. removal of unreachable code and jumps to the next instruction
public static class Optimizer
{
    private static readonly HashSet<string> Arithmetic = new() { "+", "*" };
    private static readonly HashSet<string> Relational = new() { "<", ">", "<=", ">=", "==", "!=" };

    public static List<Quadruple> Optimize(IReadOnlyList<Quadruple> input)
    {
        var code = input.Select(q => new Quadruple(q.Op, q.A, q.B, q.Result)).ToList();

        for (int round = 0; round < 50; round++)
        {
            bool changed = false;
            changed |= FoldConstants(code);
            changed |= RemoveDeadTemps(ref code);
            changed |= MergeTempCopies(ref code);
            changed |= RemoveUnreachable(ref code);
            changed |= RemoveJumpsToNext(ref code);
            if (!changed) break;
        }
        return code;
    }

    // ── 1. Constant folding / propagation ───────────────────────────────────

    private static bool FoldConstants(List<Quadruple> code)
    {
        bool changed = false;
        var leaders = Leaders(code);
        var known = new Dictionary<string, int>();

        for (int i = 0; i < code.Count; i++)
        {
            if (leaders.Contains(i)) known.Clear();

            var q = code[i];
            string a = Substitute(q.A), b = Substitute(q.B);
            Quadruple folded = new(q.Op, a, b, q.Result);

            if (IsJump(q.Op))
            {
                if (q.Op == "jnz" && IsLiteral(a, out int cond))
                    folded = cond != 0 ? new("j", "null", "null", q.Result)
                                       : new("nop", "null", "null", "null");
            }
            else
            {
                if ((Arithmetic.Contains(q.Op) || Relational.Contains(q.Op))
                    && IsLiteral(a, out int x) && IsLiteral(b, out int y))
                    folded = new("=", Evaluate(q.Op, x, y).ToString(), "null", q.Result);

                if (folded.Op == "=" && IsLiteral(folded.A, out int v)) known[q.Result] = v;
                else known.Remove(q.Result);
            }

            if (folded.ToString() != q.ToString()) { code[i] = folded; changed = true; }
        }

        // A jnz that can never jump becomes a "nop"; RemoveUnreachable deletes it.
        return changed;

        string Substitute(string operand) =>
            known.TryGetValue(operand, out int v) ? v.ToString() : operand;
    }

    private static int Evaluate(string op, int x, int y) => op switch
    {
        "+"  => unchecked(x + y),
        "*"  => unchecked(x * y),
        "<"  => x <  y ? 1 : 0,
        ">"  => x >  y ? 1 : 0,
        "<=" => x <= y ? 1 : 0,
        ">=" => x >= y ? 1 : 0,
        "==" => x == y ? 1 : 0,
        "!=" => x != y ? 1 : 0,
        _    => throw new ArgumentException(op)
    };

    // ── 2. Dead temps ───────────────────────────────────────────────────────

    private static bool RemoveDeadTemps(ref List<Quadruple> code)
    {
        var read = ReadOperands(code);
        var remove = code.Select(q => !IsJump(q.Op) && IsTemp(q.Result) && !read.Contains(q.Result)).ToArray();
        return Remove(ref code, remove);
    }

    // ── 3. Tn = a op b ; $v$ = Tn  →  $v$ = a op b ─────────────────────────

    private static bool MergeTempCopies(ref List<Quadruple> code)
    {
        var targets = JumpTargets(code);
        var remove  = new bool[code.Count];

        for (int i = 0; i + 1 < code.Count; i++)
        {
            var def = code[i]; var copy = code[i + 1];
            if (IsJump(def.Op) || !IsTemp(def.Result)) continue;
            if (copy.Op != "=" || copy.A != def.Result || targets.Contains(i + 1)) continue;
            if (ReadCount(code, def.Result) != 1) continue;

            code[i] = new Quadruple(def.Op, def.A, def.B, copy.Result);
            remove[i + 1] = true;
            i++;
        }
        return Remove(ref code, remove);
    }

    // ── 4. Unreachable code / jumps to next ─────────────────────────────────

    private static bool RemoveUnreachable(ref List<Quadruple> code)
    {
        var reachable = new bool[code.Count];
        var work = new Stack<int>();
        work.Push(0);
        while (work.Count > 0)
        {
            int i = work.Pop();
            if (i >= code.Count || reachable[i]) continue;
            reachable[i] = true;
            var q = code[i];
            if (IsJump(q.Op)) work.Push(int.Parse(q.Result));
            if (q.Op != "j")  work.Push(i + 1);
        }
        var current = code;
        var remove = reachable.Select((r, i) => !r || current[i].Op == "nop").ToArray();
        return Remove(ref code, remove);
    }

    private static bool RemoveJumpsToNext(ref List<Quadruple> code)
    {
        var remove = code.Select((q, i) => q.Op == "j" && q.Result == (i + 1).ToString()).ToArray();
        return Remove(ref code, remove);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    // Deletes marked instructions and retargets jumps. A jump to a deleted
    // instruction moves to the next instruction that survives.
    private static bool Remove(ref List<Quadruple> code, bool[] remove)
    {
        if (!remove.Any(r => r)) return false;

        var newIndex = new int[code.Count + 1];
        for (int i = 0, kept = 0; i <= code.Count; i++)
        {
            newIndex[i] = kept;
            if (i < code.Count && !remove[i]) kept++;
        }

        var result = new List<Quadruple>();
        for (int i = 0; i < code.Count; i++)
        {
            if (remove[i]) continue;
            var q = code[i];
            result.Add(IsJump(q.Op)
                ? new Quadruple(q.Op, q.A, q.B, newIndex[int.Parse(q.Result)].ToString())
                : q);
        }
        code = result;
        return true;
    }

    private static HashSet<int> Leaders(List<Quadruple> code)
    {
        var leaders = new HashSet<int> { 0 };
        for (int i = 0; i < code.Count; i++)
            if (IsJump(code[i].Op)) { leaders.Add(int.Parse(code[i].Result)); leaders.Add(i + 1); }
        return leaders;
    }

    private static HashSet<int> JumpTargets(List<Quadruple> code) =>
        code.Where(q => IsJump(q.Op)).Select(q => int.Parse(q.Result)).ToHashSet();

    private static HashSet<string> ReadOperands(List<Quadruple> code) =>
        code.SelectMany(q => new[] { q.A, q.B }).ToHashSet();

    private static int ReadCount(List<Quadruple> code, string name) =>
        code.Sum(q => (q.A == name ? 1 : 0) + (q.B == name ? 1 : 0));

    private static bool IsJump(string op)  => op is "j" or "jnz";
    private static bool IsTemp(string name) => name.Length > 1 && name[0] == 'T' && name.Skip(1).All(char.IsDigit);
    private static bool IsLiteral(string s, out int v) => int.TryParse(s, out v);
}
