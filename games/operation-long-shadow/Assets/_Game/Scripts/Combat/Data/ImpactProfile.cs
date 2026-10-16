namespace LongShadow.Combat.Data
{
    /// <summary>Impact particle families (TECH_PLAN §7.5 "impacts per SurfaceType"); several surfaces share one.</summary>
    public enum ImpactProfile
    {
        /// <summary>Sand, gravel, salt crust, untagged ground: a brown dust kick and grit.</summary>
        Dust = 0,
        /// <summary>Stone, concrete, tile: grey dust and chips.</summary>
        Stone = 1,
        /// <summary>Whitewashed plaster: a bright powder puff.</summary>
        Plaster = 2,
        /// <summary>Wood: splinters and brown dust.</summary>
        Wood = 3,
        /// <summary>Metal: sparks and a wisp of smoke.</summary>
        Metal = 4,
        /// <summary>Awnings, cloth: fibre puff.</summary>
        Fabric = 5,
        /// <summary>Glass: glinting shards.</summary>
        Glass = 6,
        /// <summary>Bodies: blood mist.</summary>
        Flesh = 7,
        /// <summary>Market fruit: juice spray.</summary>
        Fruit = 8,
    }
}
