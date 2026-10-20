using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RR.Game
{
    // Capital-ship module assemblies (Prompt 7): separate meshes attached to a hull model, so a refit visibly changes the
    // silhouette and the configuration stays "hull + modules". Purely visual; the rules read the modules from the army entry.
    // Modules are authored for a hull of length 1 centred on its bounds; per-hull fits measured on the actual hulls
    // (ShipMounts.g.cs, from Tools/Blender/rr_mounts.py) set the lateral modules against the flanks and the battery on the spine.
    public static partial class ShipModules
    {
        /// <summary>Attaches the module meshes for <paramref name="modules"/> under <paramref name="model"/>, fitted to its hull.</summary>
        public static void Attach(GameObject model, IEnumerable<string> modules, string defId)
        {
            if (model == null) return;
            var mf = model.GetComponentInChildren<MeshFilter>();
            float len = mf != null && mf.sharedMesh != null ? Mathf.Max(mf.sharedMesh.bounds.size.z, mf.sharedMesh.bounds.size.x) : 1f;
            var host = mf != null ? mf.transform : model.transform;
            bool fitted = Mounts.TryGetValue(defId, out var fit);
            foreach (var mid in modules.Distinct())
            {
                var id = Commandfall.Core.SliceRules.Module(mid)?.Visual ?? mid;   // content names the mesh it reuses (Prompt 19)
                var prefab = ArtLibrary.I.Model("module_" + id);
                if (prefab == null) continue;
                var part = Object.Instantiate(prefab, host);
                part.name = "Module " + mid;
                var pos = mf != null ? mf.sharedMesh.bounds.center : Vector3.zero;
                var scale = Vector3.one * len;
                if (fitted)
                {
                    if (id == "afterburners") scale.x *= fit.afterburnerWidth;
                    else if (id == "ablative_plating") scale.x *= fit.platingWidth;
                    else if (id == "heavy_battery") pos.y += fit.batteryLift * len;
                }
                part.transform.localPosition = pos;
                part.transform.localRotation = Quaternion.identity;
                part.transform.localScale = scale;
                SystemPresentation.ApplyShipMaterial(part, defId);
            }
        }
    }
}
