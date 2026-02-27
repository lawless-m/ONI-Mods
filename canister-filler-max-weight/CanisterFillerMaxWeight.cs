using HarmonyLib;
using UnityEngine;
using KMod;

namespace CanisterFillerMaxWeight
{
    public class CanisterFillerMaxWeightMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("CanisterFillerMaxWeight: Loading...");
            base.OnLoad(harmony);
            Debug.Log("CanisterFillerMaxWeight: Loaded successfully!");
        }
    }

    /// <summary>
    /// Patch GasBottlerConfig.DoPostConfigureComplete to set the default
    /// user capacity slider to max (200kg) instead of 25kg.
    /// Existing buildings loaded from saves keep their saved slider value.
    /// </summary>
    [HarmonyPatch(typeof(GasBottlerConfig))]
    [HarmonyPatch("DoPostConfigureComplete")]
    public class GasBottlerConfig_DoPostConfigureComplete_Patch
    {
        public static void Postfix(GameObject go)
        {
            var bottler = go.GetComponent<Bottler>();
            if (bottler != null)
            {
                bottler.UserMaxCapacity = bottler.MaxCapacity;
                Debug.Log($"CanisterFillerMaxWeight: Set default capacity to {bottler.MaxCapacity} kg");
            }
        }
    }
}
