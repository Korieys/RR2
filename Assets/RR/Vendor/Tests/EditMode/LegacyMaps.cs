using System.Collections.Generic;
using Commandfall.Core;

namespace Commandfall.Tests
{
    /// <summary>
    /// The 37-hex battlefields as they shipped up to 0.13, kept only as the baseline the expanded maps are measured
    /// against (battlefield size, dead ends). Never registered in the <see cref="MapLibrary"/>.
    /// </summary>
    public static class LegacyMaps
    {
        public static MapDefinition ShatteredCitadel() =>
            new MapDefinition("shattered_citadel", "The Shattered Citadel (0.13)", "Legacy 37-hex layout.")
                .Mirror(TerrainType.Elevated, (0, 0), (-1, 2))
                .Mirror(TerrainType.Obstacle, (-2, 0))
                .Mirror(TerrainType.Cover, (-1, 1), (2, 1))
                .Mirror(TerrainType.Hazard, (-3, 1));

        public static MapDefinition CitadelCrossroads() =>
            new MapDefinition("citadel_crossroads", "Citadel Crossroads (0.13)", "Legacy 37-hex layout.")
                .Mirror(TerrainType.Obstacle, (0, 0))
                .Mirror(TerrainType.Elevated, (-2, 1), (1, 2))
                .Mirror(TerrainType.Cover, (1, 0), (-1, 2), (-3, 2))
                .Mirror(TerrainType.Hazard, (0, 1));

        public static MapDefinition ProvingGround() =>
            new MapDefinition("proving_ground", "Proving Ground (0.13)", "Legacy 37-hex layout.")
                .Mirror(TerrainType.Cover, (-1, 1))
                .Mirror(TerrainType.Obstacle, (2, 0));

        public static IEnumerable<MapDefinition> All()
        {
            yield return ShatteredCitadel();
            yield return CitadelCrossroads();
            yield return ProvingGround();
        }

        /// <summary>The legacy board is too small for three approaches; it is measured, not held to the new standard.</summary>
        public static readonly TopologyRules Rules = new TopologyRules { RequireApproaches = false };
    }
}
