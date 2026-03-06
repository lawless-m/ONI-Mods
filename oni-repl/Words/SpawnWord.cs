namespace OniRepl.Words
{
    public class SpawnWord : IWord
    {
        public string Name => "spawn";
        public string Help => "spawn — Spawn element at current position (DISABLES ACHIEVEMENTS). E.g.: water 1000kg cursor spawn";
        public bool SuppressAchievements => true;

        public string Execute()
        {
            int cell = Registers.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: no position set (use cursor, printer, or x,y first)";

            var symbol = Registers.Symbol;
            if (string.IsNullOrEmpty(symbol))
                return "Error: no element set. E.g.: water spawn";

            if (!ElementResolver.TryResolve(symbol, out SimHashes hash))
                return $"Error: '{symbol}' is not an element";

            var element = ElementLoader.FindElementByHash(hash);
            float temp = element.defaultValues.temperature;
            if (temp <= 0) temp = 300f;

            float kg = Registers.Quantity;

            SimMessages.AddRemoveSubstance(
                cell, hash, CellEventLogger.Instance.ElementConsumerSimUpdate,
                kg, temp, byte.MaxValue, 0);

            return $"Spawned {kg}kg of {element.id} at cell {cell}";
        }
    }

    public class CritterWord : IWord
    {
        public string Name => "critter";
        public string Help => "critter — Spawn critter at current position (DISABLES ACHIEVEMENTS). E.g.: hatch cursor critter";
        public bool SuppressAchievements => true;

        public string Execute()
        {
            int cell = Registers.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: no position set (use cursor, printer, or x,y first)";

            if (Registers.Critter == Tag.Invalid)
                return "Error: no critter set. E.g.: hatch critter";

            var prefab = Assets.GetPrefab(Registers.Critter);
            if (prefab == null)
                return $"Error: no prefab found for '{Registers.Critter}'";

            var pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Creatures);
            var go = GameUtil.KInstantiate(prefab, pos, Grid.SceneLayer.Creatures);
            go.SetActive(true);

            return $"Spawned {Registers.Critter} at cell {cell}";
        }
    }
}
