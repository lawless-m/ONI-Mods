using System.Collections.Generic;

namespace OniRepl.Words
{
    public class InfoWord : IWord
    {
        public string Name => "info";
        public string Help => "location info — Show cell info (element, mass, temperature). E.g.: cursor info";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            var loc = stack.Pop();
            if (loc.Type != ValueType.Location)
                return $"Error: expected location, got {loc}";

            int cell = loc.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: invalid cell";

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
