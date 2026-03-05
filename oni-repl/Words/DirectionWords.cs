using System.Collections.Generic;

namespace OniRepl.Words
{
    public class DirectionWord : IWord
    {
        private readonly string name;
        private readonly int dx;
        private readonly int dy;

        public DirectionWord(string name, int dx, int dy)
        {
            this.name = name;
            this.dx = dx;
            this.dy = dy;
        }

        public string Name => name;
        public string Help => $"{name} — Move build position one cell {name}";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            int x, y;
            Grid.CellToXY(BuildState.Cell, out x, out y);
            BuildState.Cell = Grid.XYToCell(x + dx, y + dy);
            return null;
        }
    }
}
