using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DupeTherapist
{
    public class PortraitRenderer : MonoBehaviour
    {
        public static PortraitRenderer Instance { get; private set; }

        private readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();
        private Camera portraitCamera;
        private RenderTexture renderTexture;
        private const int Size = 128;
        private bool rendering;

        private void Awake()
        {
            Instance = this;
            SetupCamera();
        }

        private void SetupCamera()
        {
            var camGO = new GameObject("PortraitCamera");
            camGO.transform.SetParent(transform);
            camGO.transform.localPosition = new Vector3(0, 0, -10);

            portraitCamera = camGO.AddComponent<Camera>();
            portraitCamera.enabled = false;
            portraitCamera.orthographic = true;
            portraitCamera.orthographicSize = 0.5f;
            portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            portraitCamera.backgroundColor = new Color(0, 0, 0, 0);
            portraitCamera.nearClipPlane = 0.1f;
            portraitCamera.farClipPlane = 20f;

            renderTexture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            portraitCamera.targetTexture = renderTexture;
        }

        public byte[] GetPortrait(string dupeName)
        {
            if (cache.TryGetValue(dupeName, out var png))
                return png;
            return null;
        }

        public void InvalidateCache()
        {
            cache.Clear();
        }

        public void RenderAllPortraits()
        {
            if (rendering) return;
            StartCoroutine(RenderAllCoroutine());
        }

        private IEnumerator RenderAllCoroutine()
        {
            rendering = true;
            var dupes = Components.LiveMinionIdentities.Items;
            if (dupes == null) { rendering = false; yield break; }

            foreach (var identity in dupes)
            {
                var name = identity.GetProperName();
                if (cache.ContainsKey(name)) continue;

                try
                {
                    var png = RenderPortrait(identity);
                    if (png != null)
                        cache[name] = png;
                }
                catch (System.Exception ex)
                {
                    Debug.Log($"DupeTherapist: Portrait failed for {name}: {ex.Message}");
                }

                yield return null; // one per frame
            }
            rendering = false;
            Debug.Log($"DupeTherapist: Rendered {cache.Count} portraits");
        }

        private byte[] RenderPortrait(MinionIdentity identity)
        {
            var go = identity.gameObject;

            // Create temporary portrait controller
            var portraitGO = new GameObject("TempPortrait");
            portraitGO.transform.position = portraitCamera.transform.position + Vector3.forward * 5;

            var controller = portraitGO.AddComponent<KBatchedAnimController>();
            controller.materialType = KAnimBatchGroup.MaterialType.UI;
            controller.animScale = 0.25f;
            controller.isMovable = false;

            try
            {
                // Use the game's built-in portrait setup
                CrewPortrait.SetPortraitData(identity, controller, true);
                controller.Play("ui_idle", KAnim.PlayMode.Loop, 1f, 0f);
                controller.SetDirty();
                controller.UpdateAnim(0);

                // Wait a frame for the batch renderer
                // Render
                portraitCamera.Render();

                // Capture
                RenderTexture.active = renderTexture;
                var tex = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                var png = ImageConversion.EncodeToPNG(tex);
                Destroy(tex);

                return png;
            }
            finally
            {
                Destroy(portraitGO);
            }
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
                renderTexture.Release();
        }
    }
}
