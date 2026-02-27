using System.Collections.Generic;
using UnityEngine;

namespace CritterDispatchMod
{
    public class CritterDispatch : KMonoBehaviour
    {
        private static readonly Color TINT = new Color(1f, 0.4f, 0.4f, 1f);
        private const float REARM_DELAY = 5f;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            KBatchedAnimController anim = GetComponent<KBatchedAnimController>();
            if (anim != null)
            {
                anim.TintColour = TINT;
            }
            Subscribe((int)GameHashes.TrapCaptureCompleted, OnTrapCaptureCompleted);
        }

        protected override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.TrapCaptureCompleted, OnTrapCaptureCompleted);
            base.OnCleanUp();
        }

        private void OnTrapCaptureCompleted(object data)
        {
            Storage storage = GetComponent<Storage>();
            if (storage == null)
                return;

            List<GameObject> critters = new List<GameObject>(storage.items);
            foreach (GameObject item in critters)
            {
                if (item == null)
                    continue;

                Butcherable butcherable = item.GetComponent<Butcherable>();
                if (butcherable != null)
                {
                    butcherable.CreateDrops();
                }

                storage.Drop(item);
                Util.KDestroyGameObject(item);
            }

            GameScheduler.Instance.Schedule("CritterDispatchRearm", REARM_DELAY, delegate(object obj)
            {
                if (this == null)
                    return;
                ReusableTrap.Instance smi = gameObject.GetSMI<ReusableTrap.Instance>();
                if (smi != null)
                {
                    smi.sm.IsArmed.Set(true, smi, false);
                    gameObject.AddTag(GameTags.TrapArmed);
                    smi.RefreshLogicOutput();
                }
            });
        }
    }
}
