using System.Collections.Generic;

namespace OniRepl.Words
{
    public class FontSizeWord : IWord
    {
        public string Name => "fontsize";
        public string Help => "n fontsize — Set console font size. E.g.: 28 fontsize";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            var val = stack.Pop();
            if (val.Type != ValueType.Number)
                return $"Error: expected number, got {val}";

            int size = val.IntValue;
            if (size < 8 || size > 72)
                return $"Error: font size must be 8-72, got {size}";

            var console = ReplConsole.Instance;
            if (console != null)
                console.SetFontSize(size);

            return $"Font size set to {size}";
        }
    }
}
