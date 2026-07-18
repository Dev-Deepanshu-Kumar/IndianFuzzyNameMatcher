namespace IndianFuzzyNameMatcher;

// ─────────────────────────────────────────────────────────────────────────────
//  ConsoleUI
//  Interactive REPL loop. Handles input, rendering, and threshold adjustments.
//  No external dependencies — only System.Console.
// ─────────────────────────────────────────────────────────────────────────────

public static class ConsoleUI
{
    private const double DefaultThreshold = 0.72;

    // Colours
    private static readonly ConsoleColor ColourAccent   = ConsoleColor.Cyan;
    private static readonly ConsoleColor ColourGold     = ConsoleColor.Yellow;
    private static readonly ConsoleColor ColourMatch    = ConsoleColor.Green;
    private static readonly ConsoleColor ColourNoMatch  = ConsoleColor.Red;
    private static readonly ConsoleColor ColourMuted    = ConsoleColor.DarkGray;
    private static readonly ConsoleColor ColourLabel    = ConsoleColor.White;

    private static NameValidator _validator        = new();
    private static double        _threshold        = DefaultThreshold;
    private static bool          _showAlgoBreakdown = true;

    // ── Entry point ──────────────────────────────────────────────────────────

    public static void Run(ValidatorConfig config)
    {
        _validator         = new NameValidator(config);
        _threshold         = config.Threshold;
        _showAlgoBreakdown = config.ShowAlgoBreakdown;

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.CursorVisible  = true;

        DrawBanner();

        while (true)
        {
            Console.WriteLine();
            DrawDivider();

            string name1 = PromptName("  Name on PAN Card  : ");
            if (IsExitCommand(name1)) break;
            if (IsHelpCommand(name1)) { DrawHelp(); continue; }
            if (IsThresholdCommand(name1, out double newThreshold)) { _threshold = newThreshold; DrawThresholdConfirm(); continue; }

            string name2 = PromptName("  Name as Provided  : ");
            if (IsExitCommand(name2)) break;

            Console.WriteLine();
            var result = _validator.ValidateName(name1, name2, _threshold);
            DrawResult(name1, name2, result);
        }

        DrawGoodbye();
    }

    // ── Input helpers ────────────────────────────────────────────────────────

    private static string PromptName(string label)
    {
        Write("  ", ColourMuted);
        Write(label.Trim(), ColourLabel);
        Write(" ", ColourMuted);
        Console.ForegroundColor = ColourAccent;
        string input = Console.ReadLine()?.Trim() ?? "";
        Console.ResetColor();
        return input;
    }

    private static bool IsExitCommand(string s) =>
        s.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        s.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        s.Equals("q",    StringComparison.OrdinalIgnoreCase);

    private static bool IsHelpCommand(string s) =>
        s.Equals("help", StringComparison.OrdinalIgnoreCase) ||
        s.Equals("?",    StringComparison.OrdinalIgnoreCase);

