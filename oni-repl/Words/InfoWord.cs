using System.Collections.Generic;

namespace OniRepl.Words
{
    public class InfoWord : IWord
    {
        public string Name => "info";
        public string Help => "info — Show cell info at current position. E.g.: cursor info";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            // Discard location on top if present (already captured in BuildState)
            if (stack.Count > 0 && stack.Peek().Type == ValueType.Location)
                stack.Pop();

            int cell = BuildState.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: no position set (use cursor, printer, or x,y first)";

            var element = Grid.Element[cell];
            float mass = Grid.Mass[cell];
            float temp = Grid.Temperature[cell];

            int x, y;
            Grid.CellToXY(cell, out x, out y);

            return $"Cell ({x},{y}) #{cell}\n" +
                   $"  Element: {element.name} ({element.id})\n" +
                   $"  Mass: {mass:F1}kg\n" +
                   $"  Temp: {temp:F1}K ({temp - 273.15f:F1}C)";
        }
    }
}
