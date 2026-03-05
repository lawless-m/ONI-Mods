using System.Collections.Generic;
using System.Linq;

namespace OniRepl.Words
{
    public class ListWord : IWord
    {
        public string Name => "list";
        public string Help => "category list — List game objects. Categories: dupes, buildings, buildables, critters, elements";
        public bool SuppressAchievements => false;

        public string Execute(Stack<StackValue> stack)
        {
            var cat = stack.Pop();
            string category = cat.Raw.ToLowerInvariant();

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
                default:
                    return $"Unknown category '{category}'. Try: dupes, buildings, buildables, critters, elements";
            }
        }

        private string ListDupes()
        {
            var dupes = Components.LiveMinionIdentities.Items;
            if (dupes == null || dupes.Count == 0)
                return "No duplicants found";
            var names = dupes.Select(d => d.name).ToList();
            return $"Duplicants ({names.Count}):\n  " + string.Join("\n  ", names);
        }

        private string ListBuildings()
        {
            var buildings = Components.BuildingCompletes.Items;
            if (buildings == null || buildings.Count == 0)
                return "No buildings found";
            var groups = buildings.GroupBy(b => b.name)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}: {g.Count()}");
            return $"Buildings ({buildings.Count} total):\n  " + string.Join("\n  ", groups);
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
                .Select(g => $"{g.Key}: {g.Count()}");
            var list = critters.ToList();
            if (list.Count == 0)
                return "No critters found";
            return $"Critters:\n  " + string.Join("\n  ", list);
        }

        private string ListBuildables()
        {
            var defs = Assets.BuildingDefs;
            if (defs == null || defs.Count == 0)
                return "No building defs loaded";
            var names = defs.OrderBy(d => d.PrefabID).Select(d => d.PrefabID);
            return $"Buildable types ({defs.Count}):\n  " + string.Join("\n  ", names);
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
            return $"Elements ({elements.Count}):\n  " + string.Join("\n  ", names);
        }
    }
}
