using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    // Simple representation of a transformation edge in the synthetic routes diagram
    record Edge(string From, string To, string Reagent);

    static readonly List<Edge> Edges = new()
    {
        new("Alkene", "Alkane", "150°C, H2 / Ni"),
        new("Alkene", "Haloalkane", "Hydrogen halide, 20°C"),
        new("Alkene", "Alcohol", "Steam / H3PO4, high T / pressure (hydration)"),

        new("Haloalkane", "Nitrile", "NaCN / KCN, reflux in ethanol"),
        new("Haloalkane", "Amine", "Excess ethanolic NH3, heat"),

        new("Alcohol (1°)", "Aldehyde", "K2Cr2O7 / H2SO4 (careful conditions)"),
        new("Aldehyde", "Carboxylic acid", "K2Cr2O7 / H2SO4, reflux"),
        new("Alcohol (1°)", "Carboxylic acid", "Strong oxidising agent (K2Cr2O7/H2SO4)"),

        new("Alcohol (2°)", "Ketone", "K2Cr2O7 / H2SO4, reflux"),
        new("Ketone", "Hydroxynitrile", "HCN"),
        new("Aldehyde", "Hydroxynitrile", "HCN"),
        new("Hydroxynitrile", "Amine", "LiAlH4, dilute acid"),

        new("Nitrile", "Amine", "H2 / Ni (or LiAlH4 / acid)") ,

        new("Carboxylic acid", "Acyl chloride", "SOCl2"),
        new("Acyl chloride", "Ester", "Alcohol, 20°C"),
        new("Acyl chloride", "1° Amide", "NH3, 20°C"),
        new("Acyl chloride", "2° Amide", "Primary amine, 20°C"),

        new("Carboxylic acid", "Ester", "Alcohol, conc. H2SO4 (acid catalysis)"),
        new("Ester", "Carboxylate", "OH- (saponification), heat"),
        new("Carboxylate", "Carboxylic acid", "Dilute acid, heat"),
    };

    // Shared state for the countdown timer
    static readonly object ConsoleLock = new();
    static DateTime _countdownEndUtc;
    static volatile bool _countdownExpired;

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var rng = new Random();

        // start a 30 minute countdown that is shown on-screen and will stop the app when it reaches zero
        StartCountdown(TimeSpan.FromMinutes(30));

        PrintBanner();

        Console.WriteLine("Choose mode:");
        Console.WriteLine("  1) Quiz: pick reagent/conditions for a transformation");
        Console.WriteLine("  2) Explore: list all transformations in the diagram");
        Console.Write("Mode (1-2): ");
        var mode = Console.ReadLine()?.Trim() ?? "";
        if (mode == "2")
        {
            ExploreDiagram();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        // Default to quiz mode
        var remaining = new List<Edge>(Edges);
        (string Text, ConsoleColor Color)? lastFeedback = null;

        var total = remaining.Count;

        while (remaining.Count > 0)
        {
            if (_countdownExpired) break;

            Console.Clear();
            PrintBanner();

            int completed = total - remaining.Count;
            DrawProgressBar(completed, total, 30);

            if (lastFeedback.HasValue)
            {
                Console.ForegroundColor = lastFeedback.Value.Color;
                Console.WriteLine(lastFeedback.Value.Text);
                Console.ResetColor();
                Console.WriteLine();
                lastFeedback = null;
            }

            // pick a random transformation
            var idx = rng.Next(remaining.Count);
            var edge = remaining[idx];

            // build choices: correct reagent + distractors
            var allReagents = Edges.Select(e => e.Reagent).Distinct().Where(r => r != edge.Reagent).ToList();
            var distractors = allReagents.OrderBy(_ => rng.Next()).Take(3).ToList();
            var choices = new List<string>(distractors) { edge.Reagent };
            choices = choices.OrderBy(_ => rng.Next()).ToList();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n🌟 What reagent/conditions convert '{edge.From}' to '{edge.To}'? 🌟");
            Console.ResetColor();

            for (int i = 0; i < choices.Count; i++)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($" {i + 1}. ");
                Console.ResetColor();
                Console.WriteLine(choices[i]);
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("Pick your answer (1-4): ");
            Console.ResetColor();
            var input = Console.ReadLine();
            if (!int.TryParse(input, out int selected) || selected < 1 || selected > choices.Count)
            {
                lastFeedback = ($"Invalid choice — correct: {edge.Reagent} ({edge.From} → {edge.To})", ConsoleColor.Red);
                continue;
            }

            if (choices[selected - 1] == edge.Reagent)
            {
                lastFeedback = ("Correct! 🎉", ConsoleColor.Green);
                // mark learned
                remaining.RemoveAt(idx);
            }
            else
            {
                lastFeedback = ($"Wrong — correct: {edge.Reagent} ({edge.From} → {edge.To})", ConsoleColor.Red);
                // keep edge for later
            }
        }

        if (!_countdownExpired)
        {
            Console.Clear();
            PrintBanner();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n🌈 You've practiced all the transformations! Well done. 🎉\n");
            Console.ResetColor();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    static void ExploreDiagram()
    {
        Console.Clear();
        PrintBanner();
        Console.WriteLine("Synthetic routes — grouped by starting compound:\n");

        var groups = Edges.GroupBy(e => e.From).OrderBy(g => g.Key);
        foreach (var g in groups)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(g.Key + ":");
            Console.ResetColor();
            foreach (var e in g)
            {
                Console.WriteLine($"  -> {e.To}    [{e.Reagent}]");
            }
            Console.WriteLine();
        }
    }

    static void StartCountdown(TimeSpan duration)
    {
        _countdownEndUtc = DateTime.UtcNow + duration;
        _countdownExpired = false;

        // Run the timer loop on a background task so it can update the display live even while the main thread is blocked on input.
        Task.Run(() =>
        {
            try
            {
                var totalSeconds = Math.Max(1.0, duration.TotalSeconds);
                const int barWidth = 20;
                while (true)
                {
                    var remaining = _countdownEndUtc - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        // mark expired and show final message
                        _countdownExpired = true;
                        lock (ConsoleLock)
                        {
                            try
                            {
                                Console.Clear();
                                PrintBanner();
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("\n⏰ Time's up — well done! You've completed the session. 🎉\n");
                                Console.ResetColor();
                            }
                            catch { }
                        }
                        // Give the user a brief moment to read the message, then exit.
                        Thread.Sleep(1500);
                        Environment.Exit(0);
                        break;
                    }

                    // Draw a shrinking progress bar representing remaining time
                    var remainingSeconds = Math.Max(0.0, remaining.TotalSeconds);
                    var ratio = remainingSeconds / totalSeconds;
                    var filled = (int)Math.Round(ratio * barWidth);
                    filled = Math.Min(Math.Max(filled, 0), barWidth);
                    var bar = new string('█', filled) + new string('─', barWidth - filled);
                    var text = $"Timer: [{bar}]";

                    lock (ConsoleLock)
                    {
                        try
                        {
                            int width = 0;
                            try { width = Console.WindowWidth; } catch { width = 80; }
                            // position so the bar appears at the top-right
                            int col = Math.Max(0, width - (text.Length + 2));
                            int row = 0;
                            int curLeft = 0;
                            int curTop = 0;
                            try
                            {
                                curLeft = Console.CursorLeft;
                                curTop = Console.CursorTop;
                            }
                            catch { /* ignore if console not available */ }

                            try
                            {
                                Console.SetCursorPosition(col, row);
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                // pad to clear previous content in that area
                                var padded = text.PadRight(text.Length + 1);
                                Console.Write(padded);
                                Console.ResetColor();
                            }
                            catch { /* ignore if setting cursor fails */ }

                            try
                            {
                                Console.SetCursorPosition(curLeft, curTop);
                            }
                            catch { /* ignore */ }
                        }
                        catch { /* ignore all console errors to avoid crashing timer */ }
                    }

                    // update every 15 seconds to avoid flicker (minutes-level precision not needed)
                    Thread.Sleep(15000);
                }
            }
            catch
            {
                // swallow exceptions from background timer to avoid crashing the app
            }
        });
    }

    static string GetGenderHint(string french)
    {
        // Basic heuristic based on the french word form used in the dataset.
        // If it contains "femme" or ends with 'e' (common feminine marker in this set), label female; otherwise male.
        if (string.IsNullOrWhiteSpace(french)) return "";
        var trimmed = french.Trim().ToLowerInvariant();
        if (trimmed.Contains("femme") || trimmed.EndsWith("e"))
            return "(female)";
        return "(male)";
    }

    static bool AreEquivalentFrench(string userInput, string correctFrench)
    {
        // Normalize both strings: lowercase, replace ligatures, remove diacritics, collapse spaces
        string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.ToLowerInvariant().Trim();
            // accept oe for œ
            s = s.Replace("œ", "oe").Replace("Œ", "oe");
            // decompose and strip diacritics
            var temp = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in temp)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            var cleaned = sb.ToString().Normalize(NormalizationForm.FormC);
            // collapse whitespace
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        return string.Equals(Normalize(userInput), Normalize(correctFrench), StringComparison.OrdinalIgnoreCase);
    }

    static void DrawProgressBar(int learned, int total, int width)
    {
        if (total <= 0) return;
        double ratio = (double)learned / total;
        int filled = (int)Math.Round(ratio * width);
        filled = Math.Min(Math.Max(filled, 0), width);
        var bar = new string('█', filled) + new string('─', width - filled);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("Progress: ");
        Console.ResetColor();
        Console.Write("[");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(bar);
        Console.ResetColor();
        Console.WriteLine($"]  {learned}/{total} learned — {total - learned} remaining\n");
    }

    static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("══════════════════════════════════════════════════");
        Console.WriteLine("         🥐 French Vocabulary Tester! 🥖         ");
        Console.WriteLine("══════════════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine("We'll test French → English first (multiple choice), then English → French (type the French).");
        Console.WriteLine("Accents are optional when typing the French — ASCII equivalents are accepted.\n");
    }

    static void PrintAccentInstructionsIfNeeded(string french)
    {
        // kept for reference but accents are optional; this only shows helpful info if you want it
        var accents = new Dictionary<char, string>
        {
            { 'é', "é: Alt+NumPad 0233" },
            { 'è', "è: Alt+NumPad 0232" },
            { 'ê', "ê: Alt+NumPad 0234" },
            { 'ë', "ë: Alt+NumPad 0235" },
            { 'à', "à: Alt+NumPad 0224" },
            { 'â', "â: Alt+NumPad 0226" },
            { 'î', "î: Alt+NumPad 0238" },
            { 'ï', "ï: Alt+NumPad 0239" },
            { 'ô', "ô: Alt+NumPad 0244" },
            { 'ù', "ù: Alt+NumPad 0249" },
            { 'û', "û: Alt+NumPad 0251" },
            { 'ü', "ü: Alt+NumPad 0252" },
            { 'ç', "ç: Alt+NumPad 0231" },
            { 'œ', "œ: Alt+NumPad 0156 (or type 'oe')" },
        };

        var found = accents.Keys.Where(french.Contains).ToList();
        if (found.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Tip: This answer contains special French characters (but accents are optional).");
            foreach (var ch in found)
            {
                Console.WriteLine($"  - To type '{ch}' on Windows: {accents[ch]}");
            }
            Console.ResetColor();
        }
    }
}