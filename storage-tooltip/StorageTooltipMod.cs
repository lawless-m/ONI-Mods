using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using KMod;

namespace StorageTooltipMod
{
    /// <summary>
    /// Simple mod that shows storage contents when hovering over storage buildings
    /// </summary>
    public class StorageTooltipMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("StorageTooltipMod: Initializing...");
            base.OnLoad(harmony);
            Debug.Log("StorageTooltipMod: Loaded successfully!");
        }
    }

    /// <summary>
    /// Harmony patch to add storage contents to hover tooltips
    /// </summary>
    [HarmonyPatch(typeof(SelectToolHoverTextCard))]
    [HarmonyPatch("UpdateHoverElements")]
    public class SelectToolHoverTextCard_UpdateHoverElements_Patch
    {
        private const int MAX_ITEMS_TO_SHOW = 10;

        // Cached reflection lookups for DLC-only HighEnergyParticleStorage
        private static bool hepTypeResolved;
        private static Type hepType;
        private static PropertyInfo hepParticlesProp;
        private static FieldInfo hepCapacityField;

        /// <summary>
        /// Transpiler patch - injects our code before EndDrawing()
        /// </summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // Find the last call to EndDrawing() and insert our method call before it
            for (int i = codes.Count - 1; i >= 0; i--)
            {
                if (codes[i].opcode == OpCodes.Callvirt &&
                    codes[i].operand != null &&
                    codes[i].operand.ToString().Contains("EndDrawing"))
                {
                    // Insert our method call before EndDrawing
                    var newInstructions = new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Dup), // Duplicate hoverTextDrawer on stack
                        new CodeInstruction(OpCodes.Ldarg_0), // Load 'this' (__instance)
                        new CodeInstruction(OpCodes.Ldarg_1), // Load hoverObjects parameter
                        new CodeInstruction(OpCodes.Call,
                            typeof(SelectToolHoverTextCard_UpdateHoverElements_Patch)
                                .GetMethod(nameof(DrawAllStorageContents),
                                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
                    };

                    codes.InsertRange(i, newInstructions);
                    Debug.Log("StorageTooltipMod: Transpiler patch applied successfully");
                    break;
                }
            }

            return codes;
        }

        /// <summary>
        /// Called from transpiler injection - draws storage for all hovered objects
        /// </summary>
        public static void DrawAllStorageContents(HoverTextDrawer drawer, SelectToolHoverTextCard card, List<KSelectable> hoverObjects)
        {
            try
            {
                if (hoverObjects == null || hoverObjects.Count == 0 || drawer == null || card == null)
                    return;

                // Check each hovered object for storage
                foreach (KSelectable selectable in hoverObjects)
                {
                    if (selectable == null)
                        continue;

                    Storage[] storages = selectable.GetComponents<Storage>();
                    if (storages == null)
                        continue;

                    // SweepBotStation has a showInUI storage for building materials - skip it
                    KPrefabID prefabID = selectable.GetComponent<KPrefabID>();
                    bool isSweepStation = prefabID != null && prefabID.PrefabTag.Name == "SweepBotStation";

                    foreach (Storage storage in storages)
                    {
                        if (!storage.showInUI)
                            continue;

                        if (isSweepStation && storage.fetchCategory == Storage.FetchCategory.Building)
                            continue;

                        List<GameObject> items = storage.GetItems();
                        if (items == null || items.Count == 0)
                            continue;

                        DrawStorageContents(drawer, storage, items, card);
                    }
                }

                // Check each hovered object for radbolt storage (DLC only)
                if (TryResolveHepType())
                {
                    foreach (KSelectable selectable in hoverObjects)
                    {
                        if (selectable == null)
                            continue;

                        Component hepStorage = selectable.GetComponent(hepType);
                        if (hepStorage == null)
                            continue;

                        float capacity = (float)hepCapacityField.GetValue(hepStorage);
                        if (capacity <= 0f)
                            continue;

                        float stored = (float)hepParticlesProp.GetValue(hepStorage);
                        DrawRadboltStorageContents(drawer, stored, capacity, card);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"StorageTooltipMod error: {ex}");
            }
        }

        private static void DrawStorageContents(HoverTextDrawer drawer, Storage storage, List<GameObject> items, SelectToolHoverTextCard card)
        {
            // Start a new shadow bar for storage contents
            drawer.BeginShadowBar(false);

            // Draw title using ONI's localized "CONTENTS" string
            drawer.DrawText(Strings.Get(STRINGS.UI.DETAILTABS.DETAILS.GROUPNAME_CONTENTS.key).String, card.Styles_Title.Standard);

            // Count items by type and sum their masses
            Dictionary<string, ItemInfo> itemSummary = new Dictionary<string, ItemInfo>();

            foreach (GameObject item in items)
            {
                if (item == null)
                    continue;

                KSelectable itemSelectable = item.GetComponent<KSelectable>();
                PrimaryElement primaryElement = item.GetComponent<PrimaryElement>();

                if (itemSelectable == null)
                    continue;

                string itemName = itemSelectable.GetProperName();
                float itemMass = primaryElement != null ? primaryElement.Mass : 0f;

                if (itemSummary.ContainsKey(itemName))
                {
                    itemSummary[itemName].count++;
                    itemSummary[itemName].totalMass += itemMass;
                }
                else
                {
                    itemSummary[itemName] = new ItemInfo
                    {
                        count = 1,
                        totalMass = itemMass,
                        displayName = itemName
                    };
                }
            }

            // Draw items (limit to MAX_ITEMS_TO_SHOW)
            int displayedCount = 0;
            foreach (var kvp in itemSummary)
            {
                if (displayedCount >= MAX_ITEMS_TO_SHOW)
                {
                    drawer.NewLine(26);
                    drawer.DrawIcon(card.iconDash, 18);
                    // Use ONI's localized "{0} more" string
                    drawer.DrawText(string.Format(Strings.Get(STRINGS.UI.UISIDESCREENS.MINIONTODOSIDESCREEN.TRUNCATED_CHORES.key).String, itemSummary.Count - MAX_ITEMS_TO_SHOW), card.Styles_BodyText.Standard);
                    break;
                }

                ItemInfo info = kvp.Value;
                string itemText;

                if (info.count > 1)
                {
                    itemText = $"{info.displayName} x{info.count} ({GameUtil.GetFormattedMass(info.totalMass, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}")})";
                }
                else
                {
                    itemText = $"{info.displayName} ({GameUtil.GetFormattedMass(info.totalMass, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}")})";
                }

                drawer.NewLine(26);
                drawer.DrawIcon(card.iconDash, 18);
                drawer.DrawText(itemText, card.Styles_BodyText.Standard);

                displayedCount++;
            }

            // Show total mass using ONI's localized "Total" string
            float totalMass = storage.MassStored();
            float capacity = storage.capacityKg;
            drawer.NewLine(26);
            drawer.DrawText($"{Strings.Get(STRINGS.UI.DIAGNOSTICS_SCREEN.TOTAL.key).String}: {GameUtil.GetFormattedMass(totalMass, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}")} / {GameUtil.GetFormattedMass(capacity, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, "{0:0.#}")}", card.Styles_BodyText.Standard);

            drawer.EndShadowBar();
        }

        private static bool TryResolveHepType()
        {
            if (hepTypeResolved)
                return hepType != null;

            hepTypeResolved = true;
            hepType = Type.GetType("HighEnergyParticleStorage, Assembly-CSharp");
            if (hepType == null)
                return false;

            hepParticlesProp = hepType.GetProperty("Particles");
            hepCapacityField = hepType.GetField("capacity");
            if (hepParticlesProp == null || hepCapacityField == null)
            {
                hepType = null;
                return false;
            }

            return true;
        }

        private static void DrawRadboltStorageContents(HoverTextDrawer drawer, float stored, float capacity, SelectToolHoverTextCard card)
        {
            drawer.BeginShadowBar(false);

            string radboltLabel = Strings.Get(STRINGS.UI.UNITSUFFIXES.HIGHENERGYPARTICLES.PARTRICLES.key).String.Trim();
            drawer.DrawText(radboltLabel, card.Styles_Title.Standard);

            drawer.NewLine(26);
            drawer.DrawText($"{stored:0.#} / {capacity:0.#}", card.Styles_BodyText.Standard);

            drawer.EndShadowBar();
        }

        private class ItemInfo
        {
            public int count;
            public float totalMass;
            public string displayName;
        }
    }
}
