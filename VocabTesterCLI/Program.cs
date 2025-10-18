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

        // Build all possible sentences from the sentence builder parts
        var sentences = new List<(string French, string English)>();
        foreach (var subj in Subjects)
        foreach (var noun in Nouns)
        foreach (var adj in Adjectives)
        {
            // French sentence
            string french;
                  if (!noun.IsPlural)
                french = $"{subj.French} {noun.French} parce que c'est {adj.French}";
            else
            {
                var pron = noun.Gender == 'f' ? "parce qu'elles sont" : "parce qu'ils sont";
                french = $"{subj.French} {noun.French} {pron} {adj.French}";
            }

            // English sentence
            string englishSubject = subj.English;
            string englishNoun = noun.English;
            string becauseClause = (!noun.IsPlural) ? $"because it is {adj.English}" : $"because they are {adj.English}";
            var english = $"{englishSubject} {englishNoun} {becauseClause}";

            sentences.Add((French: french, English: english));
        }

        // We'll quiz on a subset to keep the session reasonable. Pick a shuffled subset (e.g., 20).
        sentences = sentences.OrderBy(_ => rng.Next()).Take(20).ToList();

        // remaining to learn (mutated immediately when answered correctly)
        var remaining = sentences.ToList();
        var totalSentences = remaining.Count;

        Console.WriteLine();
        PrintBanner();

        while (remaining.Count > 0)
        {
            var roundOrder = remaining.OrderBy(_ => rng.Next()).ToList();

            foreach (var item in roundOrder)
            {
                // build choices (one correct + 3 distractors)
                var choices = new HashSet<string> { item.English };
                while (choices.Count < 4)
                {
                    var subj = Subjects[rng.Next(Subjects.Length)].English;
                    var noun = Nouns[rng.Next(Nouns.Length)].English;
                    var adj = Adjectives[rng.Next(Adjectives.Length)].English;
                    var distractor = $"{subj} {noun} {(noun.EndsWith("s") ? "because they are" : "because it is")} {adj}";
                    if (distractor != item.English)
                        choices.Add(distractor);
                }

                var choiceList = choices.OrderBy(_ => rng.Next()).ToList();

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
                    // immediately mark as learned by removing from remaining
                    remaining.Remove(item);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Wrong — correct: {item.English}\n");
                    Console.ResetColor();
                    // leave in remaining (so it will be asked again)
                }

                // show progress after each question
                var learned = totalSentences - remaining.Count;
                DrawProgressBar(learned, totalSentences, 30);
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
        Console.Write("[", filled, width);
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