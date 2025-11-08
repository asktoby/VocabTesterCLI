using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class Program
{
    record Subject(string French, string English, int VerbGroup); // VerbGroup: 0=boire,1=manger,2=prendre
    record Food(string French, string English, int[] Meals);      // Meals: 0=breakfast,1=lunch,2=dinner,3=every day
    record Meal(string French, string English);

    static readonly Subject[] Subjects =
    {
        // boire (to drink) - VerbGroup 0
        new Subject("je bois", "I drink", 0),
        new Subject("tu bois", "you drink", 0),
        new Subject("il boit", "he drinks", 0),
        new Subject("elle boit", "she drinks", 0),
        new Subject("on boit", "one drinks", 0),
        new Subject("nous buvons", "we drink", 0),
        new Subject("vous buvez", "you all drink", 0),
        new Subject("ils boivent", "they drink", 0),
        new Subject("elles boivent", "they drink", 0),

        // manger (to eat) - VerbGroup 1
        new Subject("je mange", "I eat", 1),
        new Subject("tu manges", "you eat", 1),
        new Subject("il mange", "he eats", 1),
        new Subject("elle mange", "she eats", 1),
        new Subject("on mange", "one eats", 1),
        new Subject("nous mangeons", "we eat", 1),
        new Subject("vous mangez", "you all eat", 1),
        new Subject("ils mangent", "they eat", 1),
        new Subject("elles mangent", "they eat", 1),

        // prendre (to have) - VerbGroup 2
        new Subject("je prends", "I have", 2),
        new Subject("tu prends", "you have", 2),
        new Subject("il prend", "he has", 2),
        new Subject("elle prend", "she has", 2),
        new Subject("on prend", "one has", 2),
        new Subject("nous prenons", "we have", 2),
        new Subject("vous prenez", "you all have", 2),
        new Subject("ils prennent", "they have", 2),
        new Subject("elles prennent", "they have", 2),
    };

    static readonly Food[] Foods =
    {
        // breakfast (0)
        new Food("du café", "coffee", new[] {0}),
        new Food("du chocolat chaud", "hot chocolate", new[] {0}),
        new Food("du jus de fruits", "fruit juice", new[] {0}),
        new Food("des céréales", "cereal", new[] {0}),
        new Food("du pain grillé", "toast", new[] {0}),

        // common beverages (0,1,2)
        new Food("du lait", "milk", new[] {0,1,2}),
        new Food("du thé", "tea", new[] {0,1,2}),
        new Food("de l'eau", "water", new[] {0,1,2}),
        new Food("de la limonade", "lemonade", new[] {0,1,2}),

        // lunch (1)
        new Food("du chocolat", "chocolate", new[] {1}),
        new Food("du fromage", "cheese", new[] {1}),
        new Food("du miel", "honey", new[] {1}),
        new Food("du pain", "bread", new[] {1}),
        new Food("du poisson", "fish", new[] {1}),
        new Food("du poulet rôti", "roast chicken", new[] {1}),
        new Food("du riz", "rice", new[] {1}),
        new Food("du saumon", "salmon", new[] {1}),
        new Food("de la pizza", "pizza", new[] {1}),
        new Food("de la salade verte", "green salad", new[] {1}),

        // dinner (2)
        new Food("de la viande", "meat", new[] {2}),
        new Food("des frites", "fries", new[] {2}),
        new Food("des fruits", "fruit", new[] {2}),
        new Food("des légumes", "vegetables", new[] {2}),
        new Food("des oeufs", "eggs", new[] {2}),
        new Food("des pâtes", "pasta", new[] {2}),
        new Food("des sandwiches", "sandwiches", new[] {2}),
        new Food("des saucisses", "sausages", new[] {2}),
        new Food("des spaghettis", "spaghetti", new[] {2}),

        // flexible / every day (3)
        new Food("des chocolats", "chocolates", new[] {1,2,3}),
        new Food("du coca", "coke", new[] {0,1,2}),
    };

    static readonly Meal[] Meals =
    {
        new Meal("au petit-déjeuner", "at breakfast"),
        new Meal("au déjeuner", "at lunch"),
        new Meal("au dîner", "at dinner"),
        new Meal("tous les jours", "every day")
    };

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var rng = new Random();

        // Build all possible sentences and keep component indices so we can craft similar distractors
        var sentences = new List<(string French, string English, int subjIdx, int foodIdx, int mealIdx)>();
        for (int si = 0; si < Subjects.Length; si++)
        {
            for (int fi = 0; fi < Foods.Length; fi++)
            {
                foreach (var mi in Foods[fi].Meals)
                {
                    // Only include combinations where the food supports the meal
                    var subj = Subjects[si];
                    var food = Foods[fi];
                    var meal = Meals[mi];

                    var french = $"{subj.French} {food.French} {meal.French}.";
                    var english = $"{subj.English} {food.English} {meal.English}.";

                    sentences.Add((french, english, si, fi, mi));
                }
            }
        }

        var learnedSubjects = new HashSet<int>();
        var learnedFoods = new HashSet<int>();
        var learnedMeals = new HashSet<int>();

        Console.WriteLine();

        // Initial draw before the first question
        RedrawScreen(learnedSubjects.Count, Subjects.Length,
                     learnedFoods.Count, Foods.Length,
                     learnedMeals.Count, Meals.Length);

        var pool = sentences.OrderBy(_ => rng.Next()).ToList(); // randomized pool to pull from
        while (learnedSubjects.Count < Subjects.Length ||
               learnedFoods.Count < Foods.Length ||
               learnedMeals.Count < Meals.Length)
        {
            // Clear and redraw screen before each question so user can't scroll up to prior content
            RedrawScreen(learnedSubjects.Count, Subjects.Length,
                         learnedFoods.Count, Foods.Length,
                         learnedMeals.Count, Meals.Length);

            var candidate = pool.FirstOrDefault(s =>
                !learnedSubjects.Contains(s.subjIdx) ||
                !learnedFoods.Contains(s.foodIdx) ||
                !learnedMeals.Contains(s.mealIdx));

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

                learnedSubjects.Add(candidate.subjIdx);
                learnedFoods.Add(candidate.foodIdx);
                learnedMeals.Add(candidate.mealIdx);

                pool.Remove(candidate);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Wrong — correct: {candidate.English}\n");
                Console.ResetColor();

                pool.Remove(candidate);
                pool.Add(candidate);
            }

            DrawComponentProgress(learnedSubjects.Count, Subjects.Length,
                                  learnedFoods.Count, Foods.Length,
                                  learnedMeals.Count, Meals.Length);

            // small pause so user sees result before next redraw (optional)
            System.Threading.Thread.Sleep(650);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nAll subjects, foods and meals have been tested (answered correctly) — well done!");
        Console.ResetColor();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    // Draws the screen header + the three progress bars
    static void RedrawScreen(int learnedSubj, int totalSubj, int learnedFood, int totalFood, int learnedMeal, int totalMeal)
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
        DrawComponentProgress(learnedSubj, totalSubj, learnedFood, totalFood, learnedMeal, totalMeal);
    }

    // Builds 4 choices that are deliberately similar:
    // - pick one component to vary (subject, food or meal)
    // - keep the other two components identical for all options
    static List<string> BuildSimilarChoices((string French, string English, int subjIdx, int foodIdx, int mealIdx) item, Random rng)
    {
        var correct = item.English;
        var choices = new HashSet<string> { correct };

        var attempts = new[] { 0, 1, 2 }.OrderBy(_ => rng.Next()).ToList();
        // 0 = vary food, 1 = vary meal, 2 = vary subject

        foreach (var attempt in attempts)
        {
            if (choices.Count >= 4) break;

            if (attempt == 0)
            {
                // vary food: keep subjIdx & mealIdx fixed; require food that supports same meal
                var pool = Enumerable.Range(0, Foods.Length)
                    .Where(fi => fi != item.foodIdx && Foods[fi].Meals.Contains(item.mealIdx))
                    .OrderBy(_ => rng.Next()).ToList();
                foreach (var fi in pool)
                {
                    if (choices.Count >= 4) break;
                    choices.Add(FormatEnglish(item.subjIdx, fi, item.mealIdx));
                }
            }
            else if (attempt == 1)
            {
                // vary meal: keep subjIdx & foodIdx fixed; prefer other meals that this food supports
                var foodMeals = Foods[item.foodIdx].Meals.Where(m => m != item.mealIdx).OrderBy(_ => rng.Next()).ToList();
                foreach (var mi in foodMeals)
                {
                    if (choices.Count >= 4) break;
                    choices.Add(FormatEnglish(item.subjIdx, item.foodIdx, mi));
                }
            }
            else // attempt == 2
            {
                // vary subject: keep foodIdx & mealIdx fixed; prefer subjects from same verb group
                var group = Subjects[item.subjIdx].VerbGroup;
                var pool = Enumerable.Range(0, Subjects.Length)
                    .Where(si => si != item.subjIdx && Subjects[si].VerbGroup == group)
                    .OrderBy(_ => rng.Next()).ToList();
                foreach (var si in pool)
                {
                    if (choices.Count >= 4) break;
                    choices.Add(FormatEnglish(si, item.foodIdx, item.mealIdx));
                }
            }
        }

        // Fill remaining slots with constrained random candidates (change only one component)
        var fillAttempts = 0;
        while (choices.Count < 4 && fillAttempts < 200)
        {
            fillAttempts++;
            var comp = rng.Next(3);
            int si = item.subjIdx, fi = item.foodIdx, mi = item.mealIdx;
            if (comp == 0)
            {
                // change food but keep same meal
                var pool = Enumerable.Range(0, Foods.Length).Where(i => Foods[i].Meals.Contains(item.mealIdx)).ToList();
                fi = pool[rng.Next(pool.Count)];
            }
            else if (comp == 1)
            {
                // change meal to one the food supports (or random if none)
                var alternatives = Foods[item.foodIdx].Meals.ToList();
                if (alternatives.Count == 0)
                    mi = rng.Next(Meals.Length);
                else
                    mi = alternatives[rng.Next(alternatives.Count)];
            }
            else
            {
                // change subject, prefer same verb group
                var group = Subjects[item.subjIdx].VerbGroup;
                var pool = Enumerable.Range(0, Subjects.Length).Where(i => Subjects[i].VerbGroup == group).ToList();
                si = pool[rng.Next(pool.Count)];
            }

            var candidate = FormatEnglish(si, fi, mi);
            if (candidate != correct) choices.Add(candidate);
        }

        return choices.OrderBy(_ => rng.Next()).ToList();
    }

    static string FormatEnglish(int subjIdx, int foodIdx, int mealIdx)
    {
        var subj = Subjects[subjIdx].English;
        var food = Foods[foodIdx].English;
        var meal = Meals[mealIdx].English;
        return $"{subj} {food} {meal}.";
    }

    static void DrawComponentProgress(int learnedSubj, int totalSubj, int learnedFood, int totalFood, int learnedMeal, int totalMeal)
    {
        // Render three ASCII progress bars (Subjects, Foods, Meals)
        Console.WriteLine();
        DrawProgressBar("Subjects:", learnedSubj, totalSubj, 24);
        DrawProgressBar("Foods:",    learnedFood,  totalFood,  24);
        DrawProgressBar("Meals:",    learnedMeal,  totalMeal,  24);
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
        Console.WriteLine("║     French sentence → English multiple choice  ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine("Translate the French sentence shown into natural English.\n");
    }
}