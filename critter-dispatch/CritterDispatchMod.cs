using HarmonyLib;
using KMod;
using UnityEngine;

namespace CritterDispatchMod
{
    public class CritterDispatchMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("CritterDispatchMod: Loading...");
            base.OnLoad(harmony);
            Debug.Log("CritterDispatchMod: Loaded successfully!");
        }

        [HarmonyPatch(typeof(GeneratedBuildings))]
        [HarmonyPatch("LoadGeneratedBuildings")]
        public class GeneratedBuildings_LoadGeneratedBuildings_Patch
        {
            public static void Prefix()
            {
                Strings.Add("STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.NAME", global::STRINGS.ITEMS.FOOD.MEAT.NAME);
                Strings.Add("STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.DESC", global::STRINGS.ITEMS.FOOD.MEAT.DESC);
                Strings.Add("STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.EFFECT", STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.EFFECT);
                Strings.Add("STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.LOGIC_PORT", global::STRINGS.BUILDING.STATUSITEMS.CREATURE_REUSABLE_TRAP.READY.NAME);
                Strings.Add("STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.LOGIC_PORT_ACTIVE", STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.LOGIC_PORT_ACTIVE);
                Strings.Add("STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.LOGIC_PORT_INACTIVE", STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.LOGIC_PORT_INACTIVE);
                ModUtil.AddBuildingToPlanScreen("Food", CritterDispatchConfig.ID, "ranching", "CreatureGroundTrap", ModUtil.BuildingOrdering.After);
            }
        }

        [HarmonyPatch(typeof(ReusableTrap.Instance))]
        [HarmonyPatch("RefreshLogicOutput")]
        public class ReusableTrap_Instance_RefreshLogicOutput_Patch
        {
            public static bool Prefix(ReusableTrap.Instance __instance)
            {
                if (__instance.gameObject.GetComponent<CritterDispatch>() == null)
                    return true;

                LogicPorts logicPorts = __instance.gameObject.GetComponent<LogicPorts>();
                bool isArmed = __instance.gameObject.HasTag(GameTags.TrapArmed);
                logicPorts.SendSignal(
                    __instance.def.OUTPUT_LOGIC_PORT_ID,
                    isArmed ? 1 : 0);
                return false;
            }
        }
    }

    public static class STRINGS
    {
        public static class BUILDINGS
        {
            public static class PREFABS
            {
                public static class CRITTERDISPATCH
                {
                    public static LocString NAME = "Critter Dispatch";
                    public static LocString DESC = "A lethal trap that automatically butchers any critter it catches, dropping meat and other resources.";
                    public static LocString EFFECT = "Traps and butchers critters on contact, producing meat drops. Rearms automatically after each dispatch.";
                    public static LocString LOGIC_PORT = "Trap Armed";
                    public static LocString LOGIC_PORT_ACTIVE = "Sends a <b>Green Signal</b> when the trap is armed and ready";
                    public static LocString LOGIC_PORT_INACTIVE = "Otherwise, sends a <b>Red Signal</b>";
                }
            }
        }
    }
}
