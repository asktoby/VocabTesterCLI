using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

class Program
{
    static readonly (string French, string English)[] Vocab = new[]
    {
        ("acteur", "actor"),
        ("actrice", "actress"),
        ("avocat", "lawyer"),
        ("avocate", "lawyer"),
        ("coiffeur", "hairdresser"),
        ("coiffeuse", "hairdresser"),
        ("comptable", "accountant"),
        ("cuisinier", "chef"),
        ("cuisinière", "chef"),
        ("fermier", "farmer"),
        ("fermière", "farmer"),
        ("homme au foyer", "house-husband"),
        ("femme au foyer", "house-wife"),
        ("homme d'affaires", "businessman"),
        ("femme d'affaires", "businesswoman"),
        ("infirmier", "nurse"),
        ("infirmière", "nurse"),
        ("ingénieur", "engineer"),
        ("ingénieure", "engineer"),
        ("mécanicien", "mechanic"),
        ("mécanicienne", "mechanic"),
        ("médecin", "doctor"),
        ("plombier", "plumber"),
        ("plombière", "plumber"),
        ("professeur", "teacher"),
        ("professeure", "teacher"),
    };

    enum QuizState { NeedEnglish, NeedFrench }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var rng = new Random();

        // Start by testing French -> English first (multiple choice).
        // After correct, require English -> French (typed) before eliminating.
        var remaining = Vocab.ToDictionary(v => v, v => QuizState.NeedEnglish);

        // store text + color for feedback shown with next question
        (string Text, ConsoleColor Color)? lastFeedback = null;
        var total = Vocab.Length;

        PrintBanner();

        while (remaining.Count > 0)
        {
            var order = remaining.Keys.OrderBy(_ => rng.Next()).ToList();

            foreach (var key in order)
            {
                // item may have been removed earlier in this pass
                if (!remaining.TryGetValue(key, out var state))
                    continue;

                // Clear and show banner + last feedback before each question
                Console.Clear();
                PrintBanner();

                // Show progress (learned vs remaining)
                var learned = total - remaining.Count;
                DrawProgressBar(learned, total, 30);

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
                    var choices = Vocab.Select(v => v.English).Distinct().OrderBy(_ => rng.Next()).ToList();
                    if (!choices.Contains(key.English))
                        choices.Add(key.English);

                    choices = choices.Where(c => c != key.English).Take(3).Append(key.English).OrderBy(_ => rng.Next()).ToList();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n🌟 What is the English for \"{key.French}\"? 🌟");
                    Console.ResetColor();
                    for (int i = 0; i < choices.Count; i++)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($" {i + 1}. ");
                        Console.ResetColor();
                        Console.WriteLine($"{choices[i]}");
                    }

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("Pick your answer (1-4): ");
                    Console.ResetColor();
                    var input = Console.ReadLine();
                    if (!int.TryParse(input, out int selected) || selected < 1 || selected > choices.Count)
                    {
                        // invalid - mark as wrong for this round and show correction next question (red)
                        lastFeedback = ($"Invalid choice — correct: {key.English} (French: \"{key.French}\")", ConsoleColor.Red);
                        continue;
                    }

                    if (choices[selected - 1] == key.English)
                    {
                        // don't pause — show congrats (green) with next question
                        lastFeedback = ("Correct! 🎉✨", ConsoleColor.Green);
                        correct = true;
                    }
                    else
                    {
                        // immediate correction shown on next question (red) and include the French being tested
                        lastFeedback = ($"Wrong — correct: {key.English} (French: \"{key.French}\")", ConsoleColor.Red);
                        // counted as wrong this pass
                        continue;
                    }
                }
                else // NeedFrench
                {
                    // Ask for French meaning (free text): English -> French
                    // Include gender hint (male/female) in the prompt
                    var genderHint = GetGenderHint(key.French);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n🌟 Type the French {genderHint} for \"{key.English}\"! 🌟");
                    Console.ResetColor();

                    // Track whether user corrected a shown answer; corrections should not mark the item as learned.
                    bool correctedButNotLearned = false;

                    // First attempt (if correct immediately => learned); otherwise show correct, require typing it, but do not mark learned.
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("Your answer: ");
                    Console.ResetColor();
                    var firstInput = Console.ReadLine()?.Trim() ?? "";

                    if (AreEquivalentFrench(firstInput, key.French))
                    {
                        // success — show congrats (green) with next question and mark as learned
                        lastFeedback = ("Magnifique! You got the French right! 🥳🥐", ConsoleColor.Green);
                        correct = true;
                    }
                    else
                    {
                        // Wrong: show correct answer immediately (red), then require user to type it correctly
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Aww, not quite! The correct answer is \"{key.French}\". 🍬");
                        Console.ResetColor();

                        // Ask user to type the correct form now (this is practice only; does not mark as learned)
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

                    // If correctedButNotLearned is true we intentionally do NOT set correct = true,
                    // so the item remains in 'remaining' and progress doesn't increment.
                }

                // If correct, advance state or remove
                if (correct)
                {
                    if (state == QuizState.NeedEnglish)
                    {
                        // French->English passed; now require English->French
                        remaining[key] = QuizState.NeedFrench;
                    }
                    else
                    {
                        // Both directions passed; remove permanently
                        remaining.Remove(key);
                    }
                }

                // Immediately continue to next question; lastFeedback will be displayed above it
            }
            // Loop continues until remaining is empty; incorrect answers remain unchanged
        }

        // Final clear + banner + celebration
        Console.Clear();
        PrintBanner();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n🌈 All words answered correctly both ways! You're a vocab superstar! 🦄✨");
        Console.ResetColor();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
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
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║         🥐 French Vocabulary Tester! 🥖         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
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