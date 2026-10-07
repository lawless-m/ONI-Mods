using HarmonyLib;
using KMod;
using UnityEngine;

namespace BuildingTrackerMod
{
    public class BuildingTrackerMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("BuildingTrackerMod: Loading...");
            base.OnLoad(harmony);
            Debug.Log("BuildingTrackerMod: Loaded successfully!");
        }
    }

    // Attach Tracker component when game starts
    [HarmonyPatch(typeof(Game), "OnSpawn")]
    public static class Game_OnSpawn_Patch
    {
        public static void Postfix(Game __instance)
        {
            var go = new GameObject("BuildingTrackerMod");
            go.transform.SetParent(__instance.transform);
            go.AddComponent<Tracker>();
        }
    }
}
