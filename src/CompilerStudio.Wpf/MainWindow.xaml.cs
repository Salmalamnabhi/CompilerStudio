using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using CompilerStudio.Core;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace CompilerStudio;

// ── Public types used for WPF binding ─────────────────────────────────────

public class TokenItem
{
    public string Cat { get; init; } = "";
    public string Val { get; init; } = "";
    public string Loc { get; init; } = "";

    public static TokenItem From(Token t) => new()
    {
        Cat = $"({TokenInfo.Category(t.Type)},",
        Val = $" \"{t.Value}\")",
        Loc = $"  line {t.Line}:{t.Col}"
    };
}

// ── MainWindow ─────────────────────────────────────────────────────────────

public partial class MainWindow : Window
{
    // ── Tab buttons ───────────────────────────────────────────────────────

    private List<Button> _allTabs = null!;
    private string _currentTab = "Errors";

    private static readonly SolidColorBrush ActiveBg = new(Color.FromRgb(0x31, 0x32, 0x44));
    private static readonly SolidColorBrush ActiveFg = new(Color.FromRgb(0xCD, 0xD6, 0xF4));
    private static readonly SolidColorBrush IdleBg   = new(Colors.Transparent);
    private static readonly SolidColorBrush IdleFg   = new(Color.FromRgb(0x58, 0x5B, 0x70));

