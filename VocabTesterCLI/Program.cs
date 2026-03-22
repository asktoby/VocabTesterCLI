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

    // Console resize tracking (detect font/zoom changes that adjust WindowWidth/BufferHeight)
    static volatile int s_lastWindowWidth;
    static volatile int s_lastBufferHeight;
    static volatile bool s_layoutDirty;

    // Stage 2 progress tracking
    static int s_stage2Total = 0;
    static int s_stage2Completed = 0;

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var rng = new Random();

        // Initialize last-known sizes (best-effort)
        try
        {
            s_lastWindowWidth = Console.WindowWidth;
            s_lastBufferHeight = Console.BufferHeight;
        }
        catch
        {
            s_lastWindowWidth = 80;
            s_lastBufferHeight = 25;
        }

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

            // Also detect console size changes from the background timer and mark layout dirty
            CheckForResizeAndMark();

            UpdateTimerDisplay(remaining);
        }, null, 0, 1000);

        // Build all vocabulary entries as standalone "sentences".
        // We'll store component indices so distractor generation can vary the same category.
        // tuple: french, english, whenIdx, conjugationIdx, vocabIdx
        var sentences = new List<(string French, string English, int whenIdx, int conjugationIdx, int vocabIdx)>();

        // Starters / sentence-starter entries
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
        s_layoutDirty = false;

        var pool = sentences.OrderBy(_ => rng.Next()).ToList(); // randomized pool to pull from

        // Debug flag: allow '#' to skip phase 1 and jump to phase 2 (for testing)
        bool debugSkipToStage2 = false;

        while (!s_timeUp && (
               learnedWhen.Count < WhenPhrases.Length ||
               learnedConjugations.Count < Conjugations.Length ||
               learnedVocab.Count < Vocab.Length))
        {
            // If the console was resized by the user (Ctrl+mousewheel or otherwise),
            // detect that and redraw the entire screen to avoid cursor-position corruption.
            CheckForResizeAndClearIfNeeded(learnedWhen.Count, WhenPhrases.Length,
                                          learnedConjugations.Count, Conjugations.Length,
                                          learnedVocab.Count, Vocab.Length);

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
                // If somehow all items are learned, break to avoid infinite loop
                if (learnedWhen.Count == WhenPhrases.Length &&
                    learnedConjugations.Count == Conjugations.Length &&
                    learnedVocab.Count == Vocab.Length)
                {
                    break;
                }

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
            var rawInput = Console.ReadLine() ?? string.Empty;

            // Secret debug key: '#' skips phase 1 and moves to phase 2.
            if (rawInput.Trim() == "#")
            {
                debugSkipToStage2 = true;
                break;
            }

            // If time expired while waiting for input, break immediately.
            if (s_timeUp)
            {
                break;
            }

            if (!int.TryParse(rawInput, out var selected) || selected < 1 || selected > choiceList.Count)
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

        // Do NOT stop the display timer here — keep it running into stage 2
        Console.ForegroundColor = ConsoleColor.Green;
        if (s_timeUp)
        {
            Console.WriteLine("\nTime is up — the test has ended.");
        }
        else if (debugSkipToStage2)
        {
            Console.WriteLine("\nSkipping to phase 2 (debug).");
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

        // Start second stage: English -> build French sentence
        RunConstructionStage(rng,
            learnedWhen.Count, WhenPhrases.Length,
            learnedConjugations.Count, Conjugations.Length,
            learnedVocab.Count, Vocab.Length);

        // Now stop timer after stage 2 completes
        s_displayTimer?.Dispose();

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    // Stage 2: present an English sentence, the user chooses French tokens one-by-one to build it
    static void RunConstructionStage(Random rng,
        int learnedWhenCount, int totalWhen,
        int learnedConjugationsCount, int totalConjugations,
        int learnedVocabCount, int totalVocab)
    {
        // Build a pool of combined sentences (English / French) using the same components.
        var sentencePairs = new List<(string Eng, string Fr)>();

        // For "is" starters (0 and 1) use noun-only vocab (no preposition)
        for (int s = 0; s < WhenPhrases.Length; s++)
        {
            if (s == 0 || s == 1) // "Ma pièce préférée est" / "Mon endroit préféré est"
            {
                var nouns = Vocab.Where(v => !StartsWithPreposition(v.French)).ToArray();
                foreach (var n in nouns)
                {
                    var fr = $"{WhenPhrases[s].French} {n.French}.";
                    var en = $"{WhenPhrases[s].English} {n.English}.";
                    sentencePairs.Add((en, fr));
                }
            }
            else // "J'aime" / "Je n'aime pas" -> need conjugation + preposition vocab
            {
                var pres = Vocab.Where(v => StartsWithPreposition(v.French)).ToArray();
                foreach (var c in Conjugations)
                {
                    foreach (var p in pres)
                    {
                        var fr = $"{WhenPhrases[s].French} {c.French} {p.French}.";
                        var en = $"{WhenPhrases[s].English} {c.English} {p.English}.";
                        sentencePairs.Add((en, fr));
                    }
                }
            }
        }

        // Shuffle and take a limited number to keep the stage short (adjustable)
        var pool = sentencePairs.OrderBy(_ => rng.Next()).Take(10).ToList();

        // Set stage 2 totals for progress tracking
        s_stage2Total = pool.Count;
        s_stage2Completed = 0;

        // Build a token pool from components to create distractors
        var tokenPool = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in WhenPhrases) foreach (var t in TokenizeFrench(w.French)) tokenPool.Add(t);
        foreach (var c in Conjugations) foreach (var t in TokenizeFrench(c.French)) tokenPool.Add(t);
        foreach (var v in Vocab) foreach (var t in TokenizeFrench(v.French)) tokenPool.Add(t);

        Console.WriteLine();
        // Use banner/title appropriate for phase 2
        PrintBanner("French → English sentence builder", "Build the French sentence.");
        // Draw phase-1 progress bars plus construction bar (keeps them visible during stage 2)
        DrawComponentProgress(learnedWhenCount, totalWhen, learnedConjugationsCount, totalConjugations, learnedVocabCount, totalVocab);
        Console.WriteLine("Pick the correct next French word from the choices. Wrong answers require you to try again until you find the correct word.\n");

        foreach (var pair in pool)
        {
            // Keep trying the same sentence until it completes or time expires.
            // This preserves partial progress across console resizes instead of abandoning the question.
            var targetTokens = TokenizeFrench(pair.Fr);
            var built = new List<string>();
            int mistakes = 0;

            while (!s_timeUp && built.Count < targetTokens.Count)
            {
                // If the console was resized while the user was in stage 1 or while attempting this sentence,
                // ensure we handle it and re-render preserving `built`.
                CheckForResizeAndMark();
                if (s_layoutDirty)
                {
                    try { Console.Clear(); } catch { }
                    PrintBanner("French → English sentence builder", "Build the French sentence from the English.");
                    DrawComponentProgress(learnedWhenCount, totalWhen, learnedConjugationsCount, totalConjugations, learnedVocabCount, totalVocab);
                    s_layoutDirty = false;
                }

                // Attempt to reserve a fixed block so the English sentence stays visible and clear any remnants
                try
                {
                    Console.Clear();
                    PrintBanner("French → English sentence builder", "Build the French sentence from the English.");
                    DrawComponentProgress(learnedWhenCount, totalWhen, learnedConjugationsCount, totalConjugations, learnedVocabCount, totalVocab);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"English: {pair.Eng}");
                    Console.ResetColor();

                    // Reserve in-place block lines beneath the English line
                    int blockStart = Console.CursorTop; // first line of the block

                    // Compute reserve lines large enough to avoid remnants; limited by buffer height
                    int maxAvailable = Math.Max(12, Console.BufferHeight - blockStart - 2);
                    int reserveLines = Math.Min(maxAvailable, Math.Max(12, targetTokens.Count * 4 + 6));
                    ClearRegion(blockStart, reserveLines);

                    // Resume from where we left off
                    int pos = built.Count;

                    bool abortedByResize = false;
                    bool exitedByTime = false;

                    for (; pos < targetTokens.Count; )
                    {
                        // If the user has changed console size (zoomed via ctrl+mousewheel), bail out of the in-place flow
                        // so we can re-reserve using the updated sizes while preserving `built`.
                        CheckForResizeAndMark();
                        if (s_layoutDirty)
                        {
                            abortedByResize = true;
                            break; // fall back to outer while to re-render and retry this sentence
                        }

                        var correct = targetTokens[pos];

                        // Prepare choices: correct + 3 distractors
                        var choices = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correct };
                        var attempts = 0;
                        while (choices.Count < 4 && attempts++ < 200)
                        {
                            var pick = tokenPool.ElementAt(rng.Next(tokenPool.Count));
                            if (string.Equals(pick, correct, StringComparison.OrdinalIgnoreCase)) continue;
                            choices.Add(pick);
                        }

                        var choicesList = choices.OrderBy(_ => rng.Next()).ToList();

                        // Update "French so far:" label and built content
                        Console.SetCursorPosition(0, blockStart + 0);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("French so far:".PadRight(Math.Max(1, Console.WindowWidth - 1)));
                        Console.SetCursorPosition(0, blockStart + 1);
                        var builtLine = string.Join(" ", built);
                        Console.Write(builtLine.PadRight(Math.Max(1, Console.WindowWidth - 1)));
                        Console.ResetColor();

                        // Clear previous status line
                        Console.SetCursorPosition(0, blockStart + 2);
                        Console.Write(new string(' ', Math.Max(1, Console.WindowWidth - 1)));

                        // "Choose next word:" label
                        Console.SetCursorPosition(0, blockStart + 3);
                        Console.Write("Choose next word:".PadRight(Math.Max(1, Console.WindowWidth - 1)));

                        // Write choices in reserved lines (blockStart+4 .. +7) — stay inside reserved block
                        for (int i = 0; i < 4; i++)
                        {
                            Console.SetCursorPosition(0, blockStart + 4 + i);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            var text = $" {i + 1}. {choicesList[i]}";
                            Console.Write(text.PadRight(Math.Max(1, Console.WindowWidth - 1)));
                            Console.ResetColor();
                        }

                        bool gotCorrect = false;

                        while (!gotCorrect)
                        {
                            // Check for resize while waiting for input
                            CheckForResizeAndMark();
                            if (s_layoutDirty)
                            {
                                // Stop trying to manage fixed positions — outer while will reconstruct layout
                                abortedByResize = true;
                                break;
                            }

                            // Prompt on reserved prompt line
                            Console.SetCursorPosition(0, blockStart + 8);
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.Write("Pick your answer (1-4): ".PadRight(Math.Max(1, Console.WindowWidth - 1)));
                            Console.ResetColor();

                            // Move cursor to end of prompt to read input
                            Console.SetCursorPosition("Pick your answer (1-4): ".Length, blockStart + 8);
                            var input = Console.ReadLine() ?? string.Empty;

                            if (s_timeUp)
                            {
                                // If the timer expired while we're in phase 2, bail out
                                Console.SetCursorPosition(0, blockStart + 2);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Write("Time expired — returning to main menu.".PadRight(Math.Max(1, Console.WindowWidth - 1)));
                                Console.ResetColor();
                                exitedByTime = true;
                                break;
                            }

                            if (!int.TryParse(input, out var selected) || selected < 1 || selected > choicesList.Count)
                            {
                                // Invalid: count as a mistake, ask to try again
                                mistakes++;
                                Console.SetCursorPosition(0, blockStart + 2);
                                Console.ForegroundColor = ConsoleColor.Red;
                                var msg = "Invalid choice — try again.";
                                Console.Write(msg.PadRight(Math.Max(1, Console.WindowWidth - 1)));
                                Console.ResetColor();
                                // loop continues: choices remain visible
                                continue;
                            }

                            if (choicesList[selected - 1] == correct)
                            {
                                // Correct
                                Console.SetCursorPosition(0, blockStart + 2);
                                Console.ForegroundColor = ConsoleColor.Green;
                                var msg = "Correct!";
                                Console.Write(msg.PadRight(Math.Max(1, Console.WindowWidth - 1)));
                                Console.ResetColor();

                                built.Add(correct);
                                gotCorrect = true;
                                pos++; // advance to next token
                            }
                            else
                            {
                                // Wrong selection: count and let user try again
                                mistakes++;
                                Console.SetCursorPosition(0, blockStart + 2);
                                Console.ForegroundColor = ConsoleColor.Red;
                                var msg = "Wrong — try again.";
                                Console.Write(msg.PadRight(Math.Max(1, Console.WindowWidth - 1)));
                                Console.ResetColor();
                                // loop continues so the user can pick again
                                continue;
                            }
                        }

                        if (abortedByResize || exitedByTime) break;

                        // Small pause so user sees status (keeps English visible)
                        System.Threading.Thread.Sleep(650);
                    }

                    if (abortedByResize)
                    {
                        // Preserve `built` and retry the same sentence with a fresh layout in the outer while loop.
                        s_layoutDirty = false;
                        ClearRegion(blockStart, reserveLines);
                        continue;
                    }

                    if (s_timeUp)
                    {
                        // If time expired while inside, stop attempting this sentence
                        ClearRegion(blockStart, reserveLines);
                        Console.SetCursorPosition(0, blockStart + 0);
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Time expired — returning to main menu.".PadRight(Math.Max(1, Console.WindowWidth - 1)));
                        Console.ResetColor();
                        break;
                    }

                    // After sentence complete show only summary (no Target/Your built output)
                    ClearRegion(blockStart, reserveLines);
                    Console.SetCursorPosition(0, blockStart + 0);

                    if (built.Count == targetTokens.Count && built.Count > 0)
                    {
                        if (mistakes == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Perfect — no mistakes.".PadRight(Math.Max(1, Console.WindowWidth - 1)));
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"{mistakes} mistake(s) — review the sentence above.".PadRight(Math.Max(1, Console.WindowWidth - 1)));
                        }
                        Console.ResetColor();
                    }
                    else
                    {
                        // If we left early due to time or similar
                        Console.WriteLine();
                    }

                    // Update stage 2 progress if sentence completed
                    if (built.Count == targetTokens.Count)
                    {
                        s_stage2Completed = Math.Min(s_stage2Total, s_stage2Completed + 1);
                    }

                    // Redraw the progress bars with updated construction progress
                    DrawComponentProgress(learnedWhenCount, totalWhen, learnedConjugationsCount, totalConjugations, learnedVocabCount, totalVocab);

                    // Prompt for continue, keep English at top
                    Console.WriteLine();
                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();

                    // Clear reserved block before next iteration to avoid remnants
                    ClearRegion(blockStart - 1, reserveLines + 2);

                    // Sentence finished; break outer while to move to next pair
                    break;
                }
                catch
                {
                    // Fallback: if console doesn't support cursor ops, fall back to scrolling behavior
                    // To reduce remnants, print a separator before the content.
                    try
                    {
                        Console.WriteLine(new string('-', Math.Max(10, Console.WindowWidth)));
                    }
                    catch { /* ignore if WindowWidth not supported */ }

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"English: {pair.Eng}");
                    Console.ResetColor();

                    // Tokenize target French sentence (keep last token punctuation-attached)
                    var fallbackTargetTokens = TokenizeFrench(pair.Fr);
                    bool fallbackExitedByTime = false;

                    for (int pos = built.Count; pos < fallbackTargetTokens.Count; pos++)
                    {
                        var correct = fallbackTargetTokens[pos];

                        // Prepare choices: correct + 3 distractors
                        var choices = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { correct };
                        var attempts = 0;
                        while (choices.Count < 4 && attempts++ < 200)
                        {
                            var pick = tokenPool.ElementAt(rng.Next(tokenPool.Count));
                            if (string.Equals(pick, correct, StringComparison.OrdinalIgnoreCase)) continue;
                            choices.Add(pick);
                        }

                        var choicesList = choices.OrderBy(_ => rng.Next()).ToList();

                        bool gotCorrect = false;
                        while (!gotCorrect)
                        {
                            // Show progress
                            Console.Write("French so far: ");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(string.Join(" ", built));
                            Console.ResetColor();

                            Console.WriteLine("Choose next word:");
                            for (int i = 0; i < choicesList.Count; i++)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.Write($" {i + 1}. ");
                                Console.ResetColor();
                                Console.WriteLine(choicesList[i]);
                            }

                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.Write("Pick your answer (1-4): ");
                            Console.ResetColor();
                            var input = Console.ReadLine();

                            if (s_timeUp)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Time expired — returning to main menu.");
                                Console.ResetColor();
                                fallbackExitedByTime = true;
                                break;
                            }

                            if (!int.TryParse(input, out var selected) || selected < 1 || selected > choicesList.Count)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Invalid choice — try again.");
                                Console.ResetColor();
                                mistakes++;
                                // loop continues
                                continue;
                            }

                            if (choicesList[selected - 1] == correct)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("Correct!\n");
                                Console.ResetColor();
                                built.Add(correct);
                                gotCorrect = true;
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Wrong — try again.\n");
                                Console.ResetColor();
                                mistakes++;
                                // loop continues
                            }
                        }

                        if (fallbackExitedByTime) break;
                    }

                    // Completed sentence — do NOT print Target/Your built per user request
                    Console.WriteLine();
                    if (s_timeUp)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Time expired — returning to main menu.\n");
                        Console.ResetColor();
                    }
                    else if (built.Count == fallbackTargetTokens.Count && mistakes == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Perfect — no mistakes.\n");
                        Console.ResetColor();
                    }
                    else if (built.Count == fallbackTargetTokens.Count)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{mistakes} mistake(s) — review the sentence above.\n");
                        Console.ResetColor();
                    }

                    // Update stage 2 progress if sentence completed (fallback)
                    if (built.Count == fallbackTargetTokens.Count)
                    {
                        s_stage2Completed = Math.Min(s_stage2Total, s_stage2Completed + 1);
                    }

                    // Redraw the progress bars with updated construction progress
                    DrawComponentProgress(learnedWhenCount, totalWhen, learnedConjugationsCount, totalConjugations, learnedVocabCount, totalVocab);

                    Console.WriteLine("Press Enter to continue...");
                    Console.ReadLine();
                    Console.WriteLine();

                    // In fallback mode we finished the sentence; break outer while to move to next pair
                    break;
                }
            }

            // Update the visible progress bars after each sentence attempt
            DrawComponentProgress(learnedWhenCount, totalWhen, learnedConjugationsCount, totalConjugations, learnedVocabCount, totalVocab);
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Stage 2 complete.\n");
        Console.ResetColor();
    }

    static bool StartsWithPreposition(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var lower = s.ToLowerInvariant();
        return lower.StartsWith("dans ") || lower.StartsWith("sur ") || lower.StartsWith("au ") || lower.StartsWith("à ") || lower.StartsWith("chez ") || lower.StartsWith("aux ");
    }

    static List<string> TokenizeFrench(string french)
    {
        // Keep tokens as they appear; ensure final period is attached to last token (if present).
        var trimmed = french.Trim();
        var hasPeriod = trimmed.EndsWith(".");
        if (hasPeriod) trimmed = trimmed.Substring(0, trimmed.Length - 1);

        var rawTokens = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (hasPeriod && rawTokens.Count > 0)
        {
            rawTokens[rawTokens.Count - 1] = rawTokens[rawTokens.Count - 1] + ".";
        }

        return rawTokens;
    }

    // Helper to clear a rectangular region of the console to avoid remnants.
    static void ClearRegion(int top, int height)
    {
        try
        {
            var width = Math.Max(1, Console.WindowWidth - 1);
            for (int i = 0; i < height; i++)
            {
                int row = top + i;
                if (row >= 0 && row < Console.BufferHeight)
                {
                    Console.SetCursorPosition(0, row);
                    Console.Write(new string(' ', width));
                }
            }
        }
        catch
        {
            // If cursor ops fail, ignore — caller should handle fallback.
        }
    }

    // Draws the screen header + the three progress bars
    // subtitle parameter allows showing a stage-specific instruction text.
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

        PrintBanner(); // default title/subtitle for stage 1
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
        // Render three ASCII progress bars (Starters / Conjugations / Vocabulary) plus Construction
        Console.WriteLine();
        DrawProgressBar("Starters:", learnedWhen, totalWhen, 24);
        DrawProgressBar("Conjugations:",  learnedConjugations, totalConjugations, 24);
        DrawProgressBar("Vocabulary:",   learnedVocab,  totalVocab,  24);

        // Construction progress (stage 2). Uses shared static counters set in RunConstructionStage.
        DrawProgressBar("Construction:", s_stage2Completed, s_stage2Total, 24);

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

    static void PrintBanner(string title = "French vocabulary → English multiple choice", string subtitle = "Translate the French item shown into natural English.")
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine($"║     {title}".PadRight(46) + "║");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine(subtitle + "\n");
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

    // If a resize is detected, mark layout dirty so the main thread can clear/redraw safely.
    static void CheckForResizeAndMark()
    {
        try
        {
            int w = Console.WindowWidth;
            int bh = Console.BufferHeight;
            if (w != s_lastWindowWidth || bh != s_lastBufferHeight)
            {
                s_lastWindowWidth = w;
                s_lastBufferHeight = bh;
                s_layoutDirty = true;
            }
        }
        catch
        {
            // ignore failures reading sizes
        }
    }

    // Called from main loop to clear and redraw if layout changed
    static void CheckForResizeAndClearIfNeeded(int learnedWhen, int totalWhen, int learnedConjugations, int totalConjugations, int learnedVocab, int totalVocab)
    {
        CheckForResizeAndMark();
        if (s_layoutDirty)
        {
            lock (s_consoleLock)
            {
                try { Console.Clear(); } catch { }
                RedrawScreen(learnedWhen, totalWhen, learnedConjugations, totalConjugations, learnedVocab, totalVocab);
                s_layoutDirty = false;
            }
        }
    }
}