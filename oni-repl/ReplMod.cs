using HarmonyLib;
using KMod;
using UnityEngine;

namespace OniRepl
{
    public class ReplMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("OniRepl: Loading...");
            base.OnLoad(harmony);
            Debug.Log("OniRepl: Loaded successfully!");
        }
    }

    [HarmonyPatch(typeof(Game), "OnSpawn")]
    public static class Game_OnSpawn_Patch
    {
        public static void Postfix(Game __instance)
        {
            var go = new GameObject("OniRepl");
            go.transform.SetParent(__instance.transform);
            go.AddComponent<ReplConsole>();
        }
    }

    [HarmonyPatch(typeof(Constructable), "OnCompleteWork")]
    public static class Constructable_OnCompleteWork_Patch
    {
        public static void Postfix(Constructable __instance)
        {
            int cell = Grid.PosToCell(__instance.transform.GetPosition());
            if (Words.BuildWord.TrackedBuilds.TryGetValue(cell, out string name))
            {
                Words.BuildWord.TrackedBuilds.Remove(cell);
                var console = ReplConsole.Instance;
                if (console == null) return;

                console.PostNotification($"[Built] {name} at cell {cell}");

                if (Words.BuildWord.TrackedBuilds.Count == 0)
                    console.ResumeEngine();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerController), "OnKeyDown")]
    public static class PlayerController_OnKeyDown_Patch
    {
        public static bool Prefix()
        {
            return !ReplConsole.IsVisible;
        }
    }
}
