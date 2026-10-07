using HarmonyLib;

namespace BuildingTrackerMod
{
    [HarmonyPatch]
    public static class ElementConverterPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ElementConverter), "ConvertMass");
        }

        public static void Postfix(ElementConverter __instance)
        {
            if (Tracker.Instance == null) return;

            foreach (var e in __instance.consumedElements)
            {
                if (e.IsActive)
                    Tracker.Instance.Record(__instance, e.Tag.ToString(), e.Rate, "consumed", "element");
            }

            foreach (var e in __instance.outputElements)
            {
                if (e.IsActive)
                {
                    string resource = ElementLoader.FindElementByHash(e.elementHash)?.tag.ToString()
                                      ?? e.elementHash.ToString();
                    Tracker.Instance.Record(__instance, resource, e.Rate, "produced", "element");
                }
            }
        }
    }

    [HarmonyPatch]
    public static class ConduitConsumerPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ConduitConsumer), "ConduitUpdate");
        }

        public static void Postfix(ConduitConsumer __instance, float dt)
        {
            if (Tracker.Instance == null) return;

            if (!__instance.consumedLastTick || __instance.lastConsumedElement == SimHashes.Vacuum)
                return;

            float mass = __instance.consumptionRate * dt;
            string resource = ElementLoader.FindElementByHash(__instance.lastConsumedElement)?.tag.ToString()
                              ?? __instance.lastConsumedElement.ToString();
            Tracker.Instance.Record(__instance, resource, mass, "consumed", "conduit_in");
        }
    }

    [HarmonyPatch(typeof(EnergyConsumer), nameof(EnergyConsumer.EnergySim200ms))]
    public static class EnergyConsumerPatch
    {
        public static void Postfix(EnergyConsumer __instance, float dt)
        {
            if (Tracker.Instance == null) return;

            float joules = __instance.WattsUsed * dt;
            Tracker.Instance.Record(__instance, "Power", joules, "consumed", "power_consumed");
        }
    }

    [HarmonyPatch(typeof(Generator), nameof(Generator.GenerateJoules))]
    public static class GeneratorPatch
    {
        public static void Postfix(Generator __instance, float joulesAvailable)
        {
            if (Tracker.Instance == null) return;

            Tracker.Instance.Record(__instance, "Power", joulesAvailable, "produced", "power_generated");
        }
    }
}