    // ── Constructor ───────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Load AvalonEdit syntax highlighting from embedded resource
        var asm  = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
                      .FirstOrDefault(n => n.EndsWith("CustomLang.xshd"));
        if (name != null)
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            using var reader = XmlReader.Create(stream);
            Editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }

        // Style line-number margin
        Editor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));

        // Selection colour
        Editor.TextArea.SelectionBrush =
            new SolidColorBrush(Color.FromArgb(70, 137, 180, 250));

        // Default source code sample
        Editor.Text =
            "int $x$, $y$;\n" +
            "$x$ = 10;\n" +
            "$y$ = $x$ + 5;\n";

        // Initialise tabs
        _allTabs = new List<Button>
        {
            Btn_ParseTree, Btn_Quads, Btn_FIRST, Btn_Grammar, Btn_Lexer,
            Btn_Summary, Btn_Errors, Btn_Tokens, Btn_Symbols, Btn_AST, Btn_Output
        };
        SelectTab("Errors");
    }

    // ── Tab switching ─────────────────────────────────────────────────────

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b) SelectTab(b.Tag as string ?? "Errors");
    }

    private void SelectTab(string tab)
    {
        _currentTab = tab;

        foreach (var b in _allTabs)
        {
            bool active = (b.Tag as string) == tab;
            b.Background = active ? ActiveBg : IdleBg;
            b.Foreground = active ? ActiveFg : IdleFg;
        }

        View_Errors.Visibility      = tab == "Errors"    ? Visibility.Visible : Visibility.Collapsed;
        View_Tokens.Visibility      = tab == "Tokens"    ? Visibility.Visible : Visibility.Collapsed;
        View_Quads.Visibility       = tab == "Quads"     ? Visibility.Visible : Visibility.Collapsed;
        View_Symbols.Visibility     = tab == "Symbols"   ? Visibility.Visible : Visibility.Collapsed;
        View_Summary.Visibility     = tab == "Summary"   ? Visibility.Visible : Visibility.Collapsed;
        View_ParseLog.Visibility    = tab == "ParseTree" ? Visibility.Visible : Visibility.Collapsed;
        View_Output.Visibility      = tab == "Output"    ? Visibility.Visible : Visibility.Collapsed;
        View_Placeholder.Visibility = tab is "FIRST" or "Grammar" or "Lexer" or "AST"
                                        ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Refresh all artifact views ────────────────────────────────────────

    private void RefreshAll(CompilationResult r, bool includeParse, bool includeIR,
                            IReadOnlyList<Quadruple>? code = null)
    {
        code ??= r.Quadruples.All();

        // Stats
        StatErrors.Text    = r.Errors.Count.ToString();
        StatTokens.Text    = r.Tokens.Count.ToString();
        StatArtifacts.Text = code.Count.ToString();

        // Errors tab
        ErrorsList.ItemsSource = r.Errors;

        // Tokens tab
        TokensList.ItemsSource = r.Tokens.Select(TokenItem.From).ToList();

        // Quads tab
        QuadsList.Items.Clear();
        foreach (var l in QuadrupleTable.Format(code)) QuadsList.Items.Add(l);

        // Symbols tab
        SymbolGrid.ItemsSource  = r.Identifiers.All();
        ConstsList.ItemsSource  = r.Constants.All();
        TempsList.ItemsSource   = r.Temps.All();

        // Parse log
        ParseLogList.Items.Clear();
        foreach (var l in r.ParseLog) ParseLogList.Items.Add(l);

        // Summary
        var sb = new StringBuilder();
        int errorCount = r.Errors.Count;
        int lexErrors  = r.Errors.Count(e => e.Phase == "Lexical");
        int synErrors  = r.Errors.Count(e => e.Phase == "Syntax");
        int semErrors  = r.Errors.Count(e => e.Phase == "Semantic");
        sb.AppendLine($"Source          {r.SourceLines.Length} lines");
        sb.AppendLine($"Tokens found    {r.Tokens.Count}");
        sb.AppendLine($"Identifiers     {r.Identifiers.All().Count}");
        sb.AppendLine($"Constants       {r.Constants.All().Count}");
        if (includeParse) sb.AppendLine($"Parse rules     {r.ParseLog.Count}");
        if (includeIR)    sb.AppendLine($"Quadruples      {r.Quadruples.NXQ}");
        if (includeIR && code.Count != r.Quadruples.NXQ)
                          sb.AppendLine($"  optimized     {code.Count}");
        sb.AppendLine();
        sb.AppendLine($"Lexical errors  {lexErrors}");
        sb.AppendLine($"Syntax errors   {synErrors}");
        sb.AppendLine($"Semantic errors {semErrors}");
        sb.AppendLine($"Total errors    {errorCount}");
        sb.AppendLine();
        sb.Append(errorCount == 0 ? "✓  Compilation successful" : "✗  Compilation failed");
        SummaryText.Text = sb.ToString();

        SelectTab(_currentTab);
    }

    // ── Button handlers ───────────────────────────────────────────────────

    private void BtnLexical_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Editor.Text)) return;
        RefreshAll(Compiler.Lex(Editor.Text), false, false);
        SelectTab("Tokens");
    }

    private void BtnSyntax_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Editor.Text)) return;
        var r = Compiler.Parse(Editor.Text);
        RefreshAll(r, true, false);
        SelectTab(r.Succeeded ? "ParseTree" : "Errors");
    }

    private void BtnTranslate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Editor.Text)) return;
        var r = Compiler.Translate(Editor.Text);
        RefreshAll(r, false, true, CodeFor(r));
        SelectTab(r.Succeeded ? "Quads" : "Errors");
    }

    private void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Editor.Text)) return;
        var r = Compiler.Translate(Editor.Text);
        var code = CodeFor(r);

        if (!r.Succeeded)
        {
            OutputText.Text = "✗  Not run: fix the compile errors first.";
            RefreshAll(r, false, true, code);
            SelectTab("Errors");
            return;
        }

        var x = Interpreter.Run(code);

        // Show run-time values in the Symbols view instead of the translator's estimates
        foreach (var (name, value) in x.Variables) r.Identifiers.SetVal(name, value.ToString());
        RefreshAll(r, false, true, code);

        var sb = new StringBuilder();
        sb.AppendLine($"Ran {code.Count} quadruples in {x.Steps} steps" +
                      (OptimizeBox.IsChecked == true ? " (optimized)" : ""));
        sb.AppendLine();
        int width = x.Variables.Select(p => p.Key.Length).DefaultIfEmpty(0).Max();
        foreach (var (name, value) in x.Variables) sb.AppendLine($"{name.PadRight(width)}  =  {value}");
        sb.AppendLine();
        sb.Append(x.Succeeded ? "✓  Finished" : $"✗  Runtime error: {x.Error}");
        OutputText.Text = sb.ToString();
        SelectTab("Output");
    }

    private IReadOnlyList<Quadruple> CodeFor(CompilationResult r) =>
        OptimizeBox.IsChecked == true && r.Succeeded
            ? Optimizer.Optimize(r.Quadruples.All())
            : r.Quadruples.All();
}
