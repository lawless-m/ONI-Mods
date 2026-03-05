using System.Collections.Generic;
using System.Linq;

namespace OniRepl.Words
{
    public class HelpWord : IWord
    {
        private readonly ForthEngine engine;

        public HelpWord(ForthEngine engine)
        {
            this.engine = engine;
        }

        public string Name => "help";
        public string Help => "help — Show all words. Or: word help — Show help for specific word";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            // If there's a symbol on the stack, show help for that specific word
            if (stack.Count > 0 && stack.Peek().Type == ValueType.Symbol)
            {
                var sym = stack.Pop();
                if (engine.Builtins.TryGetValue(sym.Raw, out var word))
                    return $"{word.Name}: {word.Help}";
                if (engine.UserWords.ContainsKey(sym.Raw))
                    return $"{sym.Raw}: user-defined word";
                return $"Unknown word: {sym.Raw}";
            }

            // Show all words
            var lines = new List<string> { "Built-in words:" };
            foreach (var word in engine.Builtins.Values.OrderBy(w => w.Name))
                lines.Add($"  {word.Name,-10} {word.Help.Split('—').Last().Trim()}");

            if (engine.UserWords.Count > 0)
            {
                lines.Add("User-defined words:");
                foreach (var name in engine.UserWords.Keys.OrderBy(n => n))
                    lines.Add($"  {name,-10} : {string.Join(" ", engine.UserWords[name])} ;");
            }

            lines.Add("");
            lines.Add("Value types: element (water), quantity (100kg), location (cursor/printer/x,y), critter (hatch), building (tile)");
            lines.Add("Define words: : name body... ;");
            return string.Join("\n", lines);
        }
    }
}
