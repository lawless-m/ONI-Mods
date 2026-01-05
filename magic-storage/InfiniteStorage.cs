using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using KMod;
using KSerialization;

namespace InfiniteStorage
{
    /// <summary>
    /// Template Storage mod - Items deposited once become infinitely available
    /// </summary>
    public class InfiniteStorageMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            Debug.Log("InfiniteStorage: Initializing...");
            base.OnLoad(harmony);
            Debug.Log("InfiniteStorage: Loaded successfully!");
        }
    }

    /// <summary>
    /// Component attached to Storage to track template items
    /// </summary>
    public class TemplateStorage : KMonoBehaviour
    {
        // Dictionary: Tag -> (prefab name, temperature, disease info)
        // Not serialized - will be rebuilt from items in storage
        [NonSerialized]
        public Dictionary<Tag, TemplateItem> templates = new Dictionary<Tag, TemplateItem>();

        // Simple flag to mark this storage as infinite - this DOES get saved
        [Serialize]
        public bool isInfiniteStorage = false;

        /// <summary>
        /// Check if this storage should have infinite capacity
        /// Only applies to intentional storage buildings, not internal storage
        /// </summary>
        public static bool IsValidStorageType(Storage storage)
        {
            if (storage == null) return false;

            // Check for specific storage building types
            return storage.GetComponent<StorageLocker>() != null ||
                   storage.GetComponent<Refrigerator>() != null ||
                   storage.GetComponent<RationBox>() != null ||
                   storage.GetComponent<Reservoir>() != null;
        }

        /// <summary>
        /// Check if this is a reservoir (needs accurate mass for pipes)
        /// </summary>
        public static bool IsReservoir(Storage storage)
        {
            return storage != null && storage.GetComponent<Reservoir>() != null;
        }

        public class TemplateItem
        {
            public Tag tag;
            public string prefabName;
            public float temperature;
            public byte diseaseIdx;
            public int diseaseCount;
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            // If this was marked as infinite storage, rebuild templates from items
            if (isInfiniteStorage)
            {
                Storage storage = GetComponent<Storage>();
                if (storage != null)
                {
                    // Set infinite capacity again
                    storage.capacityKg = float.MaxValue;

                    var userControlled = storage.GetComponent<IUserControlledCapacity>();
                    if (userControlled != null)
                    {
                        var field = userControlled.GetType().GetField("userMaxCapacity",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            field.SetValue(userControlled, float.MaxValue);
                        }
                    }

                    // Rebuild templates from existing items
                    Debug.Log($"InfiniteStorage: Rebuilding templates for storage with {storage.items.Count} items");
                    foreach (GameObject item in storage.items)
                    {
                        if (item != null)
                        {
                            AddTemplate(item);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Register an item as a template
        /// </summary>
        public void AddTemplate(GameObject item)
        {
            if (item == null) return;

            PrimaryElement pe = item.GetComponent<PrimaryElement>();
            if (pe == null) return;

            Tag tag = item.PrefabID();

            if (!templates.ContainsKey(tag))
            {
                templates[tag] = new TemplateItem
                {
                    tag = tag,
                    prefabName = item.name,
                    temperature = pe.Temperature,
                    diseaseIdx = pe.DiseaseIdx,
                    diseaseCount = pe.DiseaseCount
                };

                Debug.Log($"InfiniteStorage: Added template for {tag} (total templates: {templates.Count})");
            }
        }

        /// <summary>
        /// Check if templates need rebuilding and rebuild if necessary
        /// </summary>
        public void EnsureTemplates()
        {
            if (isInfiniteStorage && templates.Count == 0)
            {
                Storage storage = GetComponent<Storage>();
                if (storage != null && storage.items.Count > 0)
                {
                    Debug.Log($"InfiniteStorage: Templates missing, rebuilding from {storage.items.Count} items");
                    foreach (GameObject item in storage.items)
                    {
                        if (item != null)
                        {
                            AddTemplate(item);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create an infinite copy of a template item
        /// </summary>
        public GameObject SpawnTemplateItem(Tag tag, float amount = 1f)
        {
            if (!templates.TryGetValue(tag, out TemplateItem template))
                return null;

            // Spawn the item
            GameObject item = Util.KInstantiate(Assets.GetPrefab(tag), transform.position);

            PrimaryElement pe = item.GetComponent<PrimaryElement>();
            if (pe != null)
            {
                pe.Temperature = template.temperature;
                pe.Mass = amount;
                if (template.diseaseIdx != byte.MaxValue && template.diseaseCount > 0)
                {
                    pe.AddDisease(template.diseaseIdx, template.diseaseCount, "InfiniteStorage");
                }
            }

            item.SetActive(true);
            return item;
        }
    }

    /// <summary>
    /// Patch Storage to track items as templates when deposited
    /// </summary>
    [HarmonyPatch(typeof(Storage))]
    [HarmonyPatch("Store")]
    [HarmonyPatch(new Type[] { typeof(GameObject), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    public class Storage_Store_Patch
    {
        public static void Postfix(Storage __instance, GameObject go, GameObject __result)
        {
            if (__result == null || go == null) return;

            // Only apply to valid storage types (not toilets, etc.)
            if (!TemplateStorage.IsValidStorageType(__instance)) return;

            // Get or add TemplateStorage component
            TemplateStorage templateStorage = __instance.GetComponent<TemplateStorage>();
            if (templateStorage == null)
            {
                templateStorage = __instance.gameObject.AddComponent<TemplateStorage>();
            }

            // Register this item as a template
            templateStorage.AddTemplate(go);

            // Mark this storage as infinite (persists across saves)
            templateStorage.isInfiniteStorage = true;

            // Set infinite capacity
            __instance.capacityKg = float.MaxValue;

            // Also set user-controlled capacity to infinite (for storage bins with sliders)
            var userControlled = __instance.GetComponent<IUserControlledCapacity>();
            if (userControlled != null)
            {
                // Use reflection to set the private userMaxCapacity field
                var field = userControlled.GetType().GetField("userMaxCapacity",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(userControlled, float.MaxValue);
                }
            }
        }
    }

    /// <summary>
    /// Patch PrimaryElement.Mass getter to return infinite mass for template items (non-food only)
    /// </summary>
    [HarmonyPatch(typeof(PrimaryElement))]
    [HarmonyPatch("Mass", MethodType.Getter)]
    public class PrimaryElement_Mass_Patch
    {
        public static void Postfix(PrimaryElement __instance, ref float __result)
        {
            if (__instance == null || __instance.gameObject == null) return;

            // Don't apply to food items - they need discrete units
            if (__instance.gameObject.GetComponent<Edible>() != null) return;

            // Check if this item is in a template storage
            var storage = __instance.gameObject.GetComponentInParent<Storage>();
            if (storage == null) return;

            // Only apply to valid storage types
            if (!TemplateStorage.IsValidStorageType(storage)) return;

            // Don't apply to reservoir contents - pipes need accurate mass readings
            if (TemplateStorage.IsReservoir(storage)) return;

            TemplateStorage templateStorage = storage.GetComponent<TemplateStorage>();
            if (templateStorage == null || !templateStorage.isInfiniteStorage) return;

            // Ensure templates are rebuilt if they were cleared
            templateStorage.EnsureTemplates();

            Tag tag = __instance.gameObject.PrefabID();
            if (templateStorage.templates.ContainsKey(tag))
            {
                // Return a very large mass so items never run out
                __result = 100000f;
            }
        }
    }

    /// <summary>
    /// Patch Storage to auto-refill food items when they run low
    /// </summary>
    [HarmonyPatch(typeof(Storage))]
    [HarmonyPatch("MassStored")]
    public class Storage_MassStored_Patch
    {
        private static Dictionary<Storage, Dictionary<Tag, float>> lastRefillTime = new Dictionary<Storage, Dictionary<Tag, float>>();

        [ThreadStatic]
        private static bool isSpawning = false;

        public static void Postfix(Storage __instance)
        {
            if (isSpawning) return;
            if (!TemplateStorage.IsValidStorageType(__instance)) return;

            // Don't auto-refill reservoirs - they work differently
            if (TemplateStorage.IsReservoir(__instance)) return;

            TemplateStorage templateStorage = __instance.GetComponent<TemplateStorage>();
            if (templateStorage == null || !templateStorage.isInfiniteStorage) return;

            // Rebuild templates from current items if empty
            if (templateStorage.templates.Count == 0 && __instance.items.Count > 0)
            {
                Debug.Log("InfiniteStorage: Templates were cleared, rebuilding from items");
                foreach (GameObject item in __instance.items)
                {
                    if (item != null)
                    {
                        templateStorage.AddTemplate(item);
                    }
                }
            }

            if (templateStorage.templates.Count == 0) return;

            // Initialize timing dictionary for this storage
            if (!lastRefillTime.ContainsKey(__instance))
            {
                lastRefillTime[__instance] = new Dictionary<Tag, float>();
            }

            float currentTime = Time.time;

            // Check each template
            foreach (var template in templateStorage.templates.Values)
            {
                // Only check food items
                GameObject firstItem = __instance.FindFirst(template.tag);
                if (firstItem == null || firstItem.GetComponent<Edible>() == null)
                    continue;

                // Don't refill more than once per 5 seconds per item type
                if (lastRefillTime[__instance].ContainsKey(template.tag))
                {
                    if (currentTime - lastRefillTime[__instance][template.tag] < 5f)
                        continue;
                }

                // Count how many of this item we have
                int count = 0;
                foreach (GameObject item in __instance.items)
                {
                    if (item != null && item.PrefabID() == template.tag)
                    {
                        count++;
                    }
                }

                // If we have less than 10 food items, spawn more
                if (count < 10)
                {
                    isSpawning = true;
                    try
                    {
                        Debug.Log($"InfiniteStorage: Refilling {template.tag} in storage (currently have {count})");
                        // Spawn enough to reach 20
                        int toSpawn = 20 - count;
                        for (int i = 0; i < toSpawn; i++)
                        {
                            GameObject spawned = templateStorage.SpawnTemplateItem(template.tag, 1f);
                            if (spawned != null)
                            {
                                __instance.Store(spawned, false, false, true, false);
                            }
                        }
                        lastRefillTime[__instance][template.tag] = currentTime;
                    }
                    finally
                    {
                        isSpawning = false;
                    }
                    break; // Only refill one type per call to avoid lag
                }
            }
        }
    }


    /// <summary>
    /// Patch Storage OnCleanUp to prevent dropping infinite items
    /// </summary>
    [HarmonyPatch(typeof(Storage))]
    [HarmonyPatch("OnCleanUp")]
    public class Storage_OnCleanUp_Patch
    {
        public static void Prefix(Storage __instance)
        {
            TemplateStorage templateStorage = __instance.GetComponent<TemplateStorage>();
            if (templateStorage != null)
            {
                // Clear templates before cleanup to prevent infinite items from being dropped
                Debug.Log("InfiniteStorage: Clearing templates before storage cleanup");
                templateStorage.templates.Clear();
            }

            // Also manually clear all items to prevent drops
            List<GameObject> items = __instance.items;
            if (items != null)
            {
                // Create a copy of the list to avoid modification during iteration
                List<GameObject> itemsCopy = new List<GameObject>(items);
                foreach (GameObject item in itemsCopy)
                {
                    if (item != null)
                    {
                        Util.KDestroyGameObject(item);
                    }
                }
                items.Clear();
            }
        }
    }


    /// <summary>
    /// Patch Storage.IsFull to never be full for template storage
    /// </summary>
    [HarmonyPatch(typeof(Storage))]
    [HarmonyPatch("IsFull")]
    public class Storage_IsFull_Patch
    {
        public static void Postfix(Storage __instance, ref bool __result)
        {
            // Only apply to valid storage types
            if (!TemplateStorage.IsValidStorageType(__instance)) return;

            TemplateStorage templateStorage = __instance.GetComponent<TemplateStorage>();
            if (templateStorage != null && templateStorage.isInfiniteStorage)
            {
                __result = false;
            }
        }
    }
}
