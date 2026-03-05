using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OniRepl
{
    public enum ValueType { Element, Quantity, Location, Critter, Building, Number, Symbol }

    public struct StackValue
    {
        public ValueType Type;
        public SimHashes Element;
        public float Kg;
        public int Cell;
        public Tag CritterTag;
        public BuildingDef BuildingDef;
        public int IntValue;
        public string Raw;

        public static StackValue MakeElement(SimHashes hash, string raw) =>
            new StackValue { Type = ValueType.Element, Element = hash, Raw = raw };

        public static StackValue MakeQuantity(float kg, string raw) =>
            new StackValue { Type = ValueType.Quantity, Kg = kg, Raw = raw };

        public static StackValue MakeLocation(int cell, string raw) =>
            new StackValue { Type = ValueType.Location, Cell = cell, Raw = raw };

        public static StackValue MakeCritter(Tag tag, string raw) =>
            new StackValue { Type = ValueType.Critter, CritterTag = tag, Raw = raw };

        public static StackValue MakeBuilding(BuildingDef def, string raw) =>
            new StackValue { Type = ValueType.Building, BuildingDef = def, Raw = raw };

        public static StackValue MakeNumber(int value, string raw) =>
            new StackValue { Type = ValueType.Number, IntValue = value, Raw = raw };

        public static StackValue MakeSymbol(string raw) =>
            new StackValue { Type = ValueType.Symbol, Raw = raw };

        public override string ToString()
        {
            switch (Type)
            {
                case ValueType.Element: return $"[Element: {Raw}]";
                case ValueType.Quantity: return $"[Quantity: {Kg}kg]";
                case ValueType.Location: return $"[Location: cell {Cell}]";
                case ValueType.Critter: return $"[Critter: {Raw}]";
                case ValueType.Building: return $"[Building: {Raw}]";
                case ValueType.Number: return $"[Number: {IntValue}]";
                default: return $"[Symbol: {Raw}]";
            }
        }
    }

    public interface IWord
    {
        string Name { get; }
        string Help { get; }
        bool SuppressAchievements { get; }
        string Execute(Stack<StackValue> stack);
    }

    public class ForthEngine
    {
        public readonly Stack<StackValue> DataStack = new Stack<StackValue>();
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
            return ExecuteTokens(tokens, 0);
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
                    // Save remaining tokens as continuation
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
                    return word.Execute(DataStack);
                }
                catch (InvalidOperationException)
                {
                    return $"Error: stack underflow in '{token}'";
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
                        // Save remaining body tokens, then outer tokens get appended by caller
                        for (int j = i + 1; j < body.Count; j++)
                            continuation.Add(body[j]);
                        break;
                    }
                }
                return output.Count > 0 ? string.Join("\n", output) : null;
            }

            // Try parse as value and push
            PushValue(token);
            return null;
        }

        private void PushValue(string token)
        {
            // Quantity: 1000kg, 500g, 1t
            var match = QuantityRegex.Match(token);
            if (match.Success)
            {
                float value = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                string unit = match.Groups[2].Value.ToLowerInvariant();
                float kg = unit == "g" ? value / 1000f : unit == "t" ? value * 1000f : value;
                DataStack.Push(StackValue.MakeQuantity(kg, token));
                return;
            }

            // Plain integer
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal))
            {
                DataStack.Push(StackValue.MakeNumber(intVal, token));
                return;
            }

            // Location keywords
            if (token.Equals("cursor", StringComparison.OrdinalIgnoreCase))
            {
                int c = LocationResolver.CursorCell();
                Words.BuildState.Cell = c;
                DataStack.Push(StackValue.MakeLocation(c, token));
                return;
            }
            if (token.Equals("printer", StringComparison.OrdinalIgnoreCase))
            {
                int c = LocationResolver.PrinterCell();
                Words.BuildState.Cell = c;
                DataStack.Push(StackValue.MakeLocation(c, token));
                return;
            }

            // Coordinate pair: x,y
            if (LocationResolver.TryParseCoords(token, out int cell))
            {
                Words.BuildState.Cell = cell;
                DataStack.Push(StackValue.MakeLocation(cell, token));
                return;
            }

            // Element name
            if (ElementResolver.TryResolve(token, out SimHashes hash))
            {
                DataStack.Push(StackValue.MakeElement(hash, token));
                return;
            }

            // Critter name
            if (CritterResolver.TryResolve(token, out Tag critterTag))
            {
                DataStack.Push(StackValue.MakeCritter(critterTag, token));
                return;
            }

            // Building name
            if (BuildingResolver.TryResolve(token, out BuildingDef buildingDef))
            {
                DataStack.Push(StackValue.MakeBuilding(buildingDef, token));
                return;
            }

            // Fallback: symbol
            DataStack.Push(StackValue.MakeSymbol(token));
        }

        private List<string> Tokenize(string input)
        {
            var tokens = new List<string>();
            // Strip comments (backslash to end of line)
            foreach (var line in input.Split('\n'))
            {
                var clean = line;
                int commentIdx = clean.IndexOf('\\');
                if (commentIdx >= 0)
                    clean = clean.Substring(0, commentIdx);

                foreach (var token in clean.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                    tokens.Add(token);
            }
            return tokens;
        }
    }
}
