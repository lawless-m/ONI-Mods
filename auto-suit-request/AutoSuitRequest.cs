using HarmonyLib;
using KMod;
using UnityEngine;

namespace AutoSuitRequest
{
    public class AutoSuitRequestMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("AutoSuitRequest: Loading...");
            base.OnLoad(harmony);
            Debug.Log("AutoSuitRequest: Loaded successfully!");
        }
    }

    /// <summary>
    /// When a new suit locker is built, automatically request a suit delivery
    /// instead of requiring the player to manually click "Request Suit".
    /// Only fires for unconfigured lockers — respects saved state on load.
    /// </summary>
    [HarmonyPatch(typeof(SuitLocker), "OnSpawn")]
    public class SuitLocker_OnSpawn_Patch
    {
        public static void Postfix(SuitLocker __instance)
        {
            if (!__instance.smi.sm.isConfigured.Get(__instance.smi))
            {
                __instance.ConfigRequestSuit();
            }
        }
    }

    /// <summary>
    /// If a suit dock has been empty for a full cycle (600s), automatically
    /// request a replacement. Handles lost/dropped suits without interfering
    /// with normal suit cycling (timer resets each time the dock empties).
    /// </summary>
    [HarmonyPatch(typeof(SuitLocker.States), nameof(SuitLocker.States.InitializeStates))]
    public class SuitLocker_States_InitializeStates_Patch
    {
        public static void Postfix(SuitLocker.States __instance)
        {
            __instance.empty.configured.ToggleScheduleCallback(
                "AutoRequestSuit",
                (SuitLocker.StatesInstance smi) => 600f,
                (SuitLocker.StatesInstance smi) => smi.master.ConfigRequestSuit());
        }
    }

    /// <summary>
    /// Allow suit returns to docks that are requesting via fetch chore.
    /// Without this, a dock in waitingforsuit state rejects checkpoint
    /// returns, causing suits to be dropped on the ground.
    /// </summary>
    [HarmonyPatch(typeof(SuitLocker), nameof(SuitLocker.CanDropOffSuit))]
    public class SuitLocker_CanDropOffSuit_Patch
    {
        public static void Postfix(SuitLocker __instance, ref bool __result)
        {
            if (!__result
                && __instance.smi.sm.isConfigured.Get(__instance.smi)
                && __instance.GetStoredOutfit() == null)
            {
                __result = true;
            }
        }
    }
}
