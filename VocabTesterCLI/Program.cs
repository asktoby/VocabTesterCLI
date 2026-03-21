using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    // Reuse existing small records but repurpose them as:
    // - Subject -> Sentence starters (e.g., "J'aime", "Ma pièce préférée est")
    // - Food    -> Verb phrases (e.g., "me détendre")
    // - Meal    -> Places / prepositional phrases
    record Subject(string French, string English, int VerbGroup); // VerbGroup unused but kept for compatibility
    record Food(string French, string English, int[] Meals);      // Meals field unused for places but kept for compatibility
    record Meal(string French, string English);

    // Sentence starters / prompts
    static readonly Subject[] WhenPhrases =
    {
        new Subject("Ma pièce préférée est", "My favourite room is", 0),
        new Subject("Mon endroit préféré est", "My favourite place is", 0),
        new Subject("J'aime", "I like", 0),
        new Subject("Je n'aime pas", "I don't like", 0),
    };

    // Verb phrases that pair with starters (infinitives/inflected where needed)
    static readonly Food[] Conjugations =
    {
        new Food("me détendre", "to relax", new[] { 0 }),
        new Food("me reposer", "to rest", new[] { 0 }),
        new Food("travailler", "to work", new[] { 0 }),
        new Food("lire", "to read", new[] { 0 }),
        new Food("passer du temps", "to spend time", new[] { 0 }),
    };

    // Places / prepositional phrases and plain nouns for "is" sentences
    static readonly Meal[] Vocab =
    {
        new Meal("ma chambre", "my bedroom"),
        new Meal("dans ma chambre", "in my bedroom"),

        new Meal("la cuisine", "the kitchen"),
        new Meal("dans la cuisine", "in the kitchen"),

        new Meal("le jardin", "the garden"),
        new Meal("dans le jardin", "in the garden"),

        new Meal("la salle de bains", "the bathroom"),
        new Meal("dans la salle de bains", "in the bathroom"),

        new Meal("la salle à manger", "the dining room"),
        new Meal("dans la salle à manger", "in the dining room"),

        new Meal("le salon", "the living room"),
        new Meal("dans le salon", "in the living room"),

        new Meal("la terrasse", "the terrace"),
        new Meal("sur la terrasse", "on the terrace"),
    };

    // Timer state
    static DateTime s_endTime;
    static volatile bool s_timeUp;
    static readonly object s_consoleLock = new();
    static System.Threading.Timer? s_displayTimer;

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var rng = new Random();

        // Start 20-minute timer
        s_endTime = DateTime.UtcNow.AddMinutes(20);
        s_timeUp = false;

        // Background display timer updates the visible countdown once per second.
        s_displayTimer = new System.Threading.Timer(_ =>
        {
            var remaining = s_endTime - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                s_timeUp = true;
                remaining = TimeSpan.Zero;
            }

            UpdateTimerDisplay(remaining);
        }, null, 0, 1000);

        // Build all vocabulary entries as standalone "sentences".
        // We'll store component indices so distractor generation can vary the same category.
        // tuple: french, english, whenIdx, conjugationIdx, vocabIdx
        var sentences = new List<(string French, string English, int whenIdx, int conjugationIdx, int vocabIdx)>();

        // When / sentence-starter entries
        for (int wi = 0; wi < WhenPhrases.Length; wi++)
        {
            var w = WhenPhrases[wi];
            sentences.Add(($"{w.French}.", $"{w.English}.", wi, -1, -1));
        }

        // Conjugation entries
        for (int pi = 0; pi < Conjugations.Length; pi++)
        {
            var p = Conjugations[pi];
            sentences.Add(($"{p.French}.", $"{p.English}.", -1, pi, -1));
        }

        // Vocab / places entries
        for (int ti = 0; ti < Vocab.Length; ti++)
        {
            var t = Vocab[ti];
            sentences.Add(($"{t.French}.", $"{t.English}.", -1, -1, ti));
        }

        var learnedWhen = new HashSet<int>();
        var learnedConjugations = new HashSet<int>();
        var learnedVocab = new HashSet<int>();

        Console.WriteLine();

        // Initial draw before the first question
        RedrawScreen(learnedWhen.Count, WhenPhrases.Length,
                     learnedConjugations.Count, Conjugations.Length,
                     learnedVocab.Count, Vocab.Length);

        var pool = sentences.OrderBy(_ => rng.Next()).ToList(); // randomized pool to pull from

        while (!s_timeUp && (
               learnedWhen.Count < WhenPhrases.Length ||
               learnedConjugations.Count < Conjugations.Length ||
               learnedVocab.Count < Vocab.Length))
        {
            // Always clear and redraw the screen before each question
            RedrawScreen(learnedWhen.Count, WhenPhrases.Length,
                         learnedConjugations.Count, Conjugations.Length,
                         learnedVocab.Count, Vocab.Length);

            var candidate = pool.FirstOrDefault(s =>
                (s.whenIdx >= 0 && !learnedWhen.Contains(s.whenIdx)) ||
                (s.conjugationIdx   >= 0 && !learnedConjugations.Contains(s.conjugationIdx)) ||
                (s.vocabIdx    >= 0 && !learnedVocab.Contains(s.vocabIdx)));

            if (candidate.Equals(default))
            {
                candidate = pool[rng.Next(pool.Count)];
            }

            var choiceList = BuildSimilarChoices(candidate, rng);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nTranslate into English:\n  {candidate.French}");
            Console.ResetColor();

            for (int i = 0; i < choiceList.Count; i++)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($" {i + 1}. ");
                Console.ResetColor();
                Console.WriteLine(choiceList[i]);
            }

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("Pick your answer (1-4): ");
            Console.ResetColor();
            var input = Console.ReadLine();

            // If time expired while waiting for input, break immediately.
            if (s_timeUp)
            {
                break;
            }

            if (!int.TryParse(input, out var selected) || selected < 1 || selected > choiceList.Count)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid choice — counted as wrong.");
                Console.ResetColor();

                // Keep behavior: treat as wrong but do not pause for review.
                pool.Remove(candidate);
                pool.Add(candidate);
            }
            else if (choiceList[selected - 1] == candidate.English)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Correct!\n");
                Console.ResetColor();

                // Mark learned item in the right category
                if (candidate.whenIdx >= 0) learnedWhen.Add(candidate.whenIdx);
                if (candidate.conjugationIdx   >= 0) learnedConjugations.Add(candidate.conjugationIdx);
                if (candidate.vocabIdx    >= 0) learnedVocab.Add(candidate.vocabIdx);

                pool.Remove(candidate);
            }
            else
            {
                // WRONG — keep the question and both the chosen wrong answer and the correct answer on screen
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Wrong — correct: {candidate.English}");
                Console.ResetColor();

                // Show what the user answered (for clarity) and highlight it
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"You answered: {choiceList[selected - 1]}\n");
                Console.ResetColor();

                // Keep the candidate in the pool for future testing
                pool.Remove(candidate);
                pool.Add(candidate);

                // Wait for the user to study the mistake, then clear and redraw immediately after they press Enter
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();

                // Clear and redraw the screen now
                RedrawScreen(learnedWhen.Count, WhenPhrases.Length,
                             learnedConjugations.Count, Conjugations.Length,
                             learnedVocab.Count, Vocab.Length);
            }

            DrawComponentProgress(learnedWhen.Count, WhenPhrases.Length,
                                  learnedConjugations.Count, Conjugations.Length,
                                  learnedVocab.Count, Vocab.Length);

            // small pause so user sees result before next redraw (optional)
            System.Threading.Thread.Sleep(650);
        }

        // Stop the display timer
        s_displayTimer?.Dispose();

        Console.ForegroundColor = ConsoleColor.Green;
        if (s_timeUp)
        {
            Console.WriteLine("\nTime is up — the test has ended.");
        }
        else
        {
            Console.WriteLine("\nAll items have been tested (answered correctly) — well done!");
        }
        Console.ResetColor();

        // Final progress snapshot
        DrawComponentProgress(learnedWhen.Count, WhenPhrases.Length,
                              learnedConjugations.Count, Conjugations.Length,
                              learnedVocab.Count, Vocab.Length);

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    // Draws the screen header + the three progress bars
    static void RedrawScreen(int learnedWhen, int totalWhen, int learnedConjugations, int totalConjugations, int learnedVocab, int totalVocab)
    {
        try
        {
            Console.Clear();
        }
        catch
        {
            // Some hosts (rare) may not support Clear; ignore failures and continue
        }

        PrintBanner();
        DrawComponentProgress(learnedWhen, totalWhen, learnedConjugations, totalConjugations, learnedVocab, totalVocab);
    }

    // Builds 4 choices that are deliberately similar.
    // If the candidate is a single-category item produce distractors from the same category.
    static List<string> BuildSimilarChoices((string French, string English, int whenIdx, int conjugationIdx, int vocabIdx) item, Random rng)
    {
        var correct = item.English;
        var choices = new HashSet<string> { correct };

        // If this is a when item
        if (item.whenIdx >= 0 && item.conjugationIdx < 0 && item.vocabIdx < 0)
        {
            var pool = Enumerable.Range(0, WhenPhrases.Length).Where(i => i != item.whenIdx).OrderBy(_ => rng.Next()).ToList();
            foreach (var i in pool)
            {
                if (choices.Count >= 4) break;
                choices.Add($"{WhenPhrases[i].English}.");
            }
        }
        // If this is a conjugation item
        else if (item.conjugationIdx >= 0 && item.whenIdx < 0 && item.vocabIdx < 0)
        {
            var pool = Enumerable.Range(0, Conjugations.Length).Where(i => i != item.conjugationIdx).OrderBy(_ => rng.Next()).ToList();
            foreach (var i in pool)
            {
                if (choices.Count >= 4) break;
                choices.Add($"{Conjugations[i].English}.");
            }
        }
        // If this is a vocab item
        else if (item.vocabIdx >= 0 && item.whenIdx < 0 && item.conjugationIdx < 0)
        {
            var pool = Enumerable.Range(0, Vocab.Length).Where(i => i != item.vocabIdx).OrderBy(_ => rng.Next()).ToList();
            foreach (var i in pool)
            {
                if (choices.Count >= 4) break;
                choices.Add($"{Vocab[i].English}.");
            }
        }
        else
        {
            // Fallback: if somehow multiple components are present, vary one of them
            var attempts = new[] { 0, 1, 2 }.OrderBy(_ => rng.Next()).ToList();
            foreach (var attempt in attempts)
            {
                if (choices.Count >= 4) break;

                if (attempt == 0 && item.whenIdx >= 0)
                {
                    var pool = Enumerable.Range(0, WhenPhrases.Length).Where(i => i != item.whenIdx).OrderBy(_ => rng.Next()).ToList();
                    foreach (var i in pool)
                    {
                        if (choices.Count >= 4) break;
                        choices.Add(FormatEnglish(i, item.conjugationIdx, item.vocabIdx));
                    }
                }
                else if (attempt == 1 && item.conjugationIdx >= 0)
                {
                    var pool = Enumerable.Range(0, Conjugations.Length).Where(i => i != item.conjugationIdx).OrderBy(_ => rng.Next()).ToList();
                    foreach (var i in pool)
                    {
                        if (choices.Count >= 4) break;
                        choices.Add(FormatEnglish(item.whenIdx, i, item.vocabIdx));
                    }
                }
                else if (attempt == 2 && item.vocabIdx >= 0)
                {
                    var pool = Enumerable.Range(0, Vocab.Length).Where(i => i != item.vocabIdx).OrderBy(_ => rng.Next()).ToList();
                    foreach (var i in pool)
                    {
                        if (choices.Count >= 4) break;
                        choices.Add(FormatEnglish(item.whenIdx, item.conjugationIdx, i));
                    }
                }
            }
        }

        // Fill remaining slots by sampling same-category items (safe fallback)
        var fillAttempts = 0;
        while (choices.Count < 4 && fillAttempts++ < 200)
        {
            if (item.whenIdx >= 0)
            {
                var i = rng.Next(WhenPhrases.Length);
                choices.Add($"{WhenPhrases[i].English}.");
            }
            else if (item.conjugationIdx >= 0)
            {
                var i = rng.Next(Conjugations.Length);
                choices.Add($"{Conjugations[i].English}.");
            }
            else if (item.vocabIdx >= 0)
            {
                var i = rng.Next(Vocab.Length);
                choices.Add($"{Vocab[i].English}.");
            }
            else
            {
                // last resort
                choices.Add("...");
            }
        }

        return choices.OrderBy(_ => rng.Next()).ToList();
    }

    static string FormatEnglish(int whenIdx, int conjugationIdx, int vocabIdx)
    {
        var parts = new List<string>();
        if (whenIdx >= 0) parts.Add(WhenPhrases[whenIdx].English);
        if (conjugationIdx   >= 0) parts.Add(Conjugations[conjugationIdx].English);
        if (vocabIdx    >= 0) parts.Add(Vocab[vocabIdx].English);
        var sentence = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (!sentence.EndsWith(".")) sentence += ".";
        return sentence;
    }

    static void DrawComponentProgress(int learnedWhen, int totalWhen, int learnedConjugations, int totalConjugations, int learnedVocab, int totalVocab)
    {
        // Render three ASCII progress bars (When / Conjugations / Vocabulary)
        Console.WriteLine();
        DrawProgressBar("When:", learnedWhen, totalWhen, 24);
        DrawProgressBar("Conjugations:",  learnedConjugations, totalConjugations, 24);
        DrawProgressBar("Vocabulary:",   learnedVocab,  totalVocab,  24);
        Console.WriteLine();
    }

    // Helper to draw a single labeled ASCII progress bar with optional color
    static void DrawProgressBar(string label, int learned, int total, int width = 20)
    {
        if (total <= 0)
        {
            Console.WriteLine($"{label.PadRight(12)} [no items]");
            return;
        }

        learned = Math.Clamp(learned, 0, total);
        double ratio = (double)learned / total;
        int filled = (int)Math.Round(ratio * width);

        // Label padded for alignment
        Console.Write(label.PadRight(12));
        Console.Write(" [");

        // Filled portion (green)
        if (filled > 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(new string('#', filled));
        }

        // Unfilled portion (dark gray)
        if (filled < width)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('-', width - filled));
        }

        Console.ResetColor();
        Console.Write($"] {learned}/{total}\n");
    }

    static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║     French vocabulary → English multiple choice ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine("Translate the French item shown into natural English.\n");
    }

    // Updates the small countdown display in the banner area without disturbing user input (best-effort).
    static void UpdateTimerDisplay(TimeSpan remaining)
    {
        lock (s_consoleLock)
        {
            try
            {
                // Remember cursor
                int curLeft = Console.CursorLeft;
                int curTop = Console.CursorTop;

                // Choose a row near the top for the timer (row 1 is inside the banner area).
                int timerRow = 1;

                // Compute a right-aligned position for the timer text
                var timerText = $"Time left: {remaining:mm\\:ss}";
                int col = Math.Max(0, Console.WindowWidth - timerText.Length - 1);

                if (timerRow < Console.BufferHeight)
                {
                    Console.SetCursorPosition(col, timerRow);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(timerText);
                    Console.ResetColor();
                }

                // Restore cursor (best-effort; may throw if console size changed)
                if (curTop < Console.BufferHeight && curLeft < Console.BufferWidth)
                {
                    Console.SetCursorPosition(curLeft, curTop);
                }
            }
            catch
            {
                // If console does not support cursor ops in this host, ignore silently.
            }
        }
    }
}