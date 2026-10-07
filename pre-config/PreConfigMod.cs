using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace PreConfig
{
    public class PreConfigMod : KMod.UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class ThresholdProxy : KMonoBehaviour, IThresholdSwitch
    {
        [Serialize] private float threshold;
        [Serialize] private bool activateAbove;

        private IThresholdSwitch template;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            var uc = GetComponent<BuildingUnderConstruction>();
            if (uc != null)
            {
                template = uc.Def.BuildingComplete.GetComponent<IThresholdSwitch>();
                if (template != null)
                {
                    threshold = template.Threshold;
                    activateAbove = template.ActivateAboveThreshold;
                }
            }
        }

        public float Threshold
        {
            get => threshold;
            set => threshold = value;
        }

        public bool ActivateAboveThreshold
        {
            get => activateAbove;
            set => activateAbove = value;
        }

        public float CurrentValue => threshold;

        public float RangeMin => template?.RangeMin ?? 0;
        public float RangeMax => template?.RangeMax ?? 100;
        public LocString Title => template?.Title ?? "";
        public LocString ThresholdValueName => template?.ThresholdValueName ?? "";
        public string AboveToolTip => template?.AboveToolTip ?? "";
        public string BelowToolTip => template?.BelowToolTip ?? "";
        public ThresholdScreenLayoutType LayoutType =>
            template?.LayoutType ?? ThresholdScreenLayoutType.SliderBar;
        public int IncrementScale => template?.IncrementScale ?? 1;
        public NonLinearSlider.Range[] GetRanges =>
            template?.GetRanges ?? new NonLinearSlider.Range[] { new NonLinearSlider.Range(100, 100) };

        public float GetRangeMinInputField() => template?.GetRangeMinInputField() ?? 0;
        public float GetRangeMaxInputField() => template?.GetRangeMaxInputField() ?? 100;
        public LocString ThresholdValueUnits() => template?.ThresholdValueUnits() ?? "";
        public string Format(float value, bool units) =>
            template?.Format(value, units) ?? value.ToString();
        public float ProcessedSliderValue(float input) =>
            template?.ProcessedSliderValue(input) ?? input;
        public float ProcessedInputValue(float input) =>
            template?.ProcessedInputValue(input) ?? input;
    }

    [HarmonyPatch(typeof(BuildingUnderConstruction), "OnSpawn")]
    public class BuildingUC_OnSpawn_Patch
    {
        public static void Postfix(BuildingUnderConstruction __instance)
        {
            if (__instance.Def.BuildingComplete.GetComponent<IThresholdSwitch>() != null)
                __instance.gameObject.AddComponent<ThresholdProxy>();
        }
    }

    [HarmonyPatch(typeof(Constructable), "FinishConstruction")]
    public class Constructable_FinishConstruction_Patch
    {
        private static float savedThreshold;
        private static bool savedAbove;
        private static bool hasSaved;

        public static void Prefix(Constructable __instance)
        {
            hasSaved = false;
            var proxy = __instance.GetComponent<ThresholdProxy>();
            if (proxy == null)
                return;

            hasSaved = true;
            savedThreshold = proxy.Threshold;
            savedAbove = proxy.ActivateAboveThreshold;
        }

        public static void Postfix(Constructable __instance)
        {
            if (!hasSaved)
                return;
            hasSaved = false;

            int cell = Grid.PosToCell(__instance.transform.GetLocalPosition());
            var def = __instance.GetComponent<Building>().Def;
            var go = Grid.Objects[cell, (int)def.ObjectLayer];
            if (go == null)
                return;

            var sw = go.GetComponent<IThresholdSwitch>();
            if (sw == null)
                return;

            sw.Threshold = savedThreshold;
            sw.ActivateAboveThreshold = savedAbove;
        }
    }
}
