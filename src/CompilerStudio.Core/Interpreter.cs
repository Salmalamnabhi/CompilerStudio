namespace CompilerStudio.Core;

public class ExecutionResult
{
    // Final values of the program's $variables$, in first-assigned order
    public List<KeyValuePair<string, int>> Variables { get; } = new();
    public int     Steps     { get; set; }
    public string? Error     { get; set; }
    public bool    Succeeded => Error == null;
}

// Executes quadruples directly. Booleans are ints: 0 is false, anything else true.
public static class Interpreter
{
    public const int DefaultMaxSteps = 100_000;

    public static ExecutionResult Run(IReadOnlyList<Quadruple> code, int maxSteps = DefaultMaxSteps)
    {
        var result = new ExecutionResult();
        var env    = new Dictionary<string, int>();
        var order  = new List<string>();
        int pc     = 0;

        try
        {
            while (pc < code.Count)
            {
                if (++result.Steps > maxSteps)
                    throw new RuntimeError($"step limit of {maxSteps} reached (infinite loop?)");

                var q = code[pc];
                int next = pc + 1;
                switch (q.Op)
                {
                    case "=":   Set(q.Result, Read(q.A)); break;
                    case "+":   Set(q.Result, unchecked(Read(q.A) + Read(q.B))); break;
                    case "*":   Set(q.Result, unchecked(Read(q.A) * Read(q.B))); break;
                    case "<":   Set(q.Result, Read(q.A) <  Read(q.B) ? 1 : 0); break;
                    case ">":   Set(q.Result, Read(q.A) >  Read(q.B) ? 1 : 0); break;
                    case "<=":  Set(q.Result, Read(q.A) <= Read(q.B) ? 1 : 0); break;
                    case ">=":  Set(q.Result, Read(q.A) >= Read(q.B) ? 1 : 0); break;
                    case "==":  Set(q.Result, Read(q.A) == Read(q.B) ? 1 : 0); break;
                    case "!=":  Set(q.Result, Read(q.A) != Read(q.B) ? 1 : 0); break;
                    case "j":   next = Target(q); break;
                    case "jnz": if (Read(q.A) != 0) next = Target(q); break;
                    default:    throw new RuntimeError($"unknown operation '{q.Op}' at ({pc})");
                }
                pc = next;
            }
        }
        catch (RuntimeError ex)
        {
            result.Error = ex.Message;
        }

        foreach (var name in order.Where(IsVariable))
            result.Variables.Add(new(name, env[name]));
        return result;

        int Read(string operand)
        {
            if (int.TryParse(operand, out int literal)) return literal;
            if (env.TryGetValue(operand, out int v))    return v;
            throw new RuntimeError($"'{operand}' read before assignment at ({pc})");
        }

        void Set(string name, int value)
        {
            if (!env.ContainsKey(name)) order.Add(name);
            env[name] = value;
        }

        int Target(Quadruple q)
        {
            if (int.TryParse(q.Result, out int t) && t >= 0 && t <= code.Count) return t;
            throw new RuntimeError($"bad jump target '{q.Result}' at ({pc})");
        }
    }

    public static bool IsVariable(string name) => name.StartsWith('$');

    private class RuntimeError(string message) : Exception(message);
}
