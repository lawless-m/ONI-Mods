using System.Collections.Generic;
using UnityEngine;

namespace OniRepl.Words
{
    public class PriorityWord : IWord
    {
        public string Name => "priority";
        public string Help => "n priority — Set build priority (1-9). E.g.: 9 priority";
        public bool SuppressAchievements => false;

        public string Execute()
        {
            int p = Registers.Count;
            if (p < 1 || p > 9)
                return $"Error: priority must be 1-9, got {p}";
            Registers.Priority = p;
            return $"Build priority set to {p}";
        }
    }

    public class BuildWord : IWord
    {
        internal static readonly Dictionary<int, string> TrackedBuilds = new Dictionary<int, string>();

        public string Name => "build";
        public string Help => "build — Build at current position. E.g.: granite tile cursor build | Chain: granite tile cursor build right build";
        public bool SuppressAchievements => false;

        public string Execute()
        {
            int cell = Registers.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: no position set (use cursor, printer, or x,y first)";

            var def = Registers.Building;
            if (def == null)
                return "Error: no building set. E.g.: tile build";

            if (Registers.Material == default(SimHashes))
                return "Error: no material set. E.g.: granite tile build";

            var materials = MakeMaterials(Registers.Material, def);
            var go = PlaceBuildOrder(def, cell, materials);
            if (go == null)
                return $"Error: cannot place {def.PrefabID} at cell {cell} (blocked or invalid)";

            return $"Placed build order: {def.PrefabID} ({materials[0]}) p{Registers.Priority} at cell {cell}";
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
            var orientation = Registers.Orientation;
            Registers.Orientation = Orientation.Neutral;
            var go = def.TryPlace(null, pos, orientation, materials, 0);
            if (go == null) return null;

            var prioritizable = go.GetComponent<Prioritizable>();
            if (prioritizable != null)
                prioritizable.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic, Registers.Priority));

            TrackedBuilds[cell] = def.PrefabID;
            return go;
        }
    }

    public class ClearWord : IWord
    {
        private readonly ForthEngine engine;

        public ClearWord(ForthEngine engine) { this.engine = engine; }

        public string Name => "clear";
        public string Help => "clear — Cancel wait, clear tracked builds and queued commands";
        public bool SuppressAchievements => false;

        public string Execute()
        {
            int count = BuildWord.TrackedBuilds.Count;
            BuildWord.TrackedBuilds.Clear();
            engine.Suspended = false;
            engine.ClearContinuation();
            return count > 0 ? $"Cleared {count} tracked build(s)" : "Nothing to clear";
        }
    }

    public class WaitWord : IWord
    {
        private readonly ForthEngine engine;

        public WaitWord(ForthEngine engine) { this.engine = engine; }

        public string Name => "wait";
        public string Help => "wait — Pause until tracked builds complete. E.g.: granite tile cursor build right build wait";
        public bool SuppressAchievements => false;

        public string Execute()
        {
            if (BuildWord.TrackedBuilds.Count == 0)
                return null;

            engine.Suspended = true;
            return $"Waiting for {BuildWord.TrackedBuilds.Count} build(s)...";
        }
    }
}
