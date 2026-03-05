using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OniRepl.Words
{
    public class SaveWord : IWord
    {
        private readonly ForthEngine engine;

        public SaveWord(ForthEngine engine) { this.engine = engine; }

        public string Name => "save";
        public string Help => "save — Save user-defined words to init.forth";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            var words = engine.UserWords;
            if (words.Count == 0)
                return "Nothing to save (no user-defined words)";

            var lines = new List<string> { @"\ ONI REPL saved words" };
            foreach (var kvp in words.OrderBy(w => w.Key))
                lines.Add($": {kvp.Key} {string.Join(" ", kvp.Value)} ;");

            var path = Path.Combine(ModDirectory, "init.forth");
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
            return $"Saved {words.Count} word(s) to {path}";
        }

        internal static string ModDirectory =>
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    }

    public class LoadWord : IWord
    {
        private readonly ForthEngine engine;

        public LoadWord(ForthEngine engine) { this.engine = engine; }

        public string Name => "load";
        public string Help => "load — Load init.forth. Or: filename load — Load a .forth file";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            string filename = "init.forth";
            if (stack.Count > 0 && stack.Peek().Type == ValueType.Symbol)
            {
                var sym = stack.Pop();
                filename = sym.Raw;
                if (!filename.EndsWith(".forth"))
                    filename += ".forth";
            }

            var path = Path.Combine(SaveWord.ModDirectory, filename);
            if (!File.Exists(path))
                return $"File not found: {filename}";

            var content = File.ReadAllText(path);
            var result = engine.Execute(content);
            return result ?? $"Loaded {path}";
        }
    }
}
