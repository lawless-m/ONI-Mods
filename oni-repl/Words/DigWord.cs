using System.Collections.Generic;
using UnityEngine;

namespace OniRepl.Words
{
    public class DigWord : IWord
    {
        public string Name => "dig";
        public string Help => "dig — Dig at current position. E.g.: cursor dig | Chain: cursor dig right dig right dig";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            // Discard location on top if present (already captured in BuildState)
            if (stack.Count > 0 && stack.Peek().Type == ValueType.Location)
                stack.Pop();

            int cell = BuildState.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: no position set (use cursor, printer, or x,y first)";

            if (!Grid.Solid[cell])
                return $"Error: cell {cell} is not solid";

            if (Grid.Foundation[cell])
                return $"Error: cell {cell} is a foundation";

            var go = DigTool.PlaceDig(cell);
            if (go == null)
                return $"Error: cannot place dig at cell {cell}";

            var prioritizable = go.GetComponent<Prioritizable>();
            if (prioritizable != null)
                prioritizable.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic, BuildState.Priority));

            return $"Dig order at cell {cell} p{BuildState.Priority}";
        }
    }
}
