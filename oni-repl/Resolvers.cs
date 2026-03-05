using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace OniRepl
{
    public static class ElementResolver
    {
        private static Dictionary<string, SimHashes> lookup;

        private static void EnsureLookup()
        {
            if (lookup != null) return;
            lookup = new Dictionary<string, SimHashes>(StringComparer.OrdinalIgnoreCase);
            foreach (var elem in ElementLoader.elements)
            {
                // Store by various name forms for flexible matching
                lookup[elem.id.ToString()] = elem.id;
                lookup[elem.name] = elem.id;
                // Strip spaces for easy typing: "polluted water" -> "pollutedwater"
                var noSpaces = elem.id.ToString().Replace(" ", "");
                if (!lookup.ContainsKey(noSpaces))
                    lookup[noSpaces] = elem.id;
            }
        }

        public static bool TryResolve(string name, out SimHashes hash)
        {
            EnsureLookup();

            // Exact match first
            if (lookup.TryGetValue(name, out hash))
                return true;

            // Partial prefix match
            foreach (var kvp in lookup)
            {
                if (kvp.Key.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    hash = kvp.Value;
                    return true;
                }
            }

            hash = SimHashes.Vacuum;
            return false;
        }

        public static void Reset() => lookup = null;
    }

    public static class LocationResolver
    {
        public static int CursorCell()
        {
            var mousePos = KInputManager.GetMousePos();
            var worldPos = Camera.main.ScreenToWorldPoint(mousePos);
            return Grid.PosToCell(worldPos);
        }

        public static int PrinterCell()
        {
            var telepads = Components.Telepads.Items;
            if (telepads != null && telepads.Count > 0)
                return Grid.PosToCell(telepads[0].transform.GetPosition());
            return Grid.InvalidCell;
        }

        public static bool TryParseCoords(string token, out int cell)
        {
            cell = Grid.InvalidCell;
            var parts = token.Split(',');
            if (parts.Length != 2) return false;

            if (int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
                int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
            {
                cell = Grid.XYToCell(x, y);
                return Grid.IsValidCell(cell);
            }
            return false;
        }
    }

    public static class CritterResolver
    {
        private static Dictionary<string, Tag> lookup;

        private static void EnsureLookup()
        {
            if (lookup != null) return;
            lookup = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
            foreach (var prefab in Assets.GetPrefabsWithTag(GameTags.Creature))
            {
                var kpid = prefab.GetComponent<KPrefabID>();
                if (kpid == null) continue;
                var tag = kpid.PrefabTag;
                var name = tag.Name;
                lookup[name] = tag;
                // Also store without "Baby" suffix so "hatch" matches adult
                // but "babyhatch" still works
                if (name.StartsWith("Baby", StringComparison.OrdinalIgnoreCase))
                    lookup[name] = tag;
            }
        }

        public static bool TryResolve(string name, out Tag tag)
        {
            EnsureLookup();

            if (lookup.TryGetValue(name, out tag))
                return true;

            // Prefix match
            foreach (var kvp in lookup)
            {
                if (kvp.Key.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    tag = kvp.Value;
                    return true;
                }
            }

            tag = Tag.Invalid;
            return false;
        }

        public static void Reset() => lookup = null;
    }

    public static class BuildingResolver
    {
        private static Dictionary<string, BuildingDef> lookup;

        private static void EnsureLookup()
        {
            if (lookup != null) return;
            lookup = new Dictionary<string, BuildingDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in Assets.BuildingDefs)
            {
                lookup[def.PrefabID] = def;
                // Also store with spaces removed for easier typing
                var noSpaces = def.PrefabID.Replace(" ", "");
                if (!lookup.ContainsKey(noSpaces))
                    lookup[noSpaces] = def;
            }
        }

        public static bool TryResolve(string name, out BuildingDef def)
        {
            EnsureLookup();

            if (lookup.TryGetValue(name, out def))
                return true;

            // Prefix match
            foreach (var kvp in lookup)
            {
                if (kvp.Key.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    def = kvp.Value;
                    return true;
                }
            }

            def = null;
            return false;
        }

        public static void Reset() => lookup = null;
    }
}
