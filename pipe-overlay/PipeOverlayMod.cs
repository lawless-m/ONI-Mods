using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace PipeOverlay
{
    [JsonObject(MemberSerialization.OptIn)]
    [RestartRequired]
    public class PipeOverlayOptions
    {
        [Option("Liquid Pipes", "Color liquid pipe networks")]
        [JsonProperty]
        public bool LiquidPipes { get; set; } = true;

        [Option("Gas Pipes", "Color gas pipe networks")]
        [JsonProperty]
        public bool GasPipes { get; set; } = true;

        [Option("Conveyor Rails", "Color conveyor rail networks")]
        [JsonProperty]
        public bool ConveyorRails { get; set; } = true;

        [Option("Electrical Wires", "Color electrical wire networks")]
        [JsonProperty]
        public bool ElectricalWires { get; set; } = true;

        private static PipeOverlayOptions _instance;
        public static PipeOverlayOptions Instance =>
            _instance ?? (_instance = POptions.ReadSettings<PipeOverlayOptions>() ?? new PipeOverlayOptions());
    }

    public class PipeOverlayMod : KMod.UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("PipeOverlay: Loading...");
            PUtil.InitLibrary();
            new POptions().RegisterOptions(this, typeof(PipeOverlayOptions));
            base.OnLoad(harmony);
            Debug.Log("PipeOverlay: Loaded successfully!");
        }
    }

    public static class NetworkColors
    {
        private static readonly Color32[] Palette = GeneratePalette(16);

        private static Color32[] GeneratePalette(int count)
        {
            var colors = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                float hue = (float)i / count;
                Color c = Color.HSVToRGB(hue, 0.7f, 1.0f);
                colors[i] = c;
            }
            return colors;
        }

        public static Color32 Tint(Color32 original, int networkId)
        {
            if (networkId < 0)
                return original;
            Color32 net = Palette[networkId % Palette.Length];
            return new Color32(
                (byte)(original.r * 0.3f + net.r * 0.7f),
                (byte)(original.g * 0.3f + net.g * 0.7f),
                (byte)(original.b * 0.3f + net.b * 0.7f),
                original.a);
        }

        public static Color32 ApplyPipeType(Color32 color, string prefabId)
        {
            if (prefabId.Contains("Radiant"))
            {
                return new Color32(
                    (byte)Mathf.Min(255, color.r * 1.35f),
                    (byte)Mathf.Min(255, color.g * 1.35f),
                    (byte)Mathf.Min(255, color.b * 1.35f),
                    color.a);
            }
            if (prefabId.Contains("Insulated"))
            {
                return new Color32(
                    (byte)(color.r * 0.45f),
                    (byte)(color.g * 0.45f),
                    (byte)(color.b * 0.45f),
                    color.a);
            }
            return color;
        }
    }

    [HarmonyPatch(typeof(OverlayModes.ConduitMode), "Update")]
    public class ConduitMode_Update_Patch
    {
        public static void Postfix(OverlayModes.ConduitMode __instance,
            HashSet<SaveLoadRoot> ___layerTargets)
        {
            bool isLiquid = __instance is OverlayModes.LiquidConduits;
            if (isLiquid && !PipeOverlayOptions.Instance.LiquidPipes)
                return;
            if (!isLiquid && !PipeOverlayOptions.Instance.GasPipes)
                return;

            var networkMgr = isLiquid
                ? (IUtilityNetworkMgr)Game.Instance.liquidConduitSystem
                : (IUtilityNetworkMgr)Game.Instance.gasConduitSystem;

            foreach (SaveLoadRoot target in ___layerTargets)
            {
                if (target == null)
                    continue;

                var bridged = target.GetComponent<IBridgedNetworkItem>();
                if (bridged == null)
                    continue;

                int cell = bridged.GetNetworkCell();
                UtilityNetwork network = networkMgr.GetNetworkForCell(cell);
                if (network == null)
                    continue;

                var kbac = target.GetComponent<KBatchedAnimController>();
                if (kbac == null)
                    continue;

                kbac.TintColour = NetworkColors.Tint(kbac.TintColour, network.id);

                var building = target.GetComponent<Building>();
                if (building != null)
                    kbac.TintColour = NetworkColors.ApplyPipeType(kbac.TintColour, building.Def.PrefabID);
            }
        }
    }

    [HarmonyPatch(typeof(ConduitFlowVisualizer), "GetCellTintColour")]
    public class ConduitFlowVisualizer_GetCellTintColour_Patch
    {
        public static void Postfix(ref Color32 __result, int cell,
            ConduitFlow ___flowManager, bool ___showContents)
        {
            if (!___showContents)
                return;

            bool isLiquid = ___flowManager == Game.Instance.liquidConduitFlow;
            if (isLiquid && !PipeOverlayOptions.Instance.LiquidPipes)
                return;
            if (!isLiquid && !PipeOverlayOptions.Instance.GasPipes)
                return;

            IUtilityNetworkMgr networkMgr = isLiquid
                ? (IUtilityNetworkMgr)Game.Instance.liquidConduitSystem
                : (IUtilityNetworkMgr)Game.Instance.gasConduitSystem;

            UtilityNetwork network = networkMgr.GetNetworkForCell(cell);
            if (network == null)
                return;

            __result = NetworkColors.Tint(__result, network.id);

            int layer = isLiquid ? (int)ObjectLayer.LiquidConduit : (int)ObjectLayer.GasConduit;
            GameObject conduitGo = Grid.Objects[cell, layer];
            if (conduitGo != null)
            {
                var building = conduitGo.GetComponent<Building>();
                if (building != null)
                    __result = NetworkColors.ApplyPipeType(__result, building.Def.PrefabID);
            }
        }
    }

    [HarmonyPatch(typeof(OverlayModes.SolidConveyor), "Update")]
    public class SolidConveyor_Update_Patch
    {
        public static void Postfix(HashSet<SaveLoadRoot> ___layerTargets)
        {
            if (!PipeOverlayOptions.Instance.ConveyorRails)
                return;

            var networkMgr = (IUtilityNetworkMgr)Game.Instance.solidConduitSystem;

            foreach (SaveLoadRoot target in ___layerTargets)
            {
                if (target == null)
                    continue;

                int cell;
                var bridged = target.GetComponent<IBridgedNetworkItem>();
                if (bridged != null)
                {
                    cell = bridged.GetNetworkCell();
                }
                else
                {
                    var conduit = target.GetComponent<SolidConduit>();
                    if (conduit == null)
                        continue;
                    cell = Grid.PosToCell(conduit);
                }

                UtilityNetwork network = networkMgr.GetNetworkForCell(cell);
                if (network == null)
                    continue;

                var kbac = target.GetComponent<KBatchedAnimController>();
                if (kbac == null)
                    continue;

                kbac.TintColour = NetworkColors.Tint(kbac.TintColour, network.id);
            }
        }
    }

    [HarmonyPatch(typeof(OverlayModes.Power), "Update")]
    public class Power_Update_Patch
    {
        public static void Postfix(HashSet<SaveLoadRoot> ___layerTargets)
        {
            if (!PipeOverlayOptions.Instance.ElectricalWires)
                return;

            var networkMgr = (IUtilityNetworkMgr)Game.Instance.electricalConduitSystem;

            foreach (SaveLoadRoot target in ___layerTargets)
            {
                if (target == null)
                    continue;

                int cell;
                var bridged = target.GetComponent<IBridgedNetworkItem>();
                if (bridged != null)
                {
                    cell = bridged.GetNetworkCell();
                }
                else
                {
                    var building = target.GetComponent<Building>();
                    if (building == null)
                        continue;

                    if (building.Def.RequiresPowerInput)
                        cell = Grid.OffsetCell(Grid.PosToCell(building), building.Def.PowerInputOffset);
                    else if (building.Def.RequiresPowerOutput)
                        cell = Grid.OffsetCell(Grid.PosToCell(building), building.Def.PowerOutputOffset);
                    else
                        continue;
                }

                UtilityNetwork network = networkMgr.GetNetworkForCell(cell);
                if (network == null)
                    continue;

                var kbac = target.GetComponent<KBatchedAnimController>();
                if (kbac == null)
                    continue;

                kbac.TintColour = NetworkColors.Tint(kbac.TintColour, network.id);
            }
        }
    }
}