    private static bool IsThresholdCommand(string s, out double value)
    {
        // Accepts: "threshold 80"  or  "t 80"  or  "set 80"
        value = _threshold;
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        if (!parts[0].Equals("threshold", StringComparison.OrdinalIgnoreCase) &&
            !parts[0].Equals("t",         StringComparison.OrdinalIgnoreCase) &&
            !parts[0].Equals("set",       StringComparison.OrdinalIgnoreCase)) return false;
        if (!double.TryParse(parts[1], out double v)) return false;
        if (v is < 1 or > 100) return false;
        value = v > 1 ? v / 100.0 : v;
        return true;
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    private static void DrawBanner()
    {
        Console.Clear();
        Console.WriteLine();
        WriteLn("  ╔══════════════════════════════════════════════════╗", ColourAccent);
        WriteLn("  ║         PAN Name Validator  v1.0                ║", ColourAccent);
        WriteLn("  ║  Weighted string-similarity engine for Indian   ║", ColourAccent);
        WriteLn("  ║  name variations. Built by Deepanshu Kumar.     ║", ColourAccent);
        WriteLn("  ╚══════════════════════════════════════════════════╝", ColourAccent);
        Console.WriteLine();
        Write("  Default threshold  : ", ColourMuted);
        WriteLn($"{_threshold * 100:F0}%", ColourGold);
        Write("  Commands           : ", ColourMuted);
        WriteLn("help  |  threshold <50–100>  |  quit", ColourMuted);
    }

    private static void DrawResult(string raw1, string raw2, MatchResult result)
    {
        // ── Verdict ──────────────────────────────────────────────────────────
        if (result.IsMatch)
        {
            Write("  ✔  ", ColourMatch);
            WriteLn("NAMES MATCH", ColourMatch);
        }
        else
        {
            Write("  ✘  ", ColourNoMatch);
            WriteLn("NAMES DO NOT MATCH", ColourNoMatch);
        }

        Console.WriteLine();

        if (result.NormalizationNotes.Count > 0)
        {
            Console.WriteLine();
            WriteLn("  ── Pre-processing applied ───────────────────────", ColourMuted);
            foreach (var note in result.NormalizationNotes)
            {
                Write("    • ", ColourGold);
                WriteLn(note, ColourMuted);
            }
        }

        Console.WriteLine();

        if (result.IsSpecialMatch)
        {
            Write("  Reason   : ", ColourMuted);
            WriteLn(result.SpecialReason!, ColourGold);
        }
        else
        {
            // ── Composite score bar ───────────────────────────────────────────
            Write("  Score    : ", ColourMuted);
            Write($"{result.Score * 100:F1}%", result.IsMatch ? ColourMatch : ColourNoMatch);
            Write($"  (threshold {result.Threshold * 100:F0}%)", ColourMuted);
            Console.WriteLine();

            Console.WriteLine();
            Write("  ", ColourMuted);
            DrawBar(result.Score, result.Threshold);
            Console.WriteLine();

            // ── Individual algorithm scores ───────────────────────────────────
            if (_showAlgoBreakdown && result.AlgoScores.Count > 0)
            {
                Console.WriteLine();
                WriteLn("  ── Individual Measures ─────────────────────────", ColourMuted);
                Console.WriteLine();

                int maxNameLen = result.AlgoScores.Keys.Max(k => k.Length);
                foreach (var (algo, score) in result.AlgoScores)
                {
                    string padded = algo.PadRight(maxNameLen + 2);
                    Write($"    {padded}", ColourMuted);
                    DrawBar(score, result.Threshold, width: 24);
                    Write($"  {score * 100,5:F1}%", ColourLabel);
                    Console.WriteLine();
                }
            }
        }
    }

    private static void DrawBar(double score, double threshold, int width = 36)
    {
        int filled    = (int)Math.Round(score * width);
        int threshPos = (int)Math.Round(threshold * width);
        filled    = Math.Clamp(filled,    0, width);
        threshPos = Math.Clamp(threshPos, 0, width);

        Write("[", ColourMuted);
        for (int i = 0; i < width; i++)
        {
            if (i == threshPos)
            {
                Write("|", ColourGold);          // threshold marker
            }
            else if (i < filled)
            {
                ConsoleColor barColour = score >= threshold ? ColourMatch : ConsoleColor.DarkYellow;
                Write("█", barColour);
            }
            else
            {
                Write("░", ColourMuted);
            }
        }
        Write("]", ColourMuted);
    }

    private static void DrawDivider() =>
        WriteLn("  " + new string('─', 50), ColourMuted);

    private static void DrawHelp()
    {
        Console.WriteLine();
        WriteLn("  ── Help ─────────────────────────────────────────", ColourGold);
        Console.WriteLine();
        WriteLn("  Enter any two names when prompted.", ColourLabel);
        Console.WriteLine();
        WriteLn("  Commands (type instead of a name):", ColourMuted);
        Console.WriteLine();
        Write("    threshold <number>  ", ColourAccent);
        WriteLn("Set match threshold. E.g. 'threshold 75'", ColourLabel);
        Write("    t <number>          ", ColourAccent);
        WriteLn("Shorthand for threshold.", ColourLabel);
        Write("    help  or  ?         ", ColourAccent);
        WriteLn("Show this help.", ColourLabel);
        Write("    quit  or  exit      ", ColourAccent);
        WriteLn("Exit the program.", ColourLabel);
        Console.WriteLine();
        Write("  Current threshold : ", ColourMuted);
        WriteLn($"{_threshold * 100:F0}%", ColourGold);
        Write("  Default           : ", ColourMuted);
        WriteLn($"{DefaultThreshold * 100:F0}%", ColourMuted);
    }

    private static void DrawThresholdConfirm()
    {
        Write("\n  Threshold updated to ", ColourMuted);
        Write($"{_threshold * 100:F0}%", ColourGold);
        WriteLn(" — takes effect on next comparison.\n", ColourMuted);
    }

    private static void DrawGoodbye()
    {
        Console.WriteLine();
        WriteLn("  Goodbye.", ColourMuted);
        Console.WriteLine();
    }

    // ── Console write helpers ────────────────────────────────────────────────

    private static void Write(string text, ConsoleColor colour)
    {
        Console.ForegroundColor = colour;
        Console.Write(text);
        Console.ResetColor();
    }

    private static void WriteLn(string text, ConsoleColor colour)
    {
        Console.ForegroundColor = colour;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
