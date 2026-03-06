using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OniRepl
{
    public static class Registers
    {
        public static int Cell = Grid.InvalidCell;
        public static int Priority = 5;
        public static SimHashes Material;
        public static BuildingDef Building;
        public static int Count;
        public static float Quantity = 100f;
        public static Tag Critter = Tag.Invalid;
        public static string Symbol;
        public static Orientation Orientation = Orientation.Neutral;
    }

    public interface IWord
    {
        string Name { get; }
        string Help { get; }
        bool SuppressAchievements { get; }
        string Execute();
    }

    public class ForthEngine
    {
        private readonly Dictionary<string, IWord> builtins = new Dictionary<string, IWord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> userWords = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex QuantityRegex = new Regex(@"^(\d+(?:\.\d+)?)(kg|g|t)$", RegexOptions.IgnoreCase);

        public IReadOnlyDictionary<string, IWord> Builtins => builtins;
        public IReadOnlyDictionary<string, List<string>> UserWords => userWords;

        // Suspension support
        public bool Suspended { get; set; }
        private readonly List<string> continuation = new List<string>();

        public void RegisterWord(IWord word)
        {
            builtins[word.Name] = word;
        }

        public string Execute(string input)
        {
            var tokens = Tokenize(input);
            if (Suspended)
            {
                continuation.AddRange(tokens);
                return "Queued (waiting for builds to complete)...";
            }
            return ExecuteTokens(tokens, 0);
        }

        public void ClearContinuation()
        {
            continuation.Clear();
        }

        public string Resume()
        {
            if (continuation.Count == 0) return null;
            var tokens = new List<string>(continuation);
            continuation.Clear();
            return ExecuteTokens(tokens, 0);
        }

        private string ExecuteTokens(List<string> tokens, int startIndex)
        {
            Suspended = false;
            var output = new List<string>();

            for (int i = startIndex; i < tokens.Count; i++)
            {
                // Handle do...loop: N do body... loop
                if (tokens[i].Equals("do", StringComparison.OrdinalIgnoreCase))
                {
                    int count = Registers.Count;

                    var body = new List<string>();
                    int depth = 1;
                    i++;
                    while (i < tokens.Count && depth > 0)
                    {
                        if (tokens[i].Equals("do", StringComparison.OrdinalIgnoreCase))
                            depth++;
                        else if (tokens[i].Equals("loop", StringComparison.OrdinalIgnoreCase))
                        {
                            depth--;
                            if (depth == 0) break;
                        }
                        body.Add(tokens[i]);
                        i++;
                    }
                    if (depth > 0)
                    {
                        output.Add("Error: unterminated do, expected 'loop'");
                        break;
                    }

                    for (int n = 0; n < count; n++)
                    {
                        var loopResult = ExecuteTokens(body, 0);
                        if (loopResult != null)
                            output.Add(loopResult);
                        if (Suspended)
                        {
                            for (int remaining = n + 1; remaining < count; remaining++)
                                continuation.AddRange(body);
                            for (int j = i + 1; j < tokens.Count; j++)
                                continuation.Add(tokens[j]);
                            break;
                        }
                    }
                    if (Suspended) break;
                    continue;
                }

                // Handle word definition: : name ... ;
                if (tokens[i] == ":")
                {
                    i++;
                    if (i >= tokens.Count)
                    {
                        output.Add("Error: expected word name after ':'");
                        break;
                    }
                    var wordName = tokens[i];
                    var body = new List<string>();
                    i++;
                    while (i < tokens.Count && tokens[i] != ";")
                    {
                        body.Add(tokens[i]);
                        i++;
                    }
                    if (i >= tokens.Count)
                    {
                        output.Add("Error: unterminated word definition, expected ';'");
                        break;
                    }
                    userWords[wordName] = body;
                    output.Add($"Defined: {wordName}");
                    continue;
                }

                var result = ExecuteToken(tokens[i]);
                if (result != null)
                    output.Add(result);

                if (Suspended)
                {
                    for (int j = i + 1; j < tokens.Count; j++)
                        continuation.Add(tokens[j]);
                    break;
                }
            }

            return output.Count > 0 ? string.Join("\n", output) : null;
        }

        private string ExecuteToken(string token)
        {
            // Built-in word
            if (builtins.TryGetValue(token, out var word))
            {
                try
                {
                    if (word.SuppressAchievements && Game.Instance != null)
                        Game.Instance.debugWasUsed = true;
                    return word.Execute();
                }
                catch (Exception ex)
                {
                    return $"Error in '{token}': {ex.Message}";
                }
            }

            // User-defined word
            if (userWords.TryGetValue(token, out var body))
            {
                var output = new List<string>();
                for (int i = 0; i < body.Count; i++)
                {
                    var result = ExecuteToken(body[i]);
                    if (result != null)
                        output.Add(result);

                    if (Suspended)
                    {
                        for (int j = i + 1; j < body.Count; j++)
                            continuation.Add(body[j]);
                        break;
                    }
                }
                return output.Count > 0 ? string.Join("\n", output) : null;
            }

            // Set register
            SetRegister(token);
            return null;
        }

        private void SetRegister(string token)
        {
            // Quantity: 1000kg, 500g, 1t
            var match = QuantityRegex.Match(token);
            if (match.Success)
            {
                float value = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                string unit = match.Groups[2].Value.ToLowerInvariant();
                Registers.Quantity = unit == "g" ? value / 1000f : unit == "t" ? value * 1000f : value;
                return;
            }

            // Plain integer
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal))
            {
                Registers.Count = intVal;
                return;
            }

            // Location keywords
            if (token.Equals("cursor", StringComparison.OrdinalIgnoreCase))
            {
                Registers.Cell = LocationResolver.CursorCell();
                return;
            }
            if (token.Equals("printer", StringComparison.OrdinalIgnoreCase))
            {
                Registers.Cell = LocationResolver.PrinterCell();
                return;
            }

            // Coordinate pair: x,y
            if (LocationResolver.TryParseCoords(token, out int cell))
            {
                Registers.Cell = cell;
                return;
            }

            // Element name
            if (ElementResolver.TryResolve(token, out SimHashes hash))
            {
                Registers.Material = hash;
                Registers.Symbol = token;
                return;
            }

            // Critter name
            if (CritterResolver.TryResolve(token, out Tag critterTag))
            {
                Registers.Critter = critterTag;
                return;
            }

            // Building name
            if (BuildingResolver.TryResolve(token, out BuildingDef buildingDef))
            {
                Registers.Building = buildingDef;
                return;
            }

            // Fallback: symbol
            Registers.Symbol = token;
        }

        private List<string> Tokenize(string input)
        {
            var tokens = new List<string>();
            foreach (var line in input.Split('\n'))
            {
                var clean = line;
                int commentIdx = clean.IndexOf('\\');
                if (commentIdx >= 0)
                    clean = clean.Substring(0, commentIdx);

                bool inQuote = false;
                var current = new System.Text.StringBuilder();
                foreach (char c in clean)
                {
                    if (c == '"')
                    {
                        inQuote = !inQuote;
                        continue;
                    }
                    if (!inQuote && (c == ' ' || c == '\t'))
                    {
                        if (current.Length > 0)
                        {
                            tokens.Add(current.ToString());
                            current.Clear();
                        }
                        continue;
                    }
                    current.Append(c);
                }
                if (current.Length > 0)
                    tokens.Add(current.ToString());
            }
            return tokens;
        }
    }
}
