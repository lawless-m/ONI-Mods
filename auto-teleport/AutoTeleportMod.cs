using HarmonyLib;
using KMod;
using UnityEngine;

namespace AutoTeleportMod
{
    /// <summary>
    /// Auto-Teleport mod - Automatically teleports duplicants when they enter a Warp Portal
    /// instead of requiring the player to manually click the "Teleport" button.
    /// Requires: Spaced Out! DLC
    /// </summary>
    public class AutoTeleportMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("AutoTeleportMod: Loading...");
            base.OnLoad(harmony);
            Debug.Log("AutoTeleportMod: Loaded successfully!");
        }
    }

    /// <summary>
    /// Patches WarpPortal to auto-trigger teleportation when a dupe enters the waiting state.
    ///
    /// The vanilla flow is:
    ///   idle -> become_occupied -> occupied.get_on -> occupied.waiting (player clicks button) -> occupied.warping -> do_warp
    ///
    /// This patch adds a ScheduleGoTo on the occupied.waiting state so it automatically
    /// transitions to occupied.warping after a short delay (one game tick), bypassing the
    /// manual button press.
    /// </summary>
    [HarmonyPatch(typeof(WarpPortal.WarpPortalSM))]
    [HarmonyPatch(nameof(WarpPortal.WarpPortalSM.InitializeStates))]
    public class WarpPortalSM_InitializeStates_Patch
    {
        public static void Postfix(WarpPortal.WarpPortalSM __instance)
        {
            // Add an auto-transition: after entering occupied.waiting, go to occupied.warping
            // ScheduleGoTo with 0.5f gives a brief pause so the player sees the dupe arrive
            __instance.occupied.waiting.ScheduleGoTo(0.5f, __instance.occupied.warping);
            Debug.Log("AutoTeleportMod: Patched occupied.waiting to auto-warp after 0.5s");
        }
    }
}
