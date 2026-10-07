using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace ReverseCondPanel
{
    public class ReverseCondPanelMod : KMod.UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
        }
    }

    [HarmonyPatch(typeof(ContactConductivePipeBridgeConfig), nameof(ContactConductivePipeBridgeConfig.DoPostConfigureComplete))]
    public class AddRotateComponent_Patch
    {
        public static void Postfix(GameObject go)
        {
            go.AddOrGet<InPlaceRotatable>();
        }
    }

    public class InPlaceRotatable : KMonoBehaviour
    {
        private static readonly EventSystem.IntraObjectHandler<InPlaceRotatable> OnRefreshUserMenuDelegate =
            new EventSystem.IntraObjectHandler<InPlaceRotatable>(
                (component, data) => component.OnRefreshUserMenu());

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Subscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenuDelegate);
        }

        protected override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.RefreshUserMenu, OnRefreshUserMenuDelegate);
            base.OnCleanUp();
        }

        private void OnRefreshUserMenu()
        {
            var button = new KIconButtonMenu.ButtonInfo(
                "action_mirror",
                "Rotate",
                OnRotate,
                tooltipText: "Rotate this building in place");
            Game.Instance.userMenu.AddButton(gameObject, button);
        }

        private void OnRotate()
        {
            var building = GetComponent<Building>();
            var rotatable = GetComponent<Rotatable>();
            var endpoints = GetComponent<BuildingConduitEndpoints>();
            if (building == null || rotatable == null)
                return;

            var def = building.Def;
            var oldOrientation = rotatable.GetOrientation();
            int cell = Grid.PosToCell(building.transform.GetPosition());

            // Remove conduit endpoint registrations
            endpoints?.RemoveEndPoint();

            // Unmark conduit port grid objects at old orientation
            UnmarkConduitPorts(def, cell, oldOrientation, gameObject);

            // Rotate 180° to swap input/output
            var newOrientation = (Orientation)(((int)oldOrientation + 2) % 4);
            rotatable.SetOrientation(newOrientation);

            // Re-mark conduit port grid objects at new orientation
            MarkConduitPorts(def, cell, newOrientation, gameObject);

            // Re-register conduit endpoints
            endpoints?.AddEndpoint();

            // Update cached cells on the state machine instance
            var smi = gameObject.GetSMI<ContactConductivePipeBridge.Instance>();
            if (smi != null)
            {
                smi.inputCell = building.GetUtilityInputCell();
                smi.outputCell = building.GetUtilityOutputCell();
            }

            // Rebuild conduit networks
            Game.Instance.liquidConduitSystem.ForceRebuildNetworks();

            // Force port visualizer icons to be recreated at new positions
            var visualizer = GetComponent<BuildingCellVisualizer>();
            if (visualizer != null)
            {
                var visTraverse = Traverse.Create(visualizer);
                var icons = visTraverse.Field("icons").GetValue() as IDictionary;
                var portsList = visTraverse.Field("ports").GetValue() as System.Collections.IList;
                if (portsList != null)
                {
                    foreach (var port in portsList)
                    {
                        var portTraverse = Traverse.Create(port);
                        var vizGo = portTraverse.Field("visualizer").GetValue<GameObject>();
                        if (vizGo != null)
                        {
                            icons?.Remove(vizGo);
                            Object.Destroy(vizGo);
                            portTraverse.Field("visualizer").SetValue(null);
                        }
                    }
                }

                // Force immediate redraw so icons appear at new positions without waiting for next tick
                visualizer.Render200ms(0f);
            }
        }

        private static void UnmarkConduitPorts(BuildingDef def, int cell, Orientation orientation, GameObject go)
        {
            if (def.InputConduitType != ConduitType.None)
            {
                var offset = Rotatable.GetRotatedCellOffset(def.UtilityInputOffset, orientation);
                int portCell = Grid.OffsetCell(cell, offset);
                var layer = Grid.GetObjectLayerForConduitType(def.InputConduitType);
                if (Grid.Objects[portCell, (int)layer] == go)
                    Grid.Objects[portCell, (int)layer] = null;
            }
            if (def.OutputConduitType != ConduitType.None)
            {
                var offset = Rotatable.GetRotatedCellOffset(def.UtilityOutputOffset, orientation);
                int portCell = Grid.OffsetCell(cell, offset);
                var layer = Grid.GetObjectLayerForConduitType(def.OutputConduitType);
                if (Grid.Objects[portCell, (int)layer] == go)
                    Grid.Objects[portCell, (int)layer] = null;
            }
        }

        private static void MarkConduitPorts(BuildingDef def, int cell, Orientation orientation, GameObject go)
        {
            if (def.InputConduitType != ConduitType.None)
            {
                var offset = Rotatable.GetRotatedCellOffset(def.UtilityInputOffset, orientation);
                int portCell = Grid.OffsetCell(cell, offset);
                var layer = Grid.GetObjectLayerForConduitType(def.InputConduitType);
                Grid.Objects[portCell, (int)layer] = go;
            }
            if (def.OutputConduitType != ConduitType.None)
            {
                var offset = Rotatable.GetRotatedCellOffset(def.UtilityOutputOffset, orientation);
                int portCell = Grid.OffsetCell(cell, offset);
                var layer = Grid.GetObjectLayerForConduitType(def.OutputConduitType);
                Grid.Objects[portCell, (int)layer] = go;
            }
        }
    }
}
