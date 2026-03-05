using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniRepl.Words
{
    public static class BuildState
    {
        public static int Cell = Grid.InvalidCell;
        public static int Priority = 5;
    }

    public class PriorityWord : IWord
    {
        public string Name => "priority";
        public string Help => "n priority — Set build priority (1-9). E.g.: 9 priority";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            var val = stack.Pop();
            if (val.Type != ValueType.Number)
                return $"Error: expected number, got {val}";
            int p = val.IntValue;
            if (p < 1 || p > 9)
                return $"Error: priority must be 1-9, got {p}";
            BuildState.Priority = p;
            return $"Build priority set to {p}";
        }
    }

    public class BuildWord : IWord
    {
        internal static readonly Dictionary<int, string> TrackedBuilds = new Dictionary<int, string>();

        public string Name => "build";
        public string Help => "[material] building build — Build at current position. Material and building stay on stack.";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            int cell = BuildState.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: no build position set (use cursor, printer, or x,y first)";

            // Peek building (top)
            if (stack.Count == 0 || stack.Peek().Type != ValueType.Building)
                return $"Error: expected building on stack";
            var bldgVal = stack.Pop();
            var def = bldgVal.BuildingDef;

            // Peek material (under building)
            Tag[] materials;
            if (stack.Count > 0 && stack.Peek().Type == ValueType.Element)
                materials = MakeMaterials(stack.Peek().Element, def);
            else
                materials = GetDefaultMaterials(def);

            // Push building back
            stack.Push(bldgVal);

            if (materials == null)
                return $"Error: could not find materials for {def.PrefabID}";

            var go = PlaceBuildOrder(def, cell, materials);
            if (go == null)
                return $"Error: cannot place {def.PrefabID} at cell {cell} (blocked or invalid)";

            return $"Placed build order: {def.PrefabID} ({materials[0]}) p{BuildState.Priority} at cell {cell}";
        }

        internal static Tag[] MakeMaterials(SimHashes element, BuildingDef def)
        {
            var materials = new Tag[def.MaterialCategory.Length];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = element.ToString();
            return materials;
        }

        internal static GameObject PlaceBuildOrder(BuildingDef def, int cell, Tag[] materials)
        {
            var pos = Grid.CellToPosCBC(cell, def.SceneLayer);
            var go = def.TryPlace(null, pos, Orientation.Neutral, materials, 0);
            if (go == null) return null;

            var prioritizable = go.GetComponent<Prioritizable>();
            if (prioritizable != null)
                prioritizable.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic, BuildState.Priority));

            TrackedBuilds[cell] = def.PrefabID;
            return go;
        }

        internal static Tag[] GetDefaultMaterials(BuildingDef def)
        {
            var result = new Tag[def.MaterialCategory.Length];
            for (int i = 0; i < def.MaterialCategory.Length; i++)
            {
                var category = def.MaterialCategory[i];
                var elem = ElementLoader.elements.FirstOrDefault(
                    e => e.oreTags != null && e.oreTags.Contains(category));
                if (elem == null)
                    return null;
                result[i] = elem.tag;
            }
            return result;
        }
    }

    public class WaitWord : IWord
    {
        private readonly ForthEngine engine;

        public WaitWord(ForthEngine engine) { this.engine = engine; }

        public string Name => "wait";
        public string Help => "wait — Wait for all tracked build orders to complete before continuing";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            if (BuildWord.TrackedBuilds.Count == 0)
                return null;

            engine.Suspended = true;
            return $"Waiting for {BuildWord.TrackedBuilds.Count} build(s)...";
        }
    }
}
