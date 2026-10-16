using LongShadow.Combat.Data;
using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Interactables
{
    /// <summary>
    /// Resupply crate (GDD §13.2): <c>HOLD [E] Resupply</c> for 1.0 s refills magazines, reserves and frags, unlimited
    /// uses. Spawned by the MissionDirector (WP07) at the <c>CRATE.*</c> markers through
    /// <see cref="ICombatService.SpawnAmmoCrate"/>; the prefab is <c>LIB_CombatFx/Prop.AmmoCrate</c> (or a placeholder
    /// crate). The use collider is on layer Interactable, a separate body collider on Default keeps the player out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmmoCrate : MonoBehaviour, IInteractable
    {
        /// <summary>Prompt (GDD §13.2).</summary>
        public const string Prompt = "HOLD [E] Resupply";

        [SerializeField] private Transform _anchor;

        private ServiceRef<IWeaponService> _weapons;
        private ServiceRef<IAudioService> _audio;

        /// <inheritdoc/>
        public string PromptText => Prompt;
        /// <inheritdoc/>
        public bool Hold => true;
        /// <inheritdoc/>
        public float HoldSeconds => WeaponTable.ResupplyHoldSeconds; // GDD §13.2: 1.0 s
        /// <inheritdoc/>
        public Transform Anchor => _anchor != null ? _anchor : transform;

        /// <inheritdoc/>
        public bool CanInteract(IPlayerRig player)
        {
            var w = _weapons.Value;
            return w != null && !w.IsDoorGunMounted;
        }

        /// <inheritdoc/>
        public void Interact(IPlayerRig player)
        {
            var w = _weapons.Value;
            if (w == null) return;
            w.Resupply();
            _audio.Value?.Play(CombatCues.Pickup, Anchor.position);
        }

        /// <summary>Creates a crate at a pose (library prefab or placeholder), with its colliders on the right layers.</summary>
        public static GameObject Spawn(Vector3 position, Quaternion rotation)
        {
            GameObject go = null;
            if (Libraries.TryGet(out CombatFxLibrary lib) && lib.TryGet(FxIds.AmmoCrate, out var e) && e.Prefab != null)
                go = Instantiate(e.Prefab, position, rotation);
            if (go == null)
            {
                LSLog.MissingAsset(LogCategory.Combat, "LIB_CombatFx/" + FxIds.AmmoCrate, CombatFxLibrary.AssetPath);
                go = BuildPlaceholder();
                go.transform.SetPositionAndRotation(position, rotation);
            }
            go.name = "AmmoCrate";
            if (go.GetComponent<AmmoCrate>() == null) go.AddComponent<AmmoCrate>();
            EnsureColliders(go);
            return go;
        }

        /// <summary>A 0.9 × 0.45 × 0.5 m olive ammunition crate made of primitives.</summary>
        public static GameObject BuildPlaceholder()
        {
            var root = new GameObject("PH_AmmoCrate");
            var body = Placeholders.Create("AmmoCrate_Body", PrimitiveType.Cube, new Vector3(0.9f, 0.42f, 0.5f), new Color(0.27f, 0.29f, 0.18f), root.transform);
            body.transform.localPosition = new Vector3(0f, 0.21f, 0f);
            var lid = Placeholders.Create("AmmoCrate_Lid", PrimitiveType.Cube, new Vector3(0.92f, 0.04f, 0.52f), new Color(0.24f, 0.26f, 0.16f), root.transform);
            lid.transform.localPosition = new Vector3(0f, 0.44f, 0f);
            return root;
        }

        /// <summary>Adds the Interactable use box and the Default body box when the prefab has none.</summary>
        public static void EnsureColliders(GameObject go)
        {
            var t = go.transform;
            var use = t.Find("Use");
            if (use == null)
            {
                use = new GameObject("Use").transform;
                use.SetParent(t, false);
                var c = use.gameObject.AddComponent<BoxCollider>();
                c.center = new Vector3(0f, 0.3f, 0f);
                c.size = new Vector3(1.1f, 0.7f, 0.75f);
            }
            use.gameObject.layer = LayerIds.Interactable;
            var body = t.Find("Body");
            if (body == null && go.GetComponentsInChildren<Collider>(true).Length <= 1)
            {
                body = new GameObject("Body").transform;
                body.SetParent(t, false);
                var c = body.gameObject.AddComponent<BoxCollider>();
                c.center = new Vector3(0f, 0.23f, 0f);
                c.size = new Vector3(0.9f, 0.46f, 0.5f);
                body.gameObject.layer = LayerIds.Default;
            }
        }
    }
}
