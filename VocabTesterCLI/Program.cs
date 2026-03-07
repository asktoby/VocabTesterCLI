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
    // Build vocab from the "Quand ..." weather/time phrases shown in the provided image.
    // The app will test the French phrase (e.g. "Quand il pleut") ↔ the English meaning ("When it rains").
    static readonly (string French, string English)[] Vocab = BuildVocab();

    // simple category mapping so multiple-choice distractors come from the same category
    static readonly Dictionary<string, string> CategoryByFrench = BuildCategoryMap(Vocab);

    enum QuizState { NeedEnglish, NeedFrench }

    // Shared state for the countdown timer
    static readonly object ConsoleLock = new();
    static DateTime _countdownEndUtc;
    static volatile bool _countdownExpired;

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var rng = new Random();

        // start a 20 minute countdown that is shown on-screen and will stop the app when it reaches zero
        StartCountdown(TimeSpan.FromMinutes(20));

        // Start by testing French -> English first (multiple choice).
        // After correct, require English -> French (typed) before eliminating.
        var remaining = Vocab.ToDictionary(v => v, v => QuizState.NeedEnglish);

        // store text + color for feedback shown with next question
        (string Text, ConsoleColor Color)? lastFeedback = null;

        // totalWords remains number of vocabulary items; totalSteps counts both directions
        var totalWords = Vocab.Length;
        var totalSteps = totalWords * 2;

        PrintBanner();

        while (remaining.Count > 0)
        {
            // If timer expired, stop immediately (background task also calls Environment.Exit, but check here too)
            if (_countdownExpired)
                break;

            var order = remaining.Keys.OrderBy(_ => rng.Next()).ToList();

            foreach (var key in order)
            {
                // If timer expired, stop immediately
                if (_countdownExpired)
                    break;

                // item may have been removed earlier in this pass
                if (!remaining.TryGetValue(key, out var state))
                    continue;

                // Clear and show banner + last feedback before each question
                Console.Clear();
                PrintBanner();

                // Show progress (learned vs remaining) — count steps completed:
                // 0 = not seen yet, 1 = French->English done (NeedFrench), 2 = fully learned (removed from remaining)
                int learnedSteps = Vocab.Sum(v =>
                {
                    if (!remaining.ContainsKey(v)) return 2;
                    return remaining[v] == QuizState.NeedFrench ? 1 : 0;
                });

                DrawProgressBar(learnedSteps, totalSteps, 30);

                if (lastFeedback.HasValue)
                {
                    Console.ForegroundColor = lastFeedback.Value.Color;
                    Console.WriteLine(lastFeedback.Value.Text);
                    Console.ResetColor();
                    Console.WriteLine();
                    lastFeedback = null;
                }

                bool correct = false;

                if (state == QuizState.NeedEnglish)
                {
                    // Ask for English meaning (multiple choice): French -> English
                    // Build distractors only from the same category as the correct answer.
                    var correctEnglish = key.English;

                    CategoryByFrench.TryGetValue(key.French, out var correctCategory);

                    // related choices from same category (distinct English)
                    var related = Vocab
                        .Where(v => CategoryByFrench.TryGetValue(v.French, out var c) && c == correctCategory)
                        .Select(v => v.English)
                        .Distinct()
                        .ToList();

                    if (!related.Contains(correctEnglish))
                        related.Add(correctEnglish);

                    // remove correct for selection of distractors
                    var distractors = related.Where(e => e != correctEnglish).OrderBy(_ => rng.Next()).ToList();

                    // If not enough related distractors, fill from the global pool (still avoid mixing categories if possible, but fallback allowed)
                    if (distractors.Count < 3)
                    {
                        var globalPool = Vocab.Select(v => v.English).Distinct().Where(e => e != correctEnglish && !distractors.Contains(e)).OrderBy(_ => rng.Next()).ToList();
                        foreach (var g in globalPool)
                        {
                            distractors.Add(g);
                            if (distractors.Count >= 3) break;
                        }
                    }

                    // take up to 3 distractors, then append the correct answer and shuffle
                    var finalChoices = distractors.Take(3).Append(correctEnglish).OrderBy(_ => rng.Next()).ToList();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n🌟 What is the English for \"{key.French}\"? 🌟");
                    Console.ResetColor();
                    for (int i = 0; i < finalChoices.Count; i++)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($" {i + 1}. ");
                        Console.ResetColor();
                        Console.WriteLine($"{finalChoices[i]}");
                    }

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("Pick your answer (1-4): ");
                    Console.ResetColor();
                    var input = Console.ReadLine();
                    if (!int.TryParse(input, out int selected) || selected < 1 || selected > finalChoices.Count)
                    {
                        // invalid - mark as wrong for this round and show correction next question (red)
                        lastFeedback = ($"Invalid choice — correct: {correctEnglish} (French: \"{key.French}\")", ConsoleColor.Red);
                        continue;
                    }

                    if (finalChoices[selected - 1] == correctEnglish)
                    {
                        // don't pause — show congrats (green) with next question
                        lastFeedback = ("Correct! 🎉✨", ConsoleColor.Green);

                        // mark English-side as done: move to NeedFrench (this counts as one step done)
                        remaining[key] = QuizState.NeedFrench;

                        correct = false; // removal happens after typing French
                    }
                    else
                    {
                        // immediate correction shown on next question (red) and include the French being tested
                        lastFeedback = ($"Wrong — correct: {correctEnglish} (French: \"{key.French}\")", ConsoleColor.Red);
                        // counted as wrong this pass
                        continue;
                    }
                }
                else // NeedFrench
                {
                    // Ask for French meaning (free text): English -> French
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n🌟 Type the French for \"{key.English}\"! 🌟");
                    Console.ResetColor();

                    // Track whether user corrected a shown answer; corrections should not mark the item as learned.
                    bool correctedButNotLearned = false;

                    // First attempt (if correct immediately => learned); otherwise show correct, require typing it, but do not mark learned
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("Your answer: ");
                    Console.ResetColor();
                    var firstInput = Console.ReadLine()?.Trim() ?? "";

                    if (AreEquivalentFrench(firstInput, key.French))
                    {
                        // success — show congrats (green) with next question and mark as learned
                        lastFeedback = ("Magnifique! You got the French right! 🥳🥐", ConsoleColor.Green);
                        // remove permanently (this completes the second step)
                        remaining.Remove(key);
                        correct = false; // not needed — removal already performed
                    }
                    else
                    {
                        // Wrong: show correct answer immediately (red), then require user to type it, but do not mark learned
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Aww, not quite! The correct answer is \"{key.French}\". 🍬");
                        Console.ResetColor();

                        // Ask user to type the correct form now (this is practice only; does not mark learned)
                        while (true)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("Please type the correct French word now (accents optional): ");
                            Console.ResetColor();

                            var confirm = Console.ReadLine()?.Trim() ?? "";
                            if (AreEquivalentFrench(confirm, key.French))
                            {
                                correctedButNotLearned = true;
                                lastFeedback = ("Thanks — that's correct. You'll be retested later. 🥖", ConsoleColor.Yellow);
                                break;
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("That's still not correct. Let's try again.");
                                Console.ResetColor();
                                // loop remains until correct
                            }
                        }
                    }

                    // Note: learned steps update will be reflected on the next iteration because remaining was modified.
                }

                // Immediately continue to next question; lastFeedback will be displayed above it
            }
            // Loop continues until remaining is empty; incorrect answers remain unchanged
        }

        // Final clear + banner + celebration (if timer hasn't already ended)
        if (!_countdownExpired)
        {
            Console.Clear();
            PrintBanner();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n🌈 All words answered correctly both ways! You're a vocab superstar! 🦄✨");
            Console.ResetColor();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
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

                    // Show minutes remaining (rounded up). If less than 1 minute, show "<1 min remaining".
                    var minutesLeft = (int)Math.Ceiling(remaining.TotalMinutes);
                    var minuteText = minutesLeft >= 1
                        ? $"{minutesLeft} min{(minutesLeft == 1 ? "" : "s")} remaining"
                        : "<1 min remaining";
                    var text = $"Timer: {minuteText}";

                    lock (ConsoleLock)
                    {
                        try
                        {
                            int width = 0;
                            try { width = Console.WindowWidth; } catch { width = 80; }
                            // position so the timer appears at the top-right
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

                    // update every 15 seconds (minutes-level precision is sufficient)
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

    static (string French, string English)[] BuildVocab()
    {
        // Daily routine phrases from the provided image.
        var items = new[]
        {
            ("je me lève", "I get up"),
            ("je prends le petit déjeuner", "I have breakfast"),
            ("je prépare mon sac", "I get my bag ready"),
            ("je promène le chien", "I walk the dog"),
            ("je regarde la télé", "I watch TV"),
            ("je rentre à la maison", "I go back home"),
            ("je me repose", "I rest"),
            ("je sors de chez moi", "I leave my house"),
            ("je surfe sur internet", "I surf the internet"),
            ("je vais au collège", "I go to school"),
            ("je vais au collège en bus", "I go to school by bus"),
        };

        return items.Select(t => (French: t.Item1, English: t.Item2)).ToArray();
    }

    static Dictionary<string, string> BuildCategoryMap((string French, string English)[] vocab)
    {
        // Mark all entries as the same category so distractors come from the related daily-routine set.
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (french, english) in vocab)
        {
            map[french] = "daily_routine";
        }
        return map;
    }
}