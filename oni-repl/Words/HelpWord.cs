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
        public string Help => "help — Show all words";
        public bool SuppressAchievements => false;

        public string Execute()
        {
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
            lines.Add("Registers: element sets material, building sets building, cursor/x,y sets position");
            lines.Add("Define words: : name body... ;    Loops: N do body... loop");
            return string.Join("\n", lines);
        }
    }
}
