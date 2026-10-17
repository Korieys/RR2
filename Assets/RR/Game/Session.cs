using System;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // One source of truth for what the player has selected. World highlights, inspector, outliner and battle
    // preparation all read it, so they always agree.
    public sealed class SelectionState
    {
        public string SystemId { get; private set; }
        public string FleetId { get; private set; }
        public string PlanetId { get; private set; }
        public event Action Changed;

        public void SelectSystem(string id) { SystemId = id; FleetId = null; PlanetId = null; Changed?.Invoke(); }
        public void SelectFleet(GalaxyState g, string id)
        {
            FleetId = id; PlanetId = null;
            var f = id != null ? g.GetFleet(id) : null;
            if (f != null) SystemId = f.SystemId;
            Changed?.Invoke();
        }
        public void SelectPlanet(GalaxyState g, string id)
        {
            PlanetId = id; FleetId = null;
            var p = id != null ? g.GetPlanet(id) : null;
            if (p != null) SystemId = g.SystemOf(p).Id;
            Changed?.Invoke();
        }
        public void Clear() { FleetId = null; PlanetId = null; Changed?.Invoke(); }
        public void Validate(GalaxyState g)
        {
            bool dirty = false;
            if (FleetId != null && (g.GetFleet(FleetId) == null || g.GetFleet(FleetId).Ships.Count == 0)) { FleetId = null; dirty = true; }
            if (PlanetId != null && g.GetPlanet(PlanetId) == null) { PlanetId = null; dirty = true; }
            if (dirty) Changed?.Invoke();
        }
        public void Notify() { Changed?.Invoke(); }
    }

    // Provisional strategic time model (documented in Docs/DECISIONS.md): pausable real time advancing whole days.
    public sealed class StrategyClock
    {
        public bool Paused = true;
        public int Speed = 1;                    // 1, 2, 3
        public float Fraction;                   // progress through the current day (presentation only)
        public bool Blocked;                     // transitions, battle, modal decisions
        public event Action<DayReport> DayAdvanced;

        public static float DaysPerSecond(int speed) => speed == 1 ? 1f : speed == 2 ? 2.5f : 5f;

        public void Tick(GalaxyState g, float dt)
        {
            if (Paused || Blocked || g.PendingEngagement != null) return;
            Fraction += dt * DaysPerSecond(Speed);
            int guard = 0;
            while (Fraction >= 1f && guard++ < 8)
            {
                Fraction -= 1f;
                var rep = StrategyRules.AdvanceDay(g);
                DayAdvanced?.Invoke(rep);
                if (rep.NewEngagement != null || !rep.Advanced) { Paused = true; Fraction = 0; break; }
            }
        }
    }
}