using LongShadow.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LongShadow.Cinematics.Booth
{
    /// <summary>
    /// Root of S0_Booth (at world (−600, 0, −600), local frame GDD §8.0). Holds the booth's moving parts, registers
    /// them as cinematic bindings while the scene is loaded (<see cref="BindingIds"/>), and puts the set in its resting
    /// state (tube on, CRTs on static) so inserts and the death insert work whether or not the intro ran. Built by
    /// <c>LongShadow/Cinematics/Build Booth Scene</c>.
    /// </summary>
    [DefaultExecutionOrder(ExecOrder.Cinematics - 20)]
    [DisallowMultipleComponent]
    public sealed class BoothRoot : MonoBehaviour
    {
        [SerializeField] private BoothCameraRig _camera;
        [SerializeField] private CRTScreen[] _crts = new CRTScreen[6];
        [SerializeField] private FlickerLight _tube;
        [SerializeField] private ScramblerBox _scrambler;
        [SerializeField] private ReelDeck _reel;
        [SerializeField] private WallClock _clock;
        [SerializeField] private PenTapper _pen;
        [SerializeField] private DeskPhone _phone;
        [SerializeField] private SteelLighter _lighter;
        [SerializeField] private BoothSmoke[] _smoke = new BoothSmoke[0];
        [SerializeField] private GameObject _calder;
        [SerializeField] private GameObject _interrogator;
        [SerializeField] private BoothLook _look;
        [SerializeField] private GameObject _slate;
        [SerializeField, Tooltip("Set by the builder after a successful lighting bake; until then booth bodies ignore probes.")]
        private bool _lightingBaked;

        private Vector3 _interrogatorHome;
        private Quaternion _interrogatorHomeRot = Quaternion.identity;

        /// <summary>The loaded booth (null when S0_Booth is not loaded).</summary>
        public static BoothRoot Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        /// <summary>Cam_Booth.</summary>
        public BoothCameraRig Camera => _camera;
        /// <summary>CRT1..6 (index 0 = CRT1).</summary>
        public CRTScreen[] Crts => _crts;
        /// <summary>The fluorescent tube.</summary>
        public FlickerLight Tube => _tube;
        /// <summary>The scrambler box.</summary>
        public ScramblerBox Scrambler => _scrambler;
        /// <summary>The reel-to-reel deck.</summary>
        public ReelDeck Reel => _reel;
        /// <summary>The wall clock.</summary>
        public WallClock Clock => _clock;
        /// <summary>The pen (four-tap emitter).</summary>
        public PenTapper Pen => _pen;
        /// <summary>The desk phone.</summary>
        public DeskPhone Phone => _phone;
        /// <summary>The steel lighter.</summary>
        public SteelLighter Lighter => _lighter;
        /// <summary>Smoke sources.</summary>
        public BoothSmoke[] Smoke => _smoke;
        /// <summary>Seated Calder (PFB_Calder_Booth instance).</summary>
        public GameObject CalderBody => _calder;
        /// <summary>The interrogator (PFB_Interrogator instance).</summary>
        public GameObject InterrogatorBody => _interrogator;
        /// <summary>Booth grade driver.</summary>
        public BoothLook Look => _look;
        /// <summary>Session state switch.</summary>
        public BoothState State { get; private set; }
        /// <summary>"S-3 / SESSION 11 / 03-84" slate in front of the film camera (MID-B shot 4).</summary>
        public GameObject Slate => _slate;

        /// <summary>CRT by number 1..6 (null when missing).</summary>
        public CRTScreen Crt(int number) =>
            _crts != null && number >= 1 && number <= _crts.Length ? _crts[number - 1] : null;

        private void Awake()
        {
            State = GetComponent<BoothState>();
            if (State == null) State = gameObject.AddComponent<BoothState>();
            if (_look == null) _look = GetComponentInChildren<BoothLook>(true);
            if (!_lightingBaked) DisableProbesOnBodies();
            if (_slate != null) _slate.SetActive(false);
            if (_interrogator != null)
            {
                _interrogatorHome = _interrogator.transform.localPosition;
                _interrogatorHomeRot = _interrogator.transform.localRotation;
            }
        }

        private void OnEnable()
        {
            Current = this;
            Register(BindingIds.Booth, this);
            Register(BindingIds.BoothCamera, _camera);
            if (_crts != null)
                for (var i = 0; i < _crts.Length; i++)
                    Register(BindingIds.Crt(i + 1), _crts[i]);
            Register(BindingIds.Tube, _tube);
            Register(BindingIds.Scrambler, _scrambler);
            Register(BindingIds.Reel, _reel);
            Register(BindingIds.Clock, _clock);
            Register(BindingIds.Pen, _pen);
            Register(BindingIds.Phone, _phone);
            Register(BindingIds.Lighter, _lighter);
            Register(BindingIds.BoothCalder, _calder);
            Register(BindingIds.BoothInterrogator, _interrogator);
        }

        private void Start() => SetResting();

        private void OnDisable()
        {
            if (Current == this) Current = null;
            Unregister(BindingIds.Booth, this);
            Unregister(BindingIds.BoothCamera, _camera);
            if (_crts != null)
                for (var i = 0; i < _crts.Length; i++)
                    Unregister(BindingIds.Crt(i + 1), _crts[i]);
            Unregister(BindingIds.Tube, _tube);
            Unregister(BindingIds.Scrambler, _scrambler);
            Unregister(BindingIds.Reel, _reel);
            Unregister(BindingIds.Clock, _clock);
            Unregister(BindingIds.Pen, _pen);
            Unregister(BindingIds.Phone, _phone);
            Unregister(BindingIds.Lighter, _lighter);
            Unregister(BindingIds.BoothCalder, _calder);
            Unregister(BindingIds.BoothInterrogator, _interrogator);
        }

        /// <summary>
        /// Resting state between sequences: tube on, every CRT on light static, scrambler on, Session 12. Inserts and the
        /// death insert expect this (GDD §8.4: "CRTs on static, VU needle live").
        /// </summary>
        public void SetResting()
        {
            if (_tube != null && _tube.Mode == FlickerMode.Off) _tube.SetMode(FlickerMode.On);
            if (_crts != null)
                foreach (var c in _crts)
                    if (c != null && (!c.IsOn || c.Content == CrtContent.Off)) c.ShowStatic(0.85f);
            if (_scrambler != null) _scrambler.SetPowered(true, false);
            if (State != null) State.SetSession11(false);
            if (_look != null) _look.SetSaturationOffset(0f);
            if (_phone != null) _phone.Hang();
            if (_lighter != null) _lighter.Close();
            if (_reel != null) _reel.SetPlaying(false);
            if (_interrogator != null)
            {
                _interrogator.SetActive(true);
                var agent = _interrogator.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.Warp(transform.TransformPoint(_interrogatorHome));
                else _interrogator.transform.localPosition = _interrogatorHome;
                _interrogator.transform.localRotation = _interrogatorHomeRot;
            }
            FeedCameras.Get(BindingIds.FeedFilm)?.ZoomTo(-1f, 0.01f, false);
        }

        /// <summary>Every CRT to static (death insert).</summary>
        public void AllStatic()
        {
            if (_crts == null) return;
            foreach (var c in _crts)
                if (c != null) c.ShowStatic(1f);
        }

        /// <summary>Booth-local point to world.</summary>
        public Vector3 ToWorld(Vector3 local) => transform.TransformPoint(local);

        /// <summary>Booth-local rotation to world.</summary>
        public Quaternion ToWorld(Quaternion local) => transform.rotation * local;

        /// <summary>Builder wiring.</summary>
        public void Setup(BoothCameraRig cam, CRTScreen[] crts, FlickerLight tube, ScramblerBox scrambler, ReelDeck reel,
                          WallClock clock, PenTapper pen, DeskPhone phone, SteelLighter lighter, BoothSmoke[] smoke,
                          GameObject calder, GameObject interrogator, BoothLook look, GameObject slate, bool lightingBaked)
        {
            _camera = cam;
            _crts = crts;
            _tube = tube;
            _scrambler = scrambler;
            _reel = reel;
            _clock = clock;
            _pen = pen;
            _phone = phone;
            _lighter = lighter;
            _smoke = smoke ?? new BoothSmoke[0];
            _calder = calder;
            _interrogator = interrogator;
            _look = look;
            _slate = slate;
            _lightingBaked = lightingBaked;
        }

        /// <summary>Marks the booth lighting as baked (builder, after a bake).</summary>
        public void SetLightingBaked(bool baked) => _lightingBaked = baked;

        private static void Register(string id, Object o)
        {
            if (o != null) CinematicBindings.Register(id, o);
        }

        private static void Unregister(string id, Object o)
        {
            if (o != null) CinematicBindings.Unregister(id, o);
        }

        /// <summary>
        /// Without baked booth probes, dynamic booth renderers would sample the active scene's sky probe (bright blue
        /// ambient underground); until the bake exists they take no probe light at all.
        /// </summary>
        private void DisableProbesOnBodies()
        {
            DisableProbes(_calder);
            DisableProbes(_interrogator);
        }

        private static void DisableProbes(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.lightProbeUsage = LightProbeUsage.Off;
        }
    }
}
