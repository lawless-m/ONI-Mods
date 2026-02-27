using HarmonyLib;
using KMod;
using UnityEngine;

namespace AutoGeneShuffler
{
    public class AutoGeneShufflerMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("AutoGeneShuffler: Loading...");
            base.OnLoad(harmony);
            Debug.Log("AutoGeneShuffler: Loaded successfully!");
        }
    }

    /// <summary>
    /// Patches the GeneShuffler (Neural Vacillator) to auto-complete after a short delay
    /// instead of waiting indefinitely for a manual button press.
    ///
    /// Vanilla flow:
    ///   idle -> working.pre -> working.loop (5s) -> working.complete (infinite wait) -> working.pst
    ///
    /// Patched flow:
    ///   idle -> working.pre -> working.loop (5s) -> working.complete (5s) -> working.pst
    /// </summary>
    [HarmonyPatch(typeof(GeneShuffler.GeneShufflerSM))]
    [HarmonyPatch(nameof(GeneShuffler.GeneShufflerSM.InitializeStates))]
    public class GeneShufflerSM_InitializeStates_Patch
    {
        public static void Postfix(GeneShuffler.GeneShufflerSM __instance)
        {
            // After 5s in the complete state, set work time to 0 which triggers
            // OnCompleteWork (applies the random trait) via WorkableStopTransition
            __instance.working.complete.Enter(delegate(GeneShuffler.GeneShufflerSM.Instance smi)
            {
                smi.Schedule(5f, delegate(object data)
                {
                    if (smi.IsInsideState(smi.sm.working.complete))
                        smi.master.SetWorkTime(0f);
                });
            });
            Debug.Log("AutoGeneShuffler: Patched working.complete to auto-finish after 5s");
        }
    }
}
