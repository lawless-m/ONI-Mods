using System.Collections.Generic;

namespace OniRepl.Words
{
    public class FillWord : IWord
    {
        public string Name => "fill";
        public string Help => "material building x1,y1 x2,y2 fill — Fill rectangle. E.g.: sandstone tile 50,30 55,33 fill";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            var end = stack.Pop();
            if (end.Type != ValueType.Location)
                return $"Error: expected end location, got {end}";

            var start = stack.Pop();
            if (start.Type != ValueType.Location)
                return $"Error: expected start location, got {start}";

            // Peek building (stays on stack)
            if (stack.Count == 0 || stack.Peek().Type != ValueType.Building)
                return $"Error: expected building on stack";
            var bldg = stack.Peek();

            var def = bldg.BuildingDef;

            // Peek material under building (both stay on stack)
            var bldgVal = stack.Pop();
            Tag[] materials;
            if (stack.Count > 0 && stack.Peek().Type == ValueType.Element)
                materials = BuildWord.MakeMaterials(stack.Peek().Element, def);
            else
            {
                stack.Push(bldgVal);
                return $"Error: no material specified. E.g.: sandstone {def.PrefabID} ... fill";
            }
            stack.Push(bldgVal);

            int x1, y1, x2, y2;
            Grid.CellToXY(start.Cell, out x1, out y1);
            Grid.CellToXY(end.Cell, out x2, out y2);

            if (x1 > x2) { int tmp = x1; x1 = x2; x2 = tmp; }
            if (y1 > y2) { int tmp = y1; y1 = y2; y2 = tmp; }

            int placed = 0;
            int failed = 0;
            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    int cell = Grid.XYToCell(x, y);
                    if (!Grid.IsValidCell(cell)) { failed++; continue; }

                    var go = BuildWord.PlaceBuildOrder(def, cell, materials);
                    if (go != null) placed++;
                    else failed++;
                }
            }

            string result = $"Filled {placed} {def.PrefabID} ({materials[0]}) p{BuildState.Priority}";
            if (failed > 0)
                result += $" ({failed} failed)";
            return result;
        }
    }
}
