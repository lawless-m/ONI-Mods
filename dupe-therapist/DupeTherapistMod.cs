using HarmonyLib;
using KMod;
using UnityEngine;

namespace DupeTherapist
{
    public class DupeTherapistMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("DupeTherapist: Loading...");
            base.OnLoad(harmony);
            Debug.Log("DupeTherapist: Loaded!");
        }
    }

    [HarmonyPatch(typeof(Game), "OnSpawn")]
    public static class Game_OnSpawn_Patch
    {
        public static void Postfix(Game __instance)
        {
            var go = new GameObject("DupeTherapist");
            go.transform.SetParent(__instance.transform);
            go.AddComponent<TherapistBehaviour>();
        }
    }

    [HarmonyPatch(typeof(PlayerController), "OnKeyDown")]
    public static class PlayerController_OnKeyDown_Patch
    {
        public static bool Prefix()
        {
            return !TherapistBehaviour.BlockInput;
        }
    }
}
