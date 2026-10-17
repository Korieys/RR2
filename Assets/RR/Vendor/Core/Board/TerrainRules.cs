using System;

namespace Commandfall.Core
{
    /// <summary>What a terrain type is for, as a level designer reads it.</summary>
    public enum TerrainRole : byte
    {
        /// <summary>Ordinary ground: walkable, never blocks anything.</summary>
        Ground = 0,
        /// <summary>Walkable, but screens what stands behind it (ruin walls: cover, and fog sight for the unit behind).</summary>
        SightScreen = 1,
        /// <summary>Walkable high ground (a climb for ground units).</summary>
        HighGround = 2,
        /// <summary>A deliberately placed blocking structure: impassable and opaque.</summary>
        TacticalObstacle = 3,
        /// <summary>Walkable, but ending a move there hurts.</summary>
        Hazard = 4,
    }

    /// <summary>The gameplay properties of one terrain type. Visible geometry never implies any of these: decorative
    /// scenery (<see cref="Scenery"/>) is drawn on top of a hex without changing its terrain.</summary>
    public readonly struct TerrainProperties
    {
        public readonly TerrainType Type;
        public readonly TerrainRole Role;
        /// <summary>No unit may enter or cross the hex (Hover included).</summary>
        public readonly bool BlocksMovement;
        /// <summary>Lines of fire and sight that cross the hex are blocked.</summary>
        public readonly bool BlocksSight;
        /// <summary>Movement points needed to enter from lower ground (hover units always pay 1).</summary>
        public readonly int ClimbCost;
        /// <summary>A unit standing here is in cover against ranged attacks from distance 2+.</summary>
        public readonly bool GrantsCover;
        /// <summary>Ending a move here deals damage (hover units are immune).</summary>
        public readonly bool Damaging;
        /// <summary>Units may be placed here during deployment.</summary>
        public readonly bool Deployable;

        public TerrainProperties(TerrainType type, TerrainRole role, bool blocksMovement, bool blocksSight, int climbCost, bool cover, bool damaging, bool deployable)
        {
            Type = type; Role = role; BlocksMovement = blocksMovement; BlocksSight = blocksSight; ClimbCost = climbCost;
            GrantsCover = cover; Damaging = damaging; Deployable = deployable;
        }
    }

    /// <summary>
    /// Single source of truth for terrain gameplay properties. The rules (movement, line of sight, deployment) and the
    /// topology validator read these instead of testing terrain types ad hoc, so "this hex looks like a rock" and
    /// "this hex blocks movement" can never drift apart.
    /// </summary>
    public static class TerrainRules
    {
        static readonly TerrainProperties[] Table =
        {
            new TerrainProperties(TerrainType.Open,     TerrainRole.Ground,           false, false, 1, false, false, true),
            new TerrainProperties(TerrainType.Cover,    TerrainRole.SightScreen,      false, false, 1, true,  false, true),
            new TerrainProperties(TerrainType.Elevated, TerrainRole.HighGround,       false, false, 2, false, false, true),
            new TerrainProperties(TerrainType.Obstacle, TerrainRole.TacticalObstacle, true,  true,  0, false, false, false),
            new TerrainProperties(TerrainType.Hazard,   TerrainRole.Hazard,           false, false, 1, false, true,  false),
        };

        public static TerrainProperties Of(TerrainType t) => Table[(int)t];
        public static bool BlocksMovement(TerrainType t) => Table[(int)t].BlocksMovement;
        public static bool BlocksSight(TerrainType t) => Table[(int)t].BlocksSight;
        public static bool Deployable(TerrainType t) => Table[(int)t].Deployable;
    }

    /// <summary>Decorative dressing authored on a hex. Never blocks movement or sight, never grants cover: it exists so a
    /// battlefield can look rich without the scenery deciding where units may go.</summary>
    public enum SceneryKind : byte
    {
        /// <summary>Scattered stones and broken masonry, ankle high.</summary>
        Rubble = 0,
        /// <summary>A few low crystal shards.</summary>
        Crystals = 1,
        /// <summary>Battle debris: plates, struts and spent casings.</summary>
        Debris = 2,
        /// <summary>The stump of a fallen column or a toppled block, knee high at most.</summary>
        Stump = 3,
    }

    [Serializable]
    public readonly struct Scenery
    {
        public readonly Hex At;
        public readonly SceneryKind Kind;
        public Scenery(Hex at, SceneryKind kind) { At = at; Kind = kind; }
        public const bool BlocksMovement = false;
        public const bool BlocksSight = false;
    }
}
