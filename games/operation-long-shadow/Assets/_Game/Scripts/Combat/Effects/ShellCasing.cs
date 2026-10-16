using LongShadow.Core;
using UnityEngine;

namespace LongShadow.Combat.Effects
{
    /// <summary>
    /// One pooled ejected casing (CINEMATIC_DIRECTION §4: "shell ejection with physics and per-surface clinks"). Plays the
    /// brass clink once on its first solid contact; <see cref="ShellEjector"/> recycles it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShellCasing : MonoBehaviour
    {
        private ServiceRef<IAudioService> _audio;
        private bool _clinked;

        /// <summary>Rigidbody of the casing.</summary>
        public Rigidbody Body { get; private set; }
        /// <summary>Mesh filter (calibre).</summary>
        public MeshFilter Filter { get; private set; }
        /// <summary>Renderer.</summary>
        public MeshRenderer Renderer { get; private set; }
        /// <summary>Seconds since ejection.</summary>
        public float Age { get; set; }

        /// <summary>Caches components (called by the pool).</summary>
        public void Init()
        {
            Body = GetComponent<Rigidbody>();
            Filter = GetComponent<MeshFilter>();
            Renderer = GetComponent<MeshRenderer>();
        }

        /// <summary>Arms the clink for a new ejection.</summary>
        public void Arm()
        {
            _clinked = false;
            Age = 0f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_clinked) return;
            _clinked = true;
            var audio = _audio.Value;
            if (audio == null) return;
            var speed = collision.relativeVelocity.magnitude;
            if (speed < 0.4f) return;
            audio.Play(CombatCues.Shells, transform.position, Mathf.Clamp01(speed / 4f) * 0.6f);
        }
    }
}
