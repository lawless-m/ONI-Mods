using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using KMod;

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
            Debug.Log("AutoTeleportMod: Initializing...");
            base.OnLoad(harmony);
            Debug.Log("AutoTeleportMod: Loaded successfully!");
        }
    }

    /// <summary>
    /// Helper class that resolves WarpPortal types and methods via reflection.
    /// WarpPortal is a Spaced Out DLC class so we use reflection for safety.
    /// </summary>
    internal static class WarpPortalReflection
    {
        private static bool resolved;

        // WarpPortal type and key members
        internal static Type warpPortalType;
        internal static FieldInfo smiField;
        internal static Type statesInstanceType;
        internal static Type statesType;

        // State fields on the States class
        internal static FieldInfo idleStateField;
        internal static FieldInfo readyStateField;

        // Method to start the delayed warp coroutine
        internal static MethodInfo delayedWarpMethod;

        // Worker field on WarpPortal (the dupe currently in the portal)
        internal static FieldInfo workerField;

        // WarpReceiver type
        internal static Type warpReceiverType;
        internal static MethodInfo receiveWarpedDuplicantMethod;

        internal static bool Resolve()
        {
            if (resolved)
                return warpPortalType != null;

            resolved = true;

            // Find WarpPortal class
            warpPortalType = Type.GetType("WarpPortal, Assembly-CSharp");
            if (warpPortalType == null)
            {
                Debug.LogWarning("AutoTeleportMod: WarpPortal type not found. Is Spaced Out! DLC installed?");
                return false;
            }

            Debug.Log($"AutoTeleportMod: Found WarpPortal type: {warpPortalType.FullName}");

            // Log all fields and methods for debugging
            LogTypeMembers(warpPortalType);

            // Find the worker field - stores the dupe currently in the portal
            // Try common field names
            workerField = FindField(warpPortalType, "worker", "assignedWorker", "currentWorker",
                "occupant", "assignee", "duplicant");

            // Find the DelayedWarp method
            delayedWarpMethod = FindMethod(warpPortalType, "DelayedWarp", "StartWarp",
                "StartWarpSequence", "BeginWarp", "TriggerWarp");

            // Find WarpReceiver
            warpReceiverType = Type.GetType("WarpReceiver, Assembly-CSharp");
            if (warpReceiverType != null)
            {
                receiveWarpedDuplicantMethod = warpReceiverType.GetMethod("ReceiveWarpedDuplicant",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Debug.Log($"AutoTeleportMod: Found WarpReceiver.ReceiveWarpedDuplicant: {receiveWarpedDuplicantMethod != null}");
            }

            return true;
        }

        private static FieldInfo FindField(Type type, params string[] names)
        {
            foreach (string name in names)
            {
                var field = type.GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    Debug.Log($"AutoTeleportMod: Found field '{name}' on {type.Name}: {field.FieldType.Name}");
                    return field;
                }
            }
            return null;
        }

        private static MethodInfo FindMethod(Type type, params string[] names)
        {
            foreach (string name in names)
            {
                var method = type.GetMethod(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    Debug.Log($"AutoTeleportMod: Found method '{name}' on {type.Name}");
                    return method;
                }
            }
            return null;
        }

        private static void LogTypeMembers(Type type)
        {
            Debug.Log($"AutoTeleportMod: === Fields on {type.Name} ===");
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Debug.Log($"AutoTeleportMod:   Field: {field.FieldType.Name} {field.Name}");
            }

            Debug.Log($"AutoTeleportMod: === Methods on {type.Name} ===");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Debug.Log($"AutoTeleportMod:   Method: {method.ReturnType.Name} {method.Name}({string.Join(", ", Array.ConvertAll(method.GetParameters(), p => p.ParameterType.Name))})");
            }
        }
    }

    /// <summary>
    /// Attach the auto-teleport monitor when a game loads
    /// </summary>
    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch("OnSpawn")]
    public class Game_OnSpawn_Patch
    {
        public static void Postfix(Game __instance)
        {
            if (!WarpPortalReflection.Resolve())
                return;

            __instance.gameObject.AddOrGet<AutoTeleportMonitor>();
            Debug.Log("AutoTeleportMod: Monitor component attached");
        }
    }

    /// <summary>
    /// Monitors all WarpPortal instances and auto-triggers teleportation
    /// when a duplicant has entered the portal and it's ready to warp.
    ///
    /// Uses ISim1000ms to check every ~1 second of game time, which is
    /// frequent enough since dupes take several seconds to walk to portals.
    /// </summary>
    public class AutoTeleportMonitor : KMonoBehaviour, ISim1000ms
    {
        // Track portals we've already triggered to avoid double-firing
        private HashSet<int> triggeredPortals = new HashSet<int>();

        public void Sim1000ms(float dt)
        {
            if (WarpPortalReflection.warpPortalType == null)
                return;

            // Find all WarpPortal instances in the current world
            var portals = UnityEngine.Object.FindObjectsOfType(WarpPortalReflection.warpPortalType);

            foreach (Component portal in portals)
            {
                if (portal == null)
                    continue;

                TryAutoTeleport(portal);
            }

            // Clean up triggered set for portals that no longer exist
            if (triggeredPortals.Count > portals.Length * 2)
            {
                triggeredPortals.Clear();
            }
        }

        private void TryAutoTeleport(Component portal)
        {
            int portalId = portal.GetInstanceID();

            // Check if this portal has a worker/dupe inside ready to warp
            // We use multiple approaches since the exact API may vary

            // Approach 1: Check if the portal is a Workable with an active worker
            var workable = portal as Workable;
            if (workable != null)
            {
                var workerField = AccessTools.Field(WarpPortalReflection.warpPortalType, "worker");
                if (workerField == null)
                    workerField = AccessTools.Field(typeof(Workable), "worker");

                if (workerField != null)
                {
                    var worker = workerField.GetValue(portal) as Worker;
                    if (worker != null && !triggeredPortals.Contains(portalId))
                    {
                        // Found a dupe inside! Check if the portal is in the right state
                        if (IsReadyToWarp(portal))
                        {
                            Debug.Log($"AutoTeleportMod: Dupe {worker.name} detected in portal, auto-triggering teleport!");
                            triggeredPortals.Add(portalId);
                            TriggerWarp(portal);
                            return;
                        }
                    }
                    else if (worker == null)
                    {
                        // No worker - clear triggered state so it can fire again next time
                        triggeredPortals.Remove(portalId);
                    }
                }
            }

            // Approach 2: Check via Assignable component
            var assignable = portal.GetComponent<Assignable>();
            if (assignable != null && assignable.assignee != null)
            {
                // There's an assignee - check if they've arrived at the portal
                // by looking for a nearby MinionIdentity
                if (!triggeredPortals.Contains(portalId) && IsReadyToWarp(portal))
                {
                    Debug.Log("AutoTeleportMod: Portal has assignee and is ready, auto-triggering teleport!");
                    triggeredPortals.Add(portalId);
                    TriggerWarp(portal);
                }
            }
            else if (assignable != null && assignable.assignee == null)
            {
                triggeredPortals.Remove(portalId);
            }
        }

        /// <summary>
        /// Check if the portal is in a state where it's ready to warp (dupe inside, waiting for button)
        /// </summary>
        private bool IsReadyToWarp(Component portal)
        {
            // Try checking the state machine instance
            var smiProp = AccessTools.Property(WarpPortalReflection.warpPortalType, "smi");
            if (smiProp != null)
            {
                var smi = smiProp.GetValue(portal);
                if (smi != null)
                {
                    // Check current state name for "ready" or "occupied" or "hasduplicant"
                    var getCurrentState = AccessTools.Method(smi.GetType(), "GetCurrentState");
                    if (getCurrentState != null)
                    {
                        var state = getCurrentState.Invoke(smi, null);
                        if (state != null)
                        {
                            string stateName = state.ToString().ToLower();
                            if (stateName.Contains("ready") || stateName.Contains("occupied") ||
                                stateName.Contains("hasduplicant") || stateName.Contains("waiting"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            // Fallback: check for a boolean field that indicates readiness
            var readyField = AccessTools.Field(WarpPortalReflection.warpPortalType, "isReadyToWarp") ??
                             AccessTools.Field(WarpPortalReflection.warpPortalType, "readyToWarp") ??
                             AccessTools.Field(WarpPortalReflection.warpPortalType, "canWarp");
            if (readyField != null && readyField.FieldType == typeof(bool))
            {
                return (bool)readyField.GetValue(portal);
            }

            // Fallback: if we found a worker in the portal, assume it's ready
            // (the worker wouldn't be assigned if the portal wasn't accepting)
            var workerField = AccessTools.Field(WarpPortalReflection.warpPortalType, "worker") ??
                              AccessTools.Field(typeof(Workable), "worker");
            if (workerField != null)
            {
                var worker = workerField.GetValue(portal);
                if (worker != null) return true;
            }

            return false;
        }

        /// <summary>
        /// Trigger the warp - tries multiple approaches to initiate the teleportation
        /// </summary>
        private void TriggerWarp(Component portal)
        {
            // Approach 1: Call the DelayedWarp coroutine directly (this is what the button does)
            if (WarpPortalReflection.delayedWarpMethod != null)
            {
                try
                {
                    var result = WarpPortalReflection.delayedWarpMethod.Invoke(portal, null);
                    if (result is IEnumerator coroutine)
                    {
                        // Start it as a Unity coroutine on the portal's MonoBehaviour
                        var mono = portal as MonoBehaviour;
                        if (mono != null)
                        {
                            mono.StartCoroutine(coroutine);
                            Debug.Log("AutoTeleportMod: Started DelayedWarp coroutine");
                            return;
                        }
                    }
                    Debug.Log("AutoTeleportMod: Called warp method directly");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"AutoTeleportMod: Failed to call warp method: {ex.Message}");
                }
            }

            // Approach 2: Try to find and call any method that triggers the warp
            foreach (string methodName in new[] { "StartWarpSequence", "StartWarp", "BeginWarp",
                "TriggerWarp", "DoWarp", "Warp", "OnWarpClicked", "OnTeleportClicked" })
            {
                var method = AccessTools.Method(WarpPortalReflection.warpPortalType, methodName);
                if (method != null)
                {
                    try
                    {
                        var result = method.Invoke(portal, null);
                        if (result is IEnumerator coroutine)
                        {
                            var mono = portal as MonoBehaviour;
                            if (mono != null)
                            {
                                mono.StartCoroutine(coroutine);
                            }
                        }
                        Debug.Log($"AutoTeleportMod: Triggered warp via {methodName}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"AutoTeleportMod: Failed to call {methodName}: {ex.Message}");
                    }
                }
            }

            // Approach 3: Trigger state machine transition
            var smiProp = AccessTools.Property(WarpPortalReflection.warpPortalType, "smi");
            if (smiProp != null)
            {
                var smi = smiProp.GetValue(portal);
                if (smi != null)
                {
                    // Look for a GoTo method that transitions to a "warping" state
                    var goTo = AccessTools.Method(smi.GetType(), "GoTo",
                        new Type[] { typeof(StateMachine.BaseState) });

                    // Find the warping state
                    var statesField = AccessTools.Field(smi.GetType(), "master") ??
                                     AccessTools.Field(smi.GetType(), "sm");
                    if (statesField != null)
                    {
                        var states = statesField.GetValue(smi);
                        if (states != null)
                        {
                            var warpingState = AccessTools.Field(states.GetType(), "warping") ??
                                               AccessTools.Field(states.GetType(), "warp") ??
                                               AccessTools.Field(states.GetType(), "teleporting");
                            if (warpingState != null && goTo != null)
                            {
                                try
                                {
                                    goTo.Invoke(smi, new[] { warpingState.GetValue(states) });
                                    Debug.Log("AutoTeleportMod: Triggered state machine transition to warping");
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"AutoTeleportMod: Failed state transition: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }

            Debug.LogWarning("AutoTeleportMod: Could not find a way to trigger warp. " +
                "Check the log for available fields/methods on WarpPortal.");
        }
    }
}
