namespace OniRepl.Words
{
    public class FlipWord : IWord
    {
        public string Name => "flip";
        public string Help => "flip — Toggle building orientation (Neutral → FlipH → FlipV → Neutral)";
        public bool SuppressAchievements => false;

        public string Execute()
        {
            switch (Registers.Orientation)
            {
                case Orientation.Neutral:
                    Registers.Orientation = Orientation.FlipH;
                    break;
                case Orientation.FlipH:
                    Registers.Orientation = Orientation.FlipV;
                    break;
                default:
                    Registers.Orientation = Orientation.Neutral;
                    break;
            }
            return $"Orientation: {Registers.Orientation}";
        }
    }
}
