using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace RR.Game
{
    /// <summary>
    /// Single runtime registry of shaders, models, fonts, UI settings and audio, built by the editor setup
    /// (RR > Setup Project). Referencing assets here guarantees they ship in the player build.
    /// </summary>
    [CreateAssetMenu(menuName = "RR/Art Library")]
    public sealed class ArtLibrary : ScriptableObject
    {
        public Shader planet, atmosphere, ring, starSurface, corona, ship, sky, starField, galaxyVolume, territory, lane, marker, hexGrid, fx, fade, haze;
        public VolumeProfile postFx;
        public PanelSettings panelSettings;
        public Font displayFont, bodyFont, bodySemiBold, bodyBold, condensed;
        public List<GameObject> models = new List<GameObject>();
        public List<AudioClip> audio = new List<AudioClip>();
        public List<Texture2D> icons = new List<Texture2D>();

        static ArtLibrary _instance;
        public static ArtLibrary I => _instance != null ? _instance : (_instance = Resources.Load<ArtLibrary>("RR/ArtLibrary"));

        Dictionary<string, GameObject> _modelMap;
        public GameObject Model(string id)
        {
            if (_modelMap == null)
            {
                _modelMap = new Dictionary<string, GameObject>();
                foreach (var m in models) if (m != null) _modelMap[m.name] = m;
            }
            return id != null && _modelMap.TryGetValue(id, out var go) ? go : null;
        }

        public AudioClip Clip(string name) => audio.Find(a => a != null && a.name == name);
        public Texture2D Icon(string name) => icons.Find(a => a != null && a.name == name);
    }
}
