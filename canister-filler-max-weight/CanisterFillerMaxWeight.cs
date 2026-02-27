using System;
using HarmonyLib;
using UnityEngine;
using KMod;

namespace CanisterFillerMaxWeight
{
    public class CanisterFillerMaxWeightMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("CanisterFillerMaxWeight: Initializing...");
            base.OnLoad(harmony);
            Debug.Log("CanisterFillerMaxWeight: Loaded successfully!");
        }
    }

    /// <summary>
    /// Patch GasBottlerConfig.DoPostConfigureComplete to set the default
    /// user capacity slider to 200kg (the maximum) instead of 25kg.
    /// This affects the building prefab, so all newly built Canister Fillers
    /// will default to max. Existing buildings loaded from saves keep their
    /// saved slider value due to serialization.
    /// </summary>
    [HarmonyPatch(typeof(GasBottlerConfig))]
    [HarmonyPatch("DoPostConfigureComplete")]
    public class GasBottlerConfig_DoPostConfigureComplete_Patch
    {
        public static void Postfix(GameObject go)
        {
            try
            {
                var userControlled = go.GetComponent<IUserControlledCapacity>();
                if (userControlled != null)
                {
                    float max = userControlled.MaxCapacity;
                    if (max > 0f)
                    {
                        userControlled.UserMaxCapacity = max;
                        Debug.Log($"CanisterFillerMaxWeight: Set default capacity to {max} kg");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"CanisterFillerMaxWeight: Error setting default capacity: {ex}");
            }
        }
    }
}
