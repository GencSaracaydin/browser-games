using LongShadow.Core;

namespace LongShadow.Combat.Data
{
    /// <summary>
    /// Entry ids of <c>LIB_CombatFx</c> (CombatFxLibrary): pooled tracers, muzzle flashes, impacts, decals, explosions,
    /// smoke, casings and projectiles. The CombatFxBuilder (editor) fills every id; at runtime a missing id falls back to a
    /// procedural effect built by FxFactory (logged once).
    /// </summary>
    public static class FxIds
    {
        /// <summary>Tracer streak material.</summary>
        public const string Tracer = "Tracer";
        /// <summary>Muzzle flash meshes (4 variants) + additive material.</summary>
        public const string MuzzleFlash = "Muzzle.Flash";
        /// <summary>Muzzle smoke puff particles.</summary>
        public const string MuzzleSmoke = "Muzzle.Smoke";
        /// <summary>Bullet-hole decal atlas (material + per-element UV rects named plaster_n, concrete_n, wood_n, metal_n).</summary>
        public const string DecalBulletHoles = "Decal.BulletHoles";
        /// <summary>Small blood decal atlas (walls behind a body hit).</summary>
        public const string DecalBlood = "Decal.Blood";
        /// <summary>Scorch decal atlas (explosions).</summary>
        public const string DecalScorch = "Decal.Scorch";
        /// <summary>Frag explosion.</summary>
        public const string ExplosionFrag = "Explosion.Frag";
        /// <summary>Rocket explosion.</summary>
        public const string ExplosionRocket = "Explosion.Rocket";
        /// <summary>Vehicle fireball (trucks, the technical).</summary>
        public const string ExplosionFireball = "Explosion.Fireball";
        /// <summary>Coloured smoke cloud (squad smoke grenades).</summary>
        public const string SmokeCloud = "Smoke.Cloud";
        /// <summary>Rocket smoke trail.</summary>
        public const string SmokeTrail = "Smoke.Trail";
        /// <summary>Spark burst (destructibles, racks, flood heads).</summary>
        public const string SparkBurst = "Sparks.Burst";
        /// <summary>Lingering fire after a vehicle blows up.</summary>
        public const string Fire = "Fire.Loop";
        /// <summary>5.56 casing (COMMANDO).</summary>
        public const string Casing556 = "Casing.556x45";
        /// <summary>7.62×39 casing (AK, RPK, AKMS).</summary>
        public const string Casing762x39 = "Casing.762x39";
        /// <summary>7.62×51 casing (M60D, SVD and DShK stand-in).</summary>
        public const string Casing762x51 = "Casing.762x51";
        /// <summary>9 mm casing (HUSH PUPPY).</summary>
        public const string Casing9mm = "Casing.9x19";
        /// <summary>Thrown frag (world model + rigidbody), pooled.</summary>
        public const string GrenadeProjectile = "Projectile.M67";
        /// <summary>RPG rocket (world model), pooled.</summary>
        public const string RocketProjectile = "Projectile.RPG7";
        /// <summary>Resupply crate prefab.</summary>
        public const string AmmoCrate = "Prop.AmmoCrate";

        /// <summary>Impact particle profile id for a surface.</summary>
        public static string Impact(ImpactProfile profile) => "Impact." + profile;

        /// <summary>Impact particle profile of a surface type.</summary>
        public static ImpactProfile ProfileOf(SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Stone:
                case SurfaceType.Concrete:
                case SurfaceType.Tile: return ImpactProfile.Stone;
                case SurfaceType.Plaster: return ImpactProfile.Plaster;
                case SurfaceType.Wood: return ImpactProfile.Wood;
                case SurfaceType.Metal: return ImpactProfile.Metal;
                case SurfaceType.Fabric: return ImpactProfile.Fabric;
                case SurfaceType.Glass: return ImpactProfile.Glass;
                case SurfaceType.Flesh: return ImpactProfile.Flesh;
                case SurfaceType.Fruit: return ImpactProfile.Fruit;
                default: return ImpactProfile.Dust; // Default, Sand, Gravel, SaltCrust
            }
        }

        /// <summary>Bullet-hole decal element prefix for a surface ("concrete"), or null for no hole (sand, flesh, fabric).</summary>
        public static string BulletHoleGroup(SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Stone:
                case SurfaceType.Concrete:
                case SurfaceType.Tile:
                case SurfaceType.Default: return "concrete";
                case SurfaceType.Plaster: return "plaster";
                case SurfaceType.Wood: return "wood";
                case SurfaceType.Metal: return "metal";
                default: return null;
            }
        }
    }
}
