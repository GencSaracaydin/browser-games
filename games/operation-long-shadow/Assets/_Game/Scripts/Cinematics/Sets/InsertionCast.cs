using System.Collections.Generic;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.AI;

namespace LongShadow.Cinematics.Sets
{
    /// <summary>
    /// Cinematic-only cast of the INSERTION cold open (CINEMATIC_DIRECTION §2): Magpie's exterior (VEH_HELICOPTER with
    /// spinning rotors) and its own instances of Hollis, Sparrow, Tinker and third-person Calder (WP05 bodies, "Cast
    /// means the squad, using their Beat 1 prefabs"), seated in the cabin and bound as Cast.* for the timeline. While the
    /// cast exists the gameplay squad's renderers are hidden, so the hero reveal on the crest never shows two Hollises;
    /// the swap back happens under the shot 9 white-out. Placeholders stand in for any body or vehicle not landed yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InsertionCast : MonoBehaviour
    {
        private readonly List<Renderer> _hidden = new List<Renderer>(64);
        private readonly List<GameObject> _spawned = new List<GameObject>(8);
        private ServiceRef<ISquadService> _squad;
        private GameObject _root;

        /// <summary>The active instance (on the cinematic systems object).</summary>
        public static InsertionCast Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>True while the cast exists.</summary>
        public bool Spawned => _root != null;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            Despawn();
            if (Instance == this) Instance = null;
        }

        /// <summary>Creates the helicopter and the cast (idempotent).</summary>
        public void Spawn()
        {
            if (_root != null) return;
            _root = new GameObject("InsertionCast");
            _root.transform.SetParent(transform, false);

            var heli = SpawnHeli(_root.transform);
            heli.transform.SetPositionAndRotation(InsertionLayout.Approach[0], Quaternion.LookRotation(InsertionLayout.Approach[1] - InsertionLayout.Approach[0]));
            Bind(heli, BindingIds.CastHeli);

            var cabin = new GameObject("Cabin").transform;
            cabin.SetParent(heli.transform, false);
            cabin.localPosition = InsertionLayout.CabinFloor;

            Seat(SpawnBody(CinematicAssetIds.PrefabHollis, null, "Hollis", Placeholders.Friendly), cabin,
                InsertionLayout.SeatHollis, InsertionLayout.SeatHollisYaw, BindingIds.CastHollis);
            Seat(SpawnBody(CinematicAssetIds.PrefabSparrow, null, "Sparrow", Placeholders.Friendly), cabin,
                InsertionLayout.SeatSparrow, InsertionLayout.SeatSparrowYaw, BindingIds.CastSparrow);
            Seat(SpawnBody(CinematicAssetIds.PrefabTinker, null, "Tinker", Placeholders.Friendly), cabin,
                InsertionLayout.SeatTinker, InsertionLayout.SeatTinkerYaw, BindingIds.CastTinker);
            Seat(SpawnBody(CinematicAssetIds.PrefabCalder, CinematicAssetIds.PrefabCalderShadow, "Calder", Placeholders.Greybox), cabin,
                InsertionLayout.SeatCalder, InsertionLayout.SeatCalderYaw, BindingIds.CastCalder);

            HideSquad(true);
        }

        /// <summary>Removes the cast and shows the gameplay squad again.</summary>
        public void Despawn()
        {
            HideSquad(false);
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
            if (_root != null) Destroy(_root);
            _root = null;
        }

