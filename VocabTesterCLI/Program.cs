using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    record Noun(string French, string English, char Gender, bool IsPlural);

    static readonly Noun[] Nouns = new[]
    {
        new Noun("le café", "coffee", 'm', false),
        new Noun("le chocolat", "chocolate", 'm', false),
        new Noun("le fromage", "cheese", 'm', false),
        new Noun("le jus de fruits", "fruit juice", 'm', false),
        new Noun("le lait", "milk", 'm', false),
        new Noun("le miel", "honey", 'm', false),
        new Noun("le pain", "bread", 'm', false),
        new Noun("le poisson", "fish", 'm', false),
        new Noun("le poulet rôti", "roast chicken", 'm', false),
        new Noun("le riz", "rice", 'm', false),
        new Noun("l'eau", "water", 'f', false),
        new Noun("la confiture", "jam", 'f', false),
        new Noun("la salade verte", "green salad", 'f', false),
        new Noun("la viande", "meat", 'f', false),
        new Noun("les aliments", "food", 'm', true),
        new Noun("les frites", "fries", 'f', true),
        new Noun("les bananes", "bananas", 'f', true),
        new Noun("les pommes", "apples", 'f', true),
        new Noun("les tomates", "tomatoes", 'f', true),
        new Noun("les crevettes", "prawns", 'f', true),
    };

    static readonly (string French, string English)[] Subjects =
    {
        ("J'adore", "I love"),
        ("J'aime", "I like"),
        ("Je préfère", "I prefer"),
        ("Je n'aime pas", "I don't like"),
        ("Je déteste", "I hate")
    };

    static readonly (string French, string English)[] Adjectives =
    {
        ("délicieux", "delicious"),
        ("savoureux", "tasty"),
        ("sain", "healthy"),
        ("dégoûtant", "disgusting"),
        ("malsain", "unhealthy"),
        ("sucré", "sweet"),
        ("gras", "fatty"),
        ("épicé", "spicy"),
        ("riche en protéines", "rich in protein")
    };

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var rng = new Random();

        // Build all possible sentences and keep component indices so we can craft similar distractors
        var sentences = new List<(string French, string English, int subjIdx, int nounIdx, int adjIdx)>();
        for (int si = 0; si < Subjects.Length; si++)
        {
            for (int ni = 0; ni < Nouns.Length; ni++)
            {
                for (int ai = 0; ai < Adjectives.Length; ai++)
                {
                    var subj = Subjects[si];
                    var noun = Nouns[ni];
                    var adj = Adjectives[ai];

                    string french;
                    if (!noun.IsPlural)
                        french = $"{subj.French} {noun.French} parce que c'est {adj.French}";
                    else
                    {
                        var pron = noun.Gender == 'f' ? "parce qu'elles sont" : "parce qu'ils sont";
                        french = $"{subj.French} {noun.French} {pron} {adj.French}";
                    }

                    string becauseClause = (!noun.IsPlural) ? $"because it is {adj.English}" : $"because they are {adj.English}";
                    var english = $"{subj.English} {noun.English} {becauseClause}";

                    sentences.Add((french, english, si, ni, ai));
                }
            }
        }

        // Quiz subset
        var quiz = sentences.OrderBy(_ => rng.Next()).Take(20).ToList();
        var remaining = quiz.Select(s => s).ToList();
        var total = remaining.Count;

        Console.WriteLine();
        PrintBanner();

        while (remaining.Count > 0)
        {
            var roundOrder = remaining.OrderBy(_ => rng.Next()).ToList();

            foreach (var item in roundOrder)
            {
                var choiceList = BuildSimilarChoices(item, rng);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\nTranslate into English:\n  {item.French}");
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
                }
                else if (choiceList[selected - 1] == item.English)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Correct!\n");
                    Console.ResetColor();
                    remaining.Remove(item);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Wrong — correct: {item.English}\n");
                    Console.ResetColor();
                }

                var learned = total - remaining.Count;
                DrawProgressBar(learned, total, 30);
            }

            if (remaining.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"\nNext round: {remaining.Count} sentence(s) to retry.\n");
                Console.ResetColor();
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nAll sentences translated correctly — well done!");
        Console.ResetColor();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    // Builds 4 choices that are deliberately similar:
    // - pick one component to vary (subject, noun or adjective)
    // - keep the other two components identical for all options
    // If the chosen component doesn't have enough alternatives, choose a different component.
    static List<string> BuildSimilarChoices((string French, string English, int subjIdx, int nounIdx, int adjIdx) item, Random rng)
    {
        var correct = item.English;
        var choices = new HashSet<string> { correct };

        // candidate component order to attempt (randomized)
        var attempts = new[] { 0, 1, 2 }.OrderBy(_ => rng.Next()).ToList();
        // 0 = vary adjective, 1 = vary noun, 2 = vary subject

        foreach (var attempt in attempts)
        {
            if (choices.Count >= 4) break;

            if (attempt == 0)
            {
                // vary adjective: keep subjIdx & nounIdx fixed
                var pool = Enumerable.Range(0, Adjectives.Length).Where(ai => ai != item.adjIdx).ToList();
                if (pool.Count >= 3) // enough alternatives
                {
                    var picks = pool.OrderBy(_ => rng.Next()).Take(3).ToList();
                    foreach (var ai in picks)
                        choices.Add(FormatEnglish(item.subjIdx, item.nounIdx, ai));
                }
            }
            else if (attempt == 1)
            {
                // vary noun: keep subjIdx & adjIdx fixed; require same plurality so "it/they" matches
                var pool = Enumerable.Range(0, Nouns.Length)
                    .Where(ni => ni != item.nounIdx && Nouns[ni].IsPlural == Nouns[item.nounIdx].IsPlural)
                    .ToList();
                if (pool.Count >= 3)
                {
                    var picks = pool.OrderBy(_ => rng.Next()).Take(3).ToList();
                    foreach (var ni in picks)
                        choices.Add(FormatEnglish(item.subjIdx, ni, item.adjIdx));
                }
                else if (pool.Count > 0)
                {
                    // add whatever we can from pool to make distractors closer, we'll fill later
                    foreach (var ni in pool.OrderBy(_ => rng.Next()))
                    {
                        if (choices.Count >= 4) break;
                        choices.Add(FormatEnglish(item.subjIdx, ni, item.adjIdx));
                    }
                }
            }
            else // attempt == 2
            {
                // vary subject: keep nounIdx & adjIdx fixed; prefer same polarity group
                var positive = new[] { 0, 1, 2 }; // indices of positive subjects
                var negative = new[] { 3, 4 };
                var subjGroup = positive.Contains(item.subjIdx) ? positive : negative;
                var pool = subjGroup.Where(si => si != item.subjIdx).ToList();
                if (pool.Count >= 3)
                {
                    foreach (var si in pool.OrderBy(_ => rng.Next()).Take(3))
                        choices.Add(FormatEnglish(si, item.nounIdx, item.adjIdx));
                }
                else if (pool.Count > 0)
                {
                    foreach (var si in pool.OrderBy(_ => rng.Next()))
                    {
                        if (choices.Count >= 4) break;
                        choices.Add(FormatEnglish(si, item.nounIdx, item.adjIdx));
                    }
                }
            }
        }

        // If we still don't have 4 choices, fill with constrained random candidates
        var fillAttempts = 0;
        while (choices.Count < 4 && fillAttempts < 50)
        {
            fillAttempts++;
            // prefer changing only one component relative to correct: randomly pick which to change
            var comp = rng.Next(3);
            int si = item.subjIdx, ni = item.nounIdx, ai = item.adjIdx;
            if (comp == 0) ai = rng.Next(Adjectives.Length);
            else if (comp == 1)
            {
                // choose noun with same plurality
                var pool = Enumerable.Range(0, Nouns.Length).Where(i => Nouns[i].IsPlural == Nouns[item.nounIdx].IsPlural).ToList();
                ni = pool[rng.Next(pool.Count)];
            }
            else
            {
                // choose subject from same polarity group if possible
                var positive = new[] { 0, 1, 2 };
                var negative = new[] { 3, 4 };
                var group = positive.Contains(item.subjIdx) ? positive : negative;
                si = group.OrderBy(_ => rng.Next()).First();
            }

            var candidate = FormatEnglish(si, ni, ai);
            if (candidate != correct) choices.Add(candidate);
        }

        // Final shuffle and return
        return choices.OrderBy(_ => rng.Next()).ToList();
    }

    static string FormatEnglish(int subjIdx, int nounIdx, int adjIdx)
    {
        var subj = Subjects[subjIdx].English;
        var noun = Nouns[nounIdx];
        var adj = Adjectives[adjIdx].English;
        var because = noun.IsPlural ? "because they are" : "because it is";
        return $"{subj} {noun.English} {because} {adj}";
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
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║     French sentence → English multiple choice   ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine("Translate the French sentence shown into natural English.\n");
    }
}