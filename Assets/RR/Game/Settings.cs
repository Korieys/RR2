using UnityEngine;

namespace RR.Game
{
    // Player preferences (persisted with PlayerPrefs; reads and writes are guarded).
    public static class Settings
    {
        public static bool ReducedMotion;
        public static float UiScale = 1f;
        public static float Master = 0.8f, Music = 0.6f, Effects = 0.8f;
        public static bool Muted;
        public static bool Overlays = true;

        public static void Load()
        {
            try
            {
                ReducedMotion = PlayerPrefs.GetInt("rr.reducedMotion", 0) == 1;
                UiScale = PlayerPrefs.GetFloat("rr.uiScale", 1f);
                Master = PlayerPrefs.GetFloat("rr.master", 0.8f);
                Music = PlayerPrefs.GetFloat("rr.music", 0.6f);
                Effects = PlayerPrefs.GetFloat("rr.effects", 0.8f);
                Muted = PlayerPrefs.GetInt("rr.muted", 0) == 1;
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                PlayerPrefs.SetInt("rr.reducedMotion", ReducedMotion ? 1 : 0);
                PlayerPrefs.SetFloat("rr.uiScale", UiScale);
                PlayerPrefs.SetFloat("rr.master", Master);
                PlayerPrefs.SetFloat("rr.music", Music);
                PlayerPrefs.SetFloat("rr.effects", Effects);
                PlayerPrefs.SetInt("rr.muted", Muted ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch { }
        }
    }
}