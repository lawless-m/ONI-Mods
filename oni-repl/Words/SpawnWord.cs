using System.Collections.Generic;
using UnityEngine;

namespace OniRepl.Words
{
    public class SpawnWord : IWord
    {
        public string Name => "spawn";
        public string Help => "thing [quantity] spawn — Spawn at current position. E.g.: water 1000kg cursor spawn, hatch cursor spawn";
        public bool SuppressAchievements => true;

        public string Execute(Stack<StackValue> stack)
        {
            // Discard location on top if present (already captured in BuildState)
            if (stack.Count > 0 && stack.Peek().Type == ValueType.Location)
                stack.Pop();

            int cell = BuildState.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: no position set (use cursor, printer, or x,y first)";

            // Peek to decide: if top is quantity, it's an element spawn
            // If top is element/critter directly, dispatch accordingly
            var thing = stack.Peek();

            if (thing.Type == ValueType.Quantity)
            {
                var qty = stack.Pop();
                var elem = stack.Pop();
                if (elem.Type != ValueType.Element)
                    return $"Error: expected element, got {elem}";
                return SpawnElement(elem, qty.Kg, cell);
            }
            else if (thing.Type == ValueType.Element)
            {
                stack.Pop();
                return SpawnElement(thing, 100f, cell);
            }
            else if (thing.Type == ValueType.Critter)
            {
                stack.Pop();
                return SpawnCritter(thing, cell);
            }
            else
            {
                return $"Error: expected element or critter, got {thing}";
            }
        }

        private string SpawnElement(StackValue elem, float kg, int cell)
        {
            var element = ElementLoader.FindElementByHash(elem.Element);
            float temp = element.defaultValues.temperature;
            if (temp <= 0) temp = 300f;

            SimMessages.AddRemoveSubstance(
                cell, elem.Element, CellEventLogger.Instance.ElementConsumerSimUpdate,
                kg, temp, byte.MaxValue, 0);

            return $"Spawned {kg}kg of {elem.Raw} at cell {cell}";
        }

        private string SpawnCritter(StackValue critter, int cell)
        {
            var prefab = Assets.GetPrefab(critter.CritterTag);
            if (prefab == null)
                return $"Error: no prefab found for '{critter.Raw}'";

            var pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Creatures);
            var go = GameUtil.KInstantiate(prefab, pos, Grid.SceneLayer.Creatures);
            go.SetActive(true);

            return $"Spawned {critter.Raw} at cell {cell}";
        }
    }
}
