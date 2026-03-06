using UnityEngine;

namespace OniRepl.Words
{
    public class ItemWord : IWord
    {
        public string Name => "item";
        public string Help => "item — Spawn item at current position (DISABLES ACHIEVEMENTS). E.g.: mushbar cursor item";
        public bool SuppressAchievements => true;

        public string Execute()
        {
            int cell = Registers.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: no position set (use cursor, printer, or x,y first)";

            var symbol = Registers.Symbol;
            if (string.IsNullOrEmpty(symbol))
                return "Error: no item specified. E.g.: mushbar item";

            // Search prefabs by name
            GameObject prefab = null;
            string matchedName = null;
            foreach (var kpid in Assets.Prefabs)
            {
                if (kpid == null) continue;
                var name = kpid.PrefabTag.Name;
                if (name.Equals(symbol, System.StringComparison.OrdinalIgnoreCase))
                {
                    prefab = kpid.gameObject;
                    matchedName = name;
                    break;
                }
                if (prefab == null && name.StartsWith(symbol, System.StringComparison.OrdinalIgnoreCase))
                {
                    prefab = kpid.gameObject;
                    matchedName = name;
                }
            }

            if (prefab == null)
                return $"Error: no prefab found matching '{symbol}'";

            var pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Ore);
            var go = GameUtil.KInstantiate(prefab, pos, Grid.SceneLayer.Ore);
            go.SetActive(true);

            return $"Spawned {matchedName} at cell {cell}";
        }
    }
}
