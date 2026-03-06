using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniRepl.Words
{
    public class ListWord : IWord
    {
        public string Name => "list";
        public string Help => "category list — List game objects. E.g.: dupes list, buildings list, buildables list, critters list, elements list, items list, geysers list";
        public bool SuppressAchievements => false;

        public string Execute()
        {
            string category = (Registers.Symbol ?? "").ToLowerInvariant();

            switch (category)
            {
                case "dupes":
                case "duplicants":
                    return ListDupes();
                case "buildings":
                    return ListBuildings();
                case "critters":
                case "creatures":
                    return ListCritters();
                case "buildables":
                    return ListBuildables();
                case "elements":
                    return ListElements();
                case "items":
                    return ListItems();
                case "geysers":
                    return ListGeysers();
                default:
                    return $"Unknown category '{category}'. Try: dupes, buildings, buildables, critters, elements, items, geysers";
            }
        }

        private string ListDupes()
        {
            var dupes = Components.LiveMinionIdentities.Items;
            if (dupes == null || dupes.Count == 0)
                return "No duplicants found";
            var names = dupes.Select(d => d.name);
            return $"Duplicants ({dupes.Count}): " + string.Join(", ", names);
        }

        private string ListBuildings()
        {
            var buildings = Components.BuildingCompletes.Items;
            if (buildings == null || buildings.Count == 0)
                return "No buildings found";
            var groups = buildings.GroupBy(b => b.name)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key} ({g.Count()})");
            return $"Buildings ({buildings.Count}): " + string.Join(", ", groups);
        }

        private string ListCritters()
        {
            var brains = Components.Brains.Items;
            if (brains == null || brains.Count == 0)
                return "No critters found";
            var critters = brains
                .Where(b => b.GetComponent<CreatureBrain>() != null)
                .GroupBy(b => b.name)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key} ({g.Count()})");
            var list = critters.ToList();
            if (list.Count == 0)
                return "No critters found";
            return "Critters: " + string.Join(", ", list);
        }

        private string ListBuildables()
        {
            var defs = Assets.BuildingDefs;
            if (defs == null || defs.Count == 0)
                return "No building defs loaded";
            var names = defs.OrderBy(d => d.PrefabID).Select(d => d.PrefabID);
            return $"Buildables ({defs.Count}):\n" + GroupByInitial(names);
        }

        private string ListElements()
        {
            var elements = ElementLoader.elements;
            if (elements == null || elements.Count == 0)
                return "No elements loaded";
            var names = elements
                .Where(e => e.id != SimHashes.Vacuum && e.id != SimHashes.Void)
                .OrderBy(e => e.id.ToString())
                .Select(e => e.id.ToString());
            return $"Elements ({elements.Count}):\n" + GroupByInitial(names);
        }

        private string ListItems()
        {
            var categories = new[]
            {
                new { Name = "Food", Tag = GameTags.Edible },
                new { Name = "Seeds", Tag = GameTags.Seed },
                new { Name = "Medicine", Tag = GameTags.Medicine },
                new { Name = "Ore", Tag = GameTags.Ore },
            };

            var lines = new List<string>();
            var seen = new HashSet<string>();

            foreach (var cat in categories)
            {
                var prefabs = Assets.GetPrefabsWithTag(cat.Tag);
                if (prefabs == null || prefabs.Count == 0) continue;
                var names = prefabs
                    .Select(p => p.GetComponent<KPrefabID>()?.PrefabTag.Name)
                    .Where(n => n != null)
                    .OrderBy(n => n)
                    .ToList();
                foreach (var n in names) seen.Add(n);
                lines.Add($"  {cat.Name}: {string.Join(", ", names)}");
            }

            // Everything else that's pickupable but not in the above categories
            var misc = Assets.GetPrefabsWithTag(GameTags.Pickupable);
            if (misc != null)
            {
                var others = misc
                    .Select(p => p.GetComponent<KPrefabID>()?.PrefabTag.Name)
                    .Where(n => n != null && !seen.Contains(n))
                    .OrderBy(n => n)
                    .ToList();
                if (others.Count > 0)
                    lines.Add($"  Other: {string.Join(", ", others)}");
            }

            return lines.Count > 0 ? "Items:\n" + string.Join("\n", lines) : "No items found";
        }

        private string ListGeysers()
        {
            var geysers = UnityEngine.Object.FindObjectsOfType<Geyser>();
            if (geysers == null || geysers.Length == 0)
                return "No geysers found";
            var entries = geysers
                .Select(g =>
                {
                    int cell = Grid.PosToCell(g.transform.GetPosition());
                    Grid.CellToXY(cell, out int x, out int y);
                    var name = g.GetComponent<KPrefabID>()?.GetProperName() ?? g.gameObject.name;
                    return $"{name} ({x},{y})";
                })
                .OrderBy(e => e);
            return $"Geysers ({geysers.Length}):\n  " + string.Join("\n  ", entries);
        }

        private static string GroupByInitial(IEnumerable<string> items)
        {
            var groups = items.GroupBy(s => char.ToUpper(s[0])).OrderBy(g => g.Key);
            return string.Join("\n", groups.Select(g => $"  {g.Key}: {string.Join(", ", g)}"));
        }
    }
}
