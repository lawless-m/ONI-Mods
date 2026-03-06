namespace OniRepl.Words
{
    public class RevealWord : IWord
    {
        public string Name => "reveal";
        public string Help => "reveal — Remove fog of war, revealing the entire map (DISABLES ACHIEVEMENTS)";
        public bool SuppressAchievements => true;

        public string Execute()
        {
            int count = 0;
            for (int i = 0; i < Grid.CellCount; i++)
            {
                if (!Grid.Revealed[i])
                {
                    Grid.Revealed[i] = true;
                    count++;
                }
                Grid.Visible[i] = byte.MaxValue;
            }
            return count > 0 ? $"Revealed {count} cells" : "Map already revealed";
        }
    }
}
