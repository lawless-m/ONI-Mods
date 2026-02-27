using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace CritterDispatchMod
{
    public class CritterDispatchConfig : IBuildingConfig
    {
        public const string ID = "CritterDispatch";
        public const string OUTPUT_LOGIC_PORT_ID = "CRITTERDISPATCH_HAS_PREY_STATUS_PORT";

        private static readonly List<Storage.StoredItemModifier> StoredItemModifiers = new List<Storage.StoredItemModifier>();

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(
                ID, 2, 2,
                "critter_trap_ground_kanim",
                10, 10f,
                BUILDINGS.CONSTRUCTION_MASS_KG.TIER3,
                MATERIALS.RAW_METALS,
                1600f,
                BuildLocationRule.OnFloor,
                BUILDINGS.DECOR.PENALTY.TIER2,
                NOISE_POLLUTION.NOISY.TIER0,
                0.2f);

            buildingDef.LogicInputPorts = new List<LogicPorts.Port>
            {
                LogicPorts.Port.InputPort(
                    LogicOperationalController.PORT_ID,
                    new CellOffset(0, 0),
                    global::STRINGS.BUILDINGS.PREFABS.REUSABLETRAP.INPUT_LOGIC_PORT,
                    global::STRINGS.BUILDINGS.PREFABS.REUSABLETRAP.INPUT_LOGIC_PORT_ACTIVE,
                    global::STRINGS.BUILDINGS.PREFABS.REUSABLETRAP.INPUT_LOGIC_PORT_INACTIVE,
                    false, false)
            };
            buildingDef.LogicOutputPorts = new List<LogicPorts.Port>
            {
                LogicPorts.Port.OutputPort(
                    OUTPUT_LOGIC_PORT_ID,
                    new CellOffset(1, 0),
                    global::CritterDispatchMod.STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.LOGIC_PORT,
                    global::CritterDispatchMod.STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.LOGIC_PORT_ACTIVE,
                    global::CritterDispatchMod.STRINGS.BUILDINGS.PREFABS.CRITTERDISPATCH.LOGIC_PORT_INACTIVE,
                    false, false)
            };
            buildingDef.AudioCategory = "Metal";
            buildingDef.Floodable = false;
            buildingDef.AddSearchTerms(global::STRINGS.SEARCH_TERMS.RANCHING);
            buildingDef.AddSearchTerms(global::STRINGS.SEARCH_TERMS.CRITTER);
            return buildingDef;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<Prioritizable>();
            Prioritizable.AddRef(go);
            go.AddOrGet<ArmTrapWorkable>().overrideAnims = new KAnimFile[]
            {
                Assets.GetAnim("anim_interacts_critter_trap_ground_kanim")
            };
            go.AddOrGet<Operational>();
            Storage storage = go.AddOrGet<Storage>();
            storage.allowItemRemoval = true;
            storage.SetDefaultStoredItemModifiers(StoredItemModifiers);
            storage.sendOnStoreOnSpawn = true;
            TrapTrigger trapTrigger = go.AddOrGet<TrapTrigger>();
            trapTrigger.trappableCreatures = new Tag[]
            {
                GameTags.Creatures.Walker,
                GameTags.Creatures.Hoverer,
                GameTags.Creatures.Swimmer,
                GameTags.Creatures.Flyer
            };
            trapTrigger.trappedOffset = new Vector2(0.5f, 0f);
            ReusableTrap.Def def = go.AddOrGetDef<ReusableTrap.Def>();
            def.OUTPUT_LOGIC_PORT_ID = OUTPUT_LOGIC_PORT_ID;
            def.lures = new Tag[] { GameTags.Creatures.FlyersLure, GameTags.Creatures.FishTrapLure };
            go.AddOrGet<LogicPorts>();
            go.AddOrGet<LogicOperationalController>();
            go.AddOrGet<CritterDispatch>();
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
        }
    }
}