        private GameObject SpawnHeli(Transform parent)
        {
            var prefab = CinematicLibrary.Lookup<GameObject>(CinematicAssetIds.PrefabHelicopter);
            GameObject heli;
            if (prefab != null)
            {
                heli = Instantiate(prefab, parent);
                heli.name = "Cast_Magpie";
            }
            else
            {
                heli = Placeholders.ForMissingAsset(CinematicAssetIds.PrefabHelicopter, LogCategory.Cinematics, PrimitiveType.Cube,
                    new Vector3(2.6f, 2.3f, 11.5f), parent, "Assets/_Game/Art/Models/Vehicles/helicopter/");
                heli.name = "Cast_Magpie_PH";
                var disc = Placeholders.Create("MainRotorBlur", PrimitiveType.Cylinder, new Vector3(14.6f, 0.02f, 14.6f), Color.black, heli.transform);
                disc.transform.localPosition = new Vector3(0f, 0.75f, 0.1f);
                var r = disc.GetComponent<Renderer>();
                if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
            var rotors = heli.GetComponent<HeliRotors>();
            if (rotors == null) rotors = heli.AddComponent<HeliRotors>();
            var main = HeliRotors.FindChild(heli.transform, "MainRotor");
            var tail = HeliRotors.FindChild(heli.transform, "TailRotor");
            if (main == null) main = AddRotor(heli.transform, CinematicAssetIds.ModelHeliMainRotor, InsertionLayout.MainRotorHub);
            if (tail == null) tail = AddRotor(heli.transform, CinematicAssetIds.ModelHeliTailRotor, InsertionLayout.TailRotorHub);
            rotors.Setup(main, tail);
            SetLayer(heli, LayerIds.Vehicle);
            // WP10_LOOK: downwash dust and the shadows-only blades (cabin strobe) under the main rotor hub.
            AttachFx(heli.transform, main, CinematicAssetIds.PrefabRotorDownwash);
            AttachFx(heli.transform, main, CinematicAssetIds.PrefabRotorShadow);
            _spawned.Add(heli);
            return heli;
        }

        private static void AttachFx(Transform heli, Transform mainRotor, string id)
        {
            var prefab = CinematicLibrary.Lookup<GameObject>(id);
            if (prefab == null) return;
            var fx = Instantiate(prefab, heli);
            fx.name = id;
            fx.transform.localPosition = mainRotor != null ? heli.InverseTransformPoint(mainRotor.position) : InsertionLayout.MainRotorHub;
            fx.transform.localRotation = Quaternion.identity;
        }

        private static Transform AddRotor(Transform heli, string id, Vector3 hub)
        {
            var prefab = CinematicLibrary.Lookup<GameObject>(id);
            if (prefab == null) return null;
            var r = Instantiate(prefab, heli);
            r.name = id;
            r.transform.localPosition = hub;
            r.transform.localRotation = Quaternion.identity;
            return r.transform;
        }

        private GameObject SpawnBody(string prefabId, string fallbackId, string label, Color tint)
        {
            var prefab = CinematicLibrary.Lookup<GameObject>(prefabId);
            var shadowVariant = false;
            if (prefab == null && fallbackId != null)
            {
                prefab = CinematicLibrary.Lookup<GameObject>(fallbackId);
                shadowVariant = prefab != null;
            }
            GameObject body;
            if (prefab != null)
            {
                body = Instantiate(prefab);
                body.name = "Cast_" + label;
                // Cinematic bodies ride the helicopter and run on rails: no NavMesh snapping.
                foreach (var agent in body.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
                if (shadowVariant) body.SendMessage("SetRenderVisible", true, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                LSLog.MissingAsset(LogCategory.Cinematics, prefabId, "Assets/_Game/Prefabs/Bodies/" + prefabId + ".prefab");
                body = Placeholders.Humanoid("Cast_" + label, tint);
            }
            _spawned.Add(body);
            return body;
        }

        private static void Seat(GameObject body, Transform cabin, Vector3 seat, float yaw, string bindingId)
        {
            body.transform.SetParent(cabin, false);
            body.transform.localPosition = seat;
            body.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Bind(body, bindingId);
            var cb = body.GetComponent<ICharacterBody>();
            cb?.PlayAction(ActionIds.SitHeli);
        }

        private static void Bind(GameObject go, string id)
        {
            var b = go.GetComponent<CinematicBinding>();
            if (b == null) b = go.AddComponent<CinematicBinding>();
            b.Setup(id);
        }

        private void HideSquad(bool hide)
        {
            if (!hide)
            {
                foreach (var r in _hidden)
                    if (r != null) r.enabled = true;
                _hidden.Clear();
                return;
            }
            var squad = _squad.Value;
            if (squad == null) return;
            Collect(squad.Member(SquadIds.Hollis));
            Collect(squad.Member(SquadIds.Sparrow));
            Collect(squad.Member(SquadIds.Tinker));
        }

        private void Collect(Transform member)
        {
            if (member == null) return;
            foreach (var r in member.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                r.enabled = false;
                _hidden.Add(r);
            }
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }
    }
}
