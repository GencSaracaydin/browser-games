using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Player.Vitals
{
    /// <summary>
    /// Difficulty scaling of damage dealt to the player (GDD §16), split so it is applied exactly once:
    /// <list type="bullet">
    /// <item><b>Hostile bullets arrive final.</b> WP04 sends AI shot damage already scaled
    /// (<c>ArchetypeTable.DamageToPlayer</c>: archetype value × <see cref="DifficultyProfile.EnemyDamageMultiplier"/>,
    /// the marksman's absolute 45 / 70 / 90 / 100 row) and WP03's <c>FireAI</c> passes it through, so the player does not
    /// scale <see cref="DamageType.Bullet"/> again.</item>
    /// <item><b>Other hostile damage is scaled here</b> (grenades, RPG rockets, satchels, fire, melee): WP03 resolves them
    /// with the GDD §13/§14 Regular values, so the player multiplies them by the enemy damage multiplier (0.5 / 1.0 /
    /// 1.5 / 2.2).</item>
    /// <item>Falls, scripted damage and friendly or self damage (the player's own grenade) are never scaled.</item>
    /// </list>
    /// Logged in Docs/DECISIONS.md (WP02) and Docs/CORE_CHANGES.md (DamageInfo remark clarification).
    /// </summary>
    public static class PlayerDamageMath
    {
        /// <summary>GDD §14.1: Greycoat Marksman damage per hit on Regular.</summary>
        public const float MarksmanRegularDamage = 70f;

        /// <summary>Damage after the player's share of the GDD §16 difficulty modifiers.</summary>
        public static float ScaleIncoming(in DamageInfo d, in DifficultyProfile p)
        {
            if (d.Amount <= 0f) return 0f;
            if (d.Type == DamageType.Fall || d.Type == DamageType.Script) return d.Amount;
            if (!Teams.IsEnemySide(d.InstigatorTeam)) return d.Amount;
            if (d.Type == DamageType.Bullet) return d.Amount; // already scaled by the AI (WP04)
            return d.Amount * EnemyMultiplier(p);
        }

        /// <summary>
        /// What WP04 sends for an AI bullet that hits the player (mirror of <c>ArchetypeTable.DamageToPlayer</c>, used by
        /// tests and the dev course): the marksman's SVD uses the profile's absolute per-hit value, everything else is the
        /// Regular value × the enemy damage multiplier.
        /// </summary>
        public static float AiBulletDamage(float regularDamagePerHit, string weaponId, in DifficultyProfile p)
        {
            if (weaponId == WeaponIds.SVD) return p.MarksmanDamagePerHit > 0f ? p.MarksmanDamagePerHit : MarksmanRegularDamage;
            return regularDamagePerHit * EnemyMultiplier(p);
        }

        /// <summary>GDD §16 enemy damage multiplier (1 when the profile is empty).</summary>
        public static float EnemyMultiplier(in DifficultyProfile p) => p.EnemyDamageMultiplier > 0f ? p.EnemyDamageMultiplier : 1f;

        /// <summary>Fall damage as a DamageInfo (GDD §12), torso, no instigator, direction down.</summary>
        public static DamageInfo Fall(float amount, Vector3 position) =>
            new DamageInfo(amount, DamageType.Fall, HitZone.Torso, position, Vector3.down, null, Team.None, null);

        /// <summary>
        /// Number of equal AI rifle hits (Regular damage per hit) that down the player from full health with no
        /// regeneration in between, through the whole pipeline (AI scaling, player rules, last stand).
        /// </summary>
        public static int HitsToDown(float regularDamagePerHit, Difficulty level, string weaponId = WeaponIds.AK)
        {
            var profile = DifficultyProfile.For(level);
            var model = new PlayerHealthModel(profile);
            var d = new DamageInfo(AiBulletDamage(regularDamagePerHit, weaponId, profile), DamageType.Bullet, HitZone.Torso,
                                   Vector3.zero, Vector3.forward, null, Team.Legion, weaponId);
            var amount = ScaleIncoming(in d, model.Profile);
            for (var i = 1; i <= 1000; i++)
            {
                model.ApplyDamage(amount);
                if (!model.IsAlive) return i;
                model.Tick(2f); // hits 2 s apart: past every last-stand immunity, inside every regen delay
            }
            return -1;
        }
    }
}
