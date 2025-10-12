using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static readonly (string French, string English)[] Vocab = new[]
    {
        ("le café", "coffee"),
        ("le chocolat", "chocolate"),
        ("le fromage", "cheese"),
        ("le jus de fruits", "fruit juice"),
        ("le lait", "milk"),
        ("le miel", "honey"),
        ("le pain", "bread"),
        ("le poisson", "fish"),
        ("le poulet rôti", "roast chicken"),
        ("le riz", "rice"),
        ("l'eau", "water"),
        ("la confiture", "jam"),
        ("la salade verte", "green salad"),
        ("la viande", "meat"),
        ("les aliments", "food"),
        ("sucrés", "sweet"),
        ("épicés", "spicy"),
        ("gras", "fatty"),
        ("riches en protéines", "rich in protein"),
    };

    enum QuizState { NeedEnglish, NeedFrench }

    static void Main()
    {
        var rng = new Random();
        // Each word starts needing English, then needs French, then is eliminated
        var remaining = Vocab.ToDictionary(v => v, v => QuizState.NeedEnglish);

        PrintBanner();

        while (remaining.Count > 0)
        {
            var order = remaining.Keys.OrderBy(_ => rng.Next()).ToList();
            var wrong = new List<(string French, string English, QuizState)>();

            foreach (var key in order)
            {
                var state = remaining[key];
                bool correct = false;

                if (state == QuizState.NeedEnglish)
                {
                    // Ask for English meaning (multiple choice)
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
                    int selected;
                    if (!int.TryParse(input, out selected) || selected < 1 || selected > choices.Count)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Oopsie! That's not a valid choice. Let's try again next time! 🐣");
                        Console.ResetColor();
                        wrong.Add((key.French, key.English, state));
                        continue;
                    }

                    if (choices[selected - 1] == key.English)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Yay! You got it right! 🎉✨");
                        Console.ResetColor();
                        correct = true;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Aww, not quite! The correct answer is \"{key.English}\". 🍬");
                        Console.ResetColor();
                        wrong.Add((key.French, key.English, state));
                        continue;
                    }
                }
                else if (state == QuizState.NeedFrench)
                {
                    // Ask for French meaning (free text input)
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n🌟 Type the French for \"{key.English}\"! 🌟");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.Write("Your answer: ");
                    Console.ResetColor();
                    var input = Console.ReadLine()?.Trim();

                    if (string.Equals(input, key.French, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Magnifique! You got the French right! 🥳🥐");
                        Console.ResetColor();
                        correct = true;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Aww, not quite! The correct answer is \"{key.French}\". 🍬");
                        Console.ResetColor();
                        wrong.Add((key.French, key.English, state));
                        continue;
                    }
                }

                // If correct, update state or remove
                if (correct)
                {
                    if (state == QuizState.NeedEnglish)
                        remaining[key] = QuizState.NeedFrench;
                    else
                        remaining.Remove(key);
                }
            }

            // Only keep wrong answers for next round
            var wrongSet = new HashSet<(string French, string English, QuizState)>(wrong);
            foreach (var kvp in remaining.Keys.ToList())
            {
                var state = remaining[kvp];
                if (!wrongSet.Contains((kvp.French, kvp.English, state)))
                {
                    // If not wrong, but not yet eliminated, keep in remaining
                    if (state == QuizState.NeedEnglish || state == QuizState.NeedFrench)
                        continue;
                }
                // If wrong, keep for next round
            }
            remaining = remaining
                .Where(kvp => wrongSet.Contains((kvp.Key.French, kvp.Key.English, kvp.Value)) || kvp.Value == QuizState.NeedFrench || kvp.Value == QuizState.NeedEnglish)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n🌈 All words answered correctly both ways! You're a vocab superstar! 🦄✨");
        Console.ResetColor();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("╔══════════════════════════════════════════════════╗");
        Console.WriteLine("║         🥐 French Vocabulary Tester! 🥖         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine("Let's learn some tasty French words together! 🍫🍯🍗\n");
    }
}