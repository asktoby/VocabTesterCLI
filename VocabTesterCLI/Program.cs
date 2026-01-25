using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    // Reuse existing small records but repurpose them as:
    // - Subject -> When/Weather phrases (Quand ...)
    // - Food    -> Conjugation / pronoun forms
    // - Meal    -> Activities / places / frequency expressions
    record Subject(string French, string English, int VerbGroup); // VerbGroup unused but kept for compatibility
    record Food(string French, string English, int[] Meals);      // Meals field unused for places but kept for compatibility
    record Meal(string French, string English);

    // WHEN / weather phrases (previously 'Weather')
    static readonly Subject[] WhenPhrases =
    {
        new Subject("Quand le ciel est dégagé", "When the sky is clear", 0),
        new Subject("Quand il y a des nuages", "When it is cloudy", 0),
        new Subject("Quand il fait beau", "When it is good weather", 0),
        new Subject("Quand il fait chaud", "When it is hot", 0),
        new Subject("Quand il fait froid", "When it is cold", 0),
        new Subject("Quand il fait mauvais", "When it is bad weather", 0),
        new Subject("Quand il y a du soleil", "When it is sunny", 0),
        new Subject("Quand il y a du vent", "When it is windy", 0),
        new Subject("Quand il y a du brouillard", "When it is foggy", 0),
        new Subject("Quand il y a de l'orage", "When it is stormy", 0),
        new Subject("Quand il pleut", "When it rains", 0),
        new Subject("Quand il neige", "When it snows", 0),
        new Subject("Pendant la semaine", "During the week", 0),
        new Subject("Le week-end", "At the weekend", 0),
    };

    // CONJUGATIONS / pronouns (previously 'Places')
    static readonly Food[] Conjugations =
    {
        // jouer
        new Food("Je joue", "I play", new[] { 0 }),
        new Food("Tu joues", "You play", new[] { 0 }),
        new Food("Il joue", "He plays", new[] { 0 }),
        new Food("Elle joue", "She plays", new[] { 0 }),
        new Food("On joue", "One plays", new[] { 0 }),
        new Food("Nous jouons", "We play", new[] { 0 }),
        new Food("Vous jouez", "You all play", new[] { 0 }),
        new Food("Ils jouent", "They (m) play", new[] { 0 }),
        new Food("Elles jouent", "They (f) play", new[] { 0 }),

        // faire
        new Food("Je fais", "I do", new[] { 0 }),
        new Food("Tu fais", "You do", new[] { 0 }),
        new Food("Il fait", "He does", new[] { 0 }),
        new Food("Elle fait", "She does", new[] { 0 }),
        new Food("On fait", "One does", new[] { 0 }),
        new Food("Nous faisons", "We do", new[] { 0 }),
        new Food("Vous faites", "You all do", new[] { 0 }),
        new Food("Ils font", "They (m) do", new[] { 0 }),
        new Food("Elles font", "They (f) do", new[] { 0 }),

        // aller
        new Food("Je vais", "I go", new[] { 0 }),
        new Food("Tu vas", "You go", new[] { 0 }),
        new Food("Il va", "He goes", new[] { 0 }),
        new Food("Elle va", "She goes", new[] { 0 }),
        new Food("On va", "One goes", new[] { 0 }),
        new Food("Nous allons", "We go", new[] { 0 }),
        new Food("Vous allez", "You all go", new[] { 0 }),
        new Food("Ils vont", "They (m) go", new[] { 0 }),
        new Food("Elles vont", "They (f) go", new[] { 0 }),

        // rester (short set)
        new Food("Je reste", "I stay", new[] { 0 }),
        new Food("Tu restes", "You stay", new[] { 0 }),
        new Food("Mon ami reste", "My friend (m) stays", new[] { 0 }),
        new Food("Mon amie reste", "My friend (f) stays", new[] { 0 }),
    };

    // ACTIVITIES / places / instruments / frequency expressions (previously 'Times')
    static readonly Meal[] Vocab =
    {
        // sports / pastimes
        new Meal("au basket", "to/at basketball"),
        new Meal("au foot", "to/at football"),
        new Meal("au tennis", "to/at tennis"),
        new Meal("aux cartes", "cards"),
        new Meal("aux échecs", "chess"),
        new Meal("avec des amis", "with some friends"),

        // activities (faire)
        new Meal("du footing", "jogging"),
        new Meal("du ski", "skiing"),
        new Meal("du sport", "sport"),
        new Meal("du vélo", "cycling"),
        new Meal("de l'équitation", "horse riding"),
        new Meal("de l'escalade", "climbing"),
        new Meal("de la musculation", "weight training"),
        new Meal("de la natation", "swimming"),
        new Meal("de la randonnée", "hiking"),
        new Meal("les devoirs", "homework"),

        // instruments
        new Meal("de la batterie", "the drums"),
        new Meal("du clavier", "the keyboard"),
        new Meal("de la guitare", "the guitar"),
        new Meal("du piano", "the piano"),

        // places
        new Meal("au centre commercial", "to the shopping centre"),
        new Meal("au centre sportif", "to the sports centre"),
        new Meal("au gymnase", "to the gym"),
        new Meal("au parc", "to the park"),
        new Meal("à la montagne", "to the mountains"),
        new Meal("à la pêche", "fishing"),
        new Meal("à la piscine", "to the swimming pool"),
        new Meal("à la plage", "to the beach"),
        new Meal("chez des amis", "to friends' houses"),

        // frequency/time expressions
        new Meal("de temps en temps", "from time to time"),
        new Meal("une fois par semaine", "once a week"),
        new Meal("deux fois par semaine", "twice a week"),
        new Meal("une fois par mois", "once a month"),
        new Meal("deux fois par mois", "twice a month"),
        new Meal("une fois par an", "once a year"),
        new Meal("tous les jours", "every day"),
        new Meal("tous les samedis", "every Saturday"),
        new Meal("tous les soirs", "every evening"),
        new Meal("tous les week-ends", "every weekend"),
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

        // When / weather entries
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

        // Vocab / activities / places / frequency entries
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