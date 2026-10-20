using System.Collections.Generic;
using UnityEngine;

namespace RR.Game
{
    // The hangar's 3D inspection view (Prompt 7): the actual hull model with its fitted (or previewed) modules, lit by the
    // same ship shader, rendered by its own camera into a texture the modal displays. Far from the scene; off when hidden.
    public sealed class HangarPreview : MonoBehaviour
    {
        public RenderTexture Texture { get; private set; }
        Camera _cam;
        GameObject _ship;
        Transform _pivot;

        public static HangarPreview Create(Transform parent)
        {
            var go = new GameObject("Hangar preview");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0, -20000, 0);
            var h = go.AddComponent<HangarPreview>();
            h.Texture = new RenderTexture(1040, 640, 24, RenderTextureFormat.ARGB32) { name = "Hangar preview", antiAliasing = 4 };
            h._pivot = new GameObject("Pivot").transform;
            h._pivot.SetParent(go.transform, false);
            var cam = new GameObject("Hangar camera").AddComponent<Camera>();
            cam.transform.SetParent(go.transform, false);
            cam.transform.localPosition = new Vector3(0, 1.6f, -4.2f);
            cam.transform.localRotation = Quaternion.Euler(20, 0, 0);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.047f, 0.129f, 0.208f, 1);   // Command Glass --navy-raised behind the hull
            cam.fieldOfView = 34; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60;
            cam.targetTexture = h.Texture;
            cam.enabled = false;
            h._cam = cam;
            return h;
        }

        public void Show(string defId, IEnumerable<string> modules)
        {
            if (_ship != null) Destroy(_ship);
            var prefab = ArtLibrary.I.Model(defId);
            if (prefab == null) { _cam.enabled = false; return; }
            _ship = Instantiate(prefab, _pivot);
            SystemPresentation.ApplyShipMaterial(_ship, defId);
            ShipModules.Attach(_ship, modules, defId);
            var mf = _ship.GetComponentInChildren<MeshFilter>();
            float len = mf != null && mf.sharedMesh != null ? Mathf.Max(mf.sharedMesh.bounds.size.z, mf.sharedMesh.bounds.size.x) : 1f;
            _ship.transform.localScale = Vector3.one * (2.6f / Mathf.Max(0.3f, len));
            _ship.transform.localPosition = Vector3.zero;
            _cam.enabled = true;
        }

        public void Hide()
        {
            _cam.enabled = false;
            if (_ship != null) Destroy(_ship);
        }

        void Update()
        {
            if (_ship != null && _cam.enabled) _pivot.localRotation = Quaternion.Euler(0, Time.unscaledTime * (Settings.ReducedMotion ? 4f : 18f), 0);
        }

        void OnDestroy() { if (Texture != null) Texture.Release(); }
    }
}
