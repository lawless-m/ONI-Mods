namespace OniRepl.Words
{
    public class FontSizeWord : IWord
    {
        public string Name => "fontsize";
        public string Help => "n fontsize — Set console font size. E.g.: 28 fontsize";
        public bool SuppressAchievements => false;

        public string Execute()
        {
            int size = Registers.Count;
            if (size < 8 || size > 72)
                return $"Error: font size must be 8-72, got {size}";

            var console = ReplConsole.Instance;
            if (console != null)
                console.SetFontSize(size);

            return $"Font size set to {size}";
        }
    }
}
