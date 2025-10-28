using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    record Noun(string French, string English, char Gender, bool IsPlural);

    static readonly Noun[] Nouns = new[]
    {
        // Drinks / basics
        new Noun("le café", "coffee", 'm', false),
        new Noun("le chocolat", "chocolate", 'm', false),
        new Noun("le fromage", "cheese", 'm', false),
        new Noun("le jus de fruits", "fruit juice", 'm', false),
        new Noun("le lait", "milk", 'm', false),
        new Noun("le miel", "honey", 'm', false),
        new Noun("le pain", "bread", 'm', false),

        // Proteins / mains
        new Noun("le poisson", "fish", 'm', false),
        new Noun("le poulet rôti", "roast chicken", 'm', false),
        new Noun("la viande", "meat", 'f', false),
        new Noun("les crevettes", "prawns", 'f', true),
        new Noun("les hamburgers", "hamburgers", 'm', true),

        // Carbs / sides
        new Noun("le riz", "rice", 'm', false),
        new Noun("les frites", "fries", 'f', true),
        new Noun("les pommes de terre", "potatoes", 'f', true),

        // Drinks / water
        new Noun("l'eau", "water", 'f', false),

        // Spreads / salads / small items
        new Noun("la confiture", "jam", 'f', false),
        new Noun("la salade verte", "green salad", 'f', false),

        // Fruit / produce
        new Noun("les bananes", "bananas", 'f', true),
        new Noun("les pommes", "apples", 'f', true),
        new Noun("les tomates", "tomatoes", 'f', true),
        new Noun("les pêches", "peaches", 'f', true),
        new Noun("les fruits", "fruit", 'm', true),

        // Seafood / misc
        new Noun("les calamars", "squid", 'm', true),
        new Noun("les fruits de mer", "seafood", 'm', true),

        // Sandwiches / eggs / vegetables / sausages
        new Noun("les sandwiches au fromage", "cheese sandwiches", 'm', true),
        new Noun("les sandwiches au jambon", "ham sandwiches", 'm', true),
        new Noun("les oeufs", "eggs", 'm', true),
        new Noun("les légumes", "vegetables", 'm', true),
        new Noun("les saucisses", "sausages", 'f', true),

        // Generic / food
        new Noun("les aliments", "food", 'm', true),
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
        ("délicieuses", "delicious"), // surface plural/fem form (English identical)
        ("savoureux", "tasty"),
        ("savoureuses", "tasty"),
        ("sain", "healthy"),
        ("sains", "healthy"),
        ("sanes", "healthy"), // placeholder (not used for French correctness in this app)
        ("dégoûtant", "disgusting"),
        ("dégoûtants", "disgusting"),
        ("malsain", "unhealthy"),
        ("malsains", "unhealthy"),
        ("sucré", "sweet"),
        ("sucrés", "sweet"),
        ("gras", "fatty"),
        ("épicé", "spicy"),
        ("épicés", "spicy"),
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

        // Track correctly answered component indices
        var learnedSubjects = new HashSet<int>();
        var learnedNouns = new HashSet<int>();
        var learnedAdjectives = new HashSet<int>();

        Console.WriteLine();
        PrintBanner();

        var pool = sentences.OrderBy(_ => rng.Next()).ToList(); // randomized pool to pull from
        while (learnedSubjects.Count < Subjects.Length ||
               learnedNouns.Count < Nouns.Length ||
               learnedAdjectives.Count < Adjectives.Length)
        {
            // Prefer a sentence that contains at least one untested component
            var candidate = pool.FirstOrDefault(s =>
                !learnedSubjects.Contains(s.subjIdx) ||
                !learnedNouns.Contains(s.nounIdx) ||
                !learnedAdjectives.Contains(s.adjIdx));

            // If none found (shouldn't happen given construction), fallback to any random sentence
            if (candidate.Equals(default))
            {
                candidate = pool[rng.Next(pool.Count)];
            }

            // Ask the chosen candidate
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

            bool correctAnswer = false;
            if (!int.TryParse(input, out var selected) || selected < 1 || selected > choiceList.Count)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid choice — counted as wrong.");
                Console.ResetColor();
            }
            else if (choiceList[selected - 1] == candidate.English)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Correct!\n");
                Console.ResetColor();
                correctAnswer = true;

                // mark components as learned when this sentence is answered correctly
                learnedSubjects.Add(candidate.subjIdx);
                learnedNouns.Add(candidate.nounIdx);
                learnedAdjectives.Add(candidate.adjIdx);

                // remove this sentence from pool so we won't repeat it unnecessarily
                pool.Remove(candidate);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Wrong — correct: {candidate.English}\n");
                Console.ResetColor();

                // move the sentence to the end of the pool for retry later
                pool.Remove(candidate);
                pool.Add(candidate);
            }

            // Show component-based progress after each question
            DrawComponentProgress(learnedSubjects.Count, Subjects.Length,
                                  learnedNouns.Count, Nouns.Length,
                                  learnedAdjectives.Count, Adjectives.Length);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nAll subjects, nouns and adjectives have been tested (answered correctly) — well done!");
        Console.ResetColor();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    // Builds 4 choices that are deliberately similar:
    // - pick one component to vary (subject, noun or adjective)
    // - keep the other two components identical for all options
    static List<string> BuildSimilarChoices((string French, string English, int subjIdx, int nounIdx, int adjIdx) item, Random rng)
    {
        var correct = item.English;
        var choices = new HashSet<string> { correct };

        // Candidate component order to attempt (randomized)
        var attempts = new[] { 0, 1, 2 }.OrderBy(_ => rng.Next()).ToList();
        // 0 = vary adjective, 1 = vary noun, 2 = vary subject

        foreach (var attempt in attempts)
        {
            if (choices.Count >= 4) break;

            if (attempt == 0)
            {
                // vary adjective: keep subjIdx & nounIdx fixed
                var pool = Enumerable.Range(0, Adjectives.Length).Where(ai => ai != item.adjIdx).OrderBy(_ => rng.Next()).ToList();
                foreach (var ai in pool)
                {
                    if (choices.Count >= 4) break;
                    choices.Add(FormatEnglish(item.subjIdx, item.nounIdx, ai));
                }
            }
            else if (attempt == 1)
            {
                // vary noun: keep subjIdx & adjIdx fixed; require same plurality
                var pool = Enumerable.Range(0, Nouns.Length)
                    .Where(ni => ni != item.nounIdx && Nouns[ni].IsPlural == Nouns[item.nounIdx].IsPlural)
                    .OrderBy(_ => rng.Next()).ToList();
                foreach (var ni in pool)
                {
                    if (choices.Count >= 4) break;
                    choices.Add(FormatEnglish(item.subjIdx, ni, item.adjIdx));
                }
            }
            else // attempt == 2
            {
                // vary subject: keep nounIdx & adjIdx fixed; prefer same polarity group
                var positive = new[] { 0, 1, 2 };
                var negative = new[] { 3, 4 };
                var subjGroup = positive.Contains(item.subjIdx) ? positive : negative;
                foreach (var si in subjGroup.OrderBy(_ => rng.Next()))
                {
                    if (si == item.subjIdx) continue;
                    if (choices.Count >= 4) break;
                    choices.Add(FormatEnglish(si, item.nounIdx, item.adjIdx));
                }
            }
        }

        // Fill remaining slots with constrained random candidates (change only one component)
        var fillAttempts = 0;
        while (choices.Count < 4 && fillAttempts < 100)
        {
            fillAttempts++;
            var comp = rng.Next(3);
            int si = item.subjIdx, ni = item.nounIdx, ai = item.adjIdx;
            if (comp == 0) ai = rng.Next(Adjectives.Length);
            else if (comp == 1)
            {
                var pool = Enumerable.Range(0, Nouns.Length).Where(i => Nouns[i].IsPlural == Nouns[item.nounIdx].IsPlural).ToList();
                ni = pool[rng.Next(pool.Count)];
            }
            else
            {
                var positive = new[] { 0, 1, 2 };
                var negative = new[] { 3, 4 };
                var group = positive.Contains(item.subjIdx) ? positive : negative;
                si = group.OrderBy(_ => rng.Next()).First();
            }

            var candidate = FormatEnglish(si, ni, ai);
            if (candidate != correct) choices.Add(candidate);
        }

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

    static void DrawComponentProgress(int learnedSubj, int totalSubj, int learnedNoun, int totalNoun, int learnedAdj, int totalAdj)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Subjects: {learnedSubj}/{totalSubj}   Nouns: {learnedNoun}/{totalNoun}   Adjectives: {learnedAdj}/{totalAdj}\n");
        Console.ResetColor();
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