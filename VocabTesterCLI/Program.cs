using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    // Reuse existing small records but repurpose them as:
    // - Subject -> Weather item
    // - Food    -> Place item
    // - Meal    -> Time / adverb item
    record Subject(string French, string English, int VerbGroup); // VerbGroup unused but kept for compatibility
    record Food(string French, string English, int[] Meals);      // Meals field unused for places but kept for compatibility
    record Meal(string French, string English);

    // WEATHER items (previously 'Subjects')
    static readonly Subject[] Weather =
    {
        new Subject("il fait beau",     "it is good weather", 0),
        new Subject("il fait chaud",    "it is hot", 0),
        new Subject("il y a du soleil", "it is sunny", 0),
        new Subject("il fait froid",    "it is cold", 0),
        new Subject("il fait mauvais",  "it is bad weather", 0),
        new Subject("il pleut",         "it rains", 0),
        new Subject("il neige",         "it snows", 0),
    };

    // PLACES items (previously 'Foods')
    static readonly Food[] Places =
    {
        new Food("À la maison",  "At home",     new[] { 0 }),
        new Food("Au collège",   "At school",   new[] { 0 }),
        new Food("Au gymnase",   "At the gym",  new[] { 0 }),
        new Food("À la plage",   "On the beach",new[] { 0 }),
    };

    // TIMES / frequency adverbs (previously 'Meals')
    static readonly Meal[] Times =
    {
        new Meal("D'habitude",  "Usually"),
        new Meal("En général",  "In general"),
        new Meal("Normalement", "Normally"),
        new Meal("Parfois",     "Sometimes"),
    };

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var rng = new Random();

        // Build all vocabulary entries as standalone "sentences".
        // We'll store component indices so distractor generation can vary the same category.
        // tuple: french, english, weatherIdx, placeIdx, timeIdx
        var sentences = new List<(string French, string English, int weatherIdx, int placeIdx, int timeIdx)>();

        // Weather entries
        for (int wi = 0; wi < Weather.Length; wi++)
        {
            var w = Weather[wi];
            sentences.Add(($"{w.French}.", $"{w.English}.", wi, -1, -1));
        }

        // Place entries
        for (int pi = 0; pi < Places.Length; pi++)
        {
            var p = Places[pi];
            sentences.Add(($"{p.French}.", $"{p.English}.", -1, pi, -1));
        }

        // Time / adverb entries
        for (int ti = 0; ti < Times.Length; ti++)
        {
            var t = Times[ti];
            sentences.Add(($"{t.French}.", $"{t.English}.", -1, -1, ti));
        }

        var learnedWeather = new HashSet<int>();
        var learnedPlaces = new HashSet<int>();
        var learnedTimes = new HashSet<int>();

        Console.WriteLine();

        // Initial draw before the first question
        RedrawScreen(learnedWeather.Count, Weather.Length,
                     learnedPlaces.Count, Places.Length,
                     learnedTimes.Count, Times.Length);

        var pool = sentences.OrderBy(_ => rng.Next()).ToList(); // randomized pool to pull from

        while (learnedWeather.Count < Weather.Length ||
               learnedPlaces.Count < Places.Length ||
               learnedTimes.Count < Times.Length)
        {
            // Always clear and redraw the screen before each question
            RedrawScreen(learnedWeather.Count, Weather.Length,
                         learnedPlaces.Count, Places.Length,
                         learnedTimes.Count, Times.Length);

            var candidate = pool.FirstOrDefault(s =>
                (s.weatherIdx >= 0 && !learnedWeather.Contains(s.weatherIdx)) ||
                (s.placeIdx   >= 0 && !learnedPlaces.Contains(s.placeIdx)) ||
                (s.timeIdx    >= 0 && !learnedTimes.Contains(s.timeIdx)));

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
                if (candidate.weatherIdx >= 0) learnedWeather.Add(candidate.weatherIdx);
                if (candidate.placeIdx   >= 0) learnedPlaces.Add(candidate.placeIdx);
                if (candidate.timeIdx    >= 0) learnedTimes.Add(candidate.timeIdx);

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
                RedrawScreen(learnedWeather.Count, Weather.Length,
                             learnedPlaces.Count, Places.Length,
                             learnedTimes.Count, Times.Length);
            }

            DrawComponentProgress(learnedWeather.Count, Weather.Length,
                                  learnedPlaces.Count, Places.Length,
                                  learnedTimes.Count, Times.Length);

            // small pause so user sees result before next redraw (optional)
            System.Threading.Thread.Sleep(650);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nAll weather, places and times items have been tested (answered correctly) — well done!");
        Console.ResetColor();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    // Draws the screen header + the three progress bars
    static void RedrawScreen(int learnedWeather, int totalWeather, int learnedPlaces, int totalPlaces, int learnedTimes, int totalTimes)
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
        DrawComponentProgress(learnedWeather, totalWeather, learnedPlaces, totalPlaces, learnedTimes, totalTimes);
    }

    // Builds 4 choices that are deliberately similar.
    // If the candidate is a single-category item (weather/place/time) produce distractors from the same category.
    static List<string> BuildSimilarChoices((string French, string English, int weatherIdx, int placeIdx, int timeIdx) item, Random rng)
    {
        var correct = item.English;
        var choices = new HashSet<string> { correct };

        // If this is a weather item
        if (item.weatherIdx >= 0 && item.placeIdx < 0 && item.timeIdx < 0)
        {
            var pool = Enumerable.Range(0, Weather.Length).Where(i => i != item.weatherIdx).OrderBy(_ => rng.Next()).ToList();
            foreach (var i in pool)
            {
                if (choices.Count >= 4) break;
                choices.Add($"{Weather[i].English}.");
            }
        }
        // If this is a place item
        else if (item.placeIdx >= 0 && item.weatherIdx < 0 && item.timeIdx < 0)
        {
            var pool = Enumerable.Range(0, Places.Length).Where(i => i != item.placeIdx).OrderBy(_ => rng.Next()).ToList();
            foreach (var i in pool)
            {
                if (choices.Count >= 4) break;
                choices.Add($"{Places[i].English}.");
            }
        }
        // If this is a time/adverb item
        else if (item.timeIdx >= 0 && item.weatherIdx < 0 && item.placeIdx < 0)
        {
            var pool = Enumerable.Range(0, Times.Length).Where(i => i != item.timeIdx).OrderBy(_ => rng.Next()).ToList();
            foreach (var i in pool)
            {
                if (choices.Count >= 4) break;
                choices.Add($"{Times[i].English}.");
            }
        }
        else
        {
            // Fallback: if somehow multiple components are present, vary one of them
            var attempts = new[] { 0, 1, 2 }.OrderBy(_ => rng.Next()).ToList();
            foreach (var attempt in attempts)
            {
                if (choices.Count >= 4) break;

                if (attempt == 0 && item.weatherIdx >= 0)
                {
                    var pool = Enumerable.Range(0, Weather.Length).Where(i => i != item.weatherIdx).OrderBy(_ => rng.Next()).ToList();
                    foreach (var i in pool)
                    {
                        if (choices.Count >= 4) break;
                        choices.Add(FormatEnglish(i, item.placeIdx, item.timeIdx));
                    }
                }
                else if (attempt == 1 && item.placeIdx >= 0)
                {
                    var pool = Enumerable.Range(0, Places.Length).Where(i => i != item.placeIdx).OrderBy(_ => rng.Next()).ToList();
                    foreach (var i in pool)
                    {
                        if (choices.Count >= 4) break;
                        choices.Add(FormatEnglish(item.weatherIdx, i, item.timeIdx));
                    }
                }
                else if (attempt == 2 && item.timeIdx >= 0)
                {
                    var pool = Enumerable.Range(0, Times.Length).Where(i => i != item.timeIdx).OrderBy(_ => rng.Next()).ToList();
                    foreach (var i in pool)
                    {
                        if (choices.Count >= 4) break;
                        choices.Add(FormatEnglish(item.weatherIdx, item.placeIdx, i));
                    }
                }
            }
        }

        // Fill remaining slots by sampling same-category items (safe fallback)
        var fillAttempts = 0;
        while (choices.Count < 4 && fillAttempts++ < 200)
        {
            if (item.weatherIdx >= 0)
            {
                var i = rng.Next(Weather.Length);
                choices.Add($"{Weather[i].English}.");
            }
            else if (item.placeIdx >= 0)
            {
                var i = rng.Next(Places.Length);
                choices.Add($"{Places[i].English}.");
            }
            else if (item.timeIdx >= 0)
            {
                var i = rng.Next(Times.Length);
                choices.Add($"{Times[i].English}.");
            }
            else
            {
                // last resort
                choices.Add("...");
            }
        }

        return choices.OrderBy(_ => rng.Next()).ToList();
    }

    static string FormatEnglish(int weatherIdx, int placeIdx, int timeIdx)
    {
        var parts = new List<string>();
        if (weatherIdx >= 0) parts.Add(Weather[weatherIdx].English);
        if (placeIdx   >= 0) parts.Add(Places[placeIdx].English);
        if (timeIdx    >= 0) parts.Add(Times[timeIdx].English);
        var sentence = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (!sentence.EndsWith(".")) sentence += ".";
        return sentence;
    }

    static void DrawComponentProgress(int learnedWeather, int totalWeather, int learnedPlaces, int totalPlaces, int learnedTimes, int totalTimes)
    {
        // Render three ASCII progress bars (Weather, Places, Times)
        Console.WriteLine();
        DrawProgressBar("Weather:", learnedWeather, totalWeather, 24);
        DrawProgressBar("Places:",  learnedPlaces, totalPlaces, 24);
        DrawProgressBar("Times:",   learnedTimes,  totalTimes,  24);
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
}