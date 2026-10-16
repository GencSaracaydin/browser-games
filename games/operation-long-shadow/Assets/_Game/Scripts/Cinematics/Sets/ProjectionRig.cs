using LongShadow.Cinematics.Booth;
using LongShadow.Cinematics.FX;
using LongShadow.Core;
using UnityEngine;
using UnityEngine.Video;

namespace LongShadow.Cinematics.Sets
{
    /// <summary>
    /// The screening-room projection of MID-B (GDD §8.6; CINEMATIC_DIRECTION §3): the 16 mm image on the cracked screen
    /// through SG_Film16mm (countdown leader video, then the live booth film feed at 18 fps), a shadowed warm spot from
    /// the lens that silhouettes Sparrow and Tinker, an additive volumetric beam (SG_FilmBeam) with dust motes, and the
    /// projector loop. Finds WP10's screen (B8.Screen marker, else the M_ProjectorScreen renderer near B8.Projector) or
    /// spawns its own screen quad. Torn down after the sequence; the screen's materials are restored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectionRig : MonoBehaviour
    {
        /// <summary>Projector marker (TECH_PLAN Appendix A B8).</summary>
        public const string ProjectorMarker = "B8.Projector";
        /// <summary>Player's head at the projector (camera of MID-B).</summary>
        public const string CameraMarker = "B8.CamReel";
        /// <summary>Optional screen marker requested from WP10 (REQUESTS.md): centre, forward toward the projector, scale = size.</summary>
        public const string ScreenMarker = "B8.Screen";
        /// <summary>Fallback screen distance north of the camera (m).</summary>
        public const float FallbackScreenDistance = 8.0f;
        /// <summary>Fallback screen size (m).</summary>
        public static readonly Vector2 FallbackScreenSize = new Vector2(2.6f, 1.95f);
        /// <summary>Film frame rate (GDD §8.6 shot 4: stepped at 18 fps).</summary>
        public const float FilmFps = 18f;

        private ServiceRef<IMarkerRegistry> _markers;
        private Transform _root;
        private Renderer _screen;
        private int _screenSlot = -1;
        private Material[] _screenOriginal;
        private Material _film;
        private bool _ownScreen;
        private Light _spot;
        private float _spotBase = 9f;
        private MeshRenderer _beam;
        private Material _beamMat;
        private ParticleSystem _motes;
        private VideoPlayer _video;
        private RenderTexture _videoRt;
        private string _content;
        private bool _filmFeedHeld;
        private int _loop;

        /// <summary>The active instance.</summary>
        public static ProjectionRig Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>True while set up.</summary>
        public bool Active => _root != null;
        /// <summary>World centre of the screen (camera look-at).</summary>
        public Transform ScreenCentre { get; private set; }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            Teardown();
            if (Instance == this) Instance = null;
        }

        /// <summary>Builds the rig at the B8 markers (idempotent).</summary>
        public void Setup()
        {
            if (_root != null) return;
            var reg = _markers.Value;
            Transform projector = null, cam = null;
            if (reg != null)
            {
                projector = reg.Get(ProjectorMarker);
                cam = reg.Get(CameraMarker);
            }
            var camPos = cam != null ? cam.position : new Vector3(323f, 6.45f, 351.4f);
            var lens = (projector != null ? projector.position : camPos - new Vector3(0f, 0.85f, 0.6f)) + new Vector3(0f, 0.62f, 0.55f);

            _root = new GameObject("MidB_Projection").transform;
            _root.SetParent(transform, false);

            FindOrMakeScreen(reg, projector != null ? projector.position : camPos, camPos, out var centre, out var normal, out var size);
            var centreGo = new GameObject("ScreenCentre").transform;
            centreGo.SetParent(_root, false);
            centreGo.position = centre;
            centreGo.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            ScreenCentre = centreGo;
            CinematicBindings.Register(BindingIds.Projection, centreGo);

            BuildSpot(lens, centre, size);
            BuildBeam(lens, centre, normal, size);
            BuildMotes(lens, centre);
            _loop = 0;
            Show("Off");
        }

        /// <summary>
        /// Content: "Off", "Blank" (lamp on, clear leader), "Leader" (VID_FilmLeader), "Film" (booth film feed at 18 fps),
        /// "Booth" (same feed, steadier and less graded).
        /// </summary>
        public void Show(string content)
        {
            if (_root == null) Setup();
            _content = content;
            StopVideo();
            if (_filmFeedHeld)
            {
                FeedCameras.Release(BindingIds.FeedFilm);
                _filmFeedHeld = false;
            }
            var on = content != "Off";
            if (_spot != null) _spot.enabled = on;
            if (_beam != null) _beam.enabled = on;
            if (_motes != null)
            {
                if (on) _motes.Play();
                else _motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (on && _loop == 0) _loop = CinematicAudio.Loop(CinematicCues.ProjectorLoop, _spot != null ? _spot.transform : _root);
            if (!on && _loop != 0)
            {
                CinematicAudio.Stop(_loop, 0.3f);
                _loop = 0;
            }
            if (_film == null) return;
            _film.SetFloat(ShaderIds.Brightness, on ? 1.8f : 0f);
            _film.SetFloat(ShaderIds.Burn, 0f);
            switch (content)
            {
                case "Leader":
                    PlayVideo(CinematicAssetIds.VideoLeader);
                    _film.SetFloat(ShaderIds.Mono, 1f);
                    _film.SetFloat(ShaderIds.Weave, 1f);
                    break;
                case "Film":
                case "Booth":
                    var rt = FeedCameras.Acquire(BindingIds.FeedFilm);
                    _filmFeedHeld = rt != null;
                    var feed = FeedCameras.Get(BindingIds.FeedFilm);
                    if (feed != null) feed.Fps = content == "Film" ? FilmFps : 0f;
                    _film.SetTexture(ShaderIds.MainTex, rt != null ? (Texture)rt : Texture2D.blackTexture);
                    _film.SetFloat(ShaderIds.Mono, content == "Film" ? 1f : 0.35f);
                    _film.SetFloat(ShaderIds.Weave, content == "Film" ? 1f : 0.25f);
                    _film.SetFloat(ShaderIds.Scratches, content == "Film" ? 1f : 0.2f);
                    break;
                case "Blank":
                    _film.SetTexture(ShaderIds.MainTex, Texture2D.whiteTexture);
                    _film.SetFloat(ShaderIds.Brightness, 0.9f);
                    _film.SetFloat(ShaderIds.Mono, 1f);
                    _film.SetFloat(ShaderIds.Weave, 1f);
                    break;
                default:
                    _film.SetTexture(ShaderIds.MainTex, Texture2D.blackTexture);
                    break;
            }
        }

        /// <summary>Film burn-out on the screen 0..1 (MID-B shot 8).</summary>
        public void SetBurn(float t01)
        {
            if (_film != null) _film.SetFloat(ShaderIds.Burn, Mathf.Clamp01(t01));
        }

        /// <summary>Removes everything and restores the screen.</summary>
        public void Teardown()
        {
            StopVideo();
            if (_filmFeedHeld) FeedCameras.Release(BindingIds.FeedFilm);
            _filmFeedHeld = false;
            CinematicAudio.Stop(_loop, 0.2f);
            _loop = 0;
            if (_screen != null && _screenOriginal != null && !_ownScreen) _screen.sharedMaterials = _screenOriginal;
            _screen = null;
            _screenOriginal = null;
            if (ScreenCentre != null) CinematicBindings.Unregister(BindingIds.Projection, ScreenCentre);
            ScreenCentre = null;
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            if (_film != null) Destroy(_film);
            if (_beamMat != null) Destroy(_beamMat);
            if (_videoRt != null)
            {
                _videoRt.Release();
                Destroy(_videoRt);
            }
            _film = null;
            _beamMat = null;
            _videoRt = null;
        }

        private void Update()
        {
            if (_spot == null || !_spot.enabled) return;
            // 24 fps shutter flicker plus a slow lamp breathing (light on the faces, CINEMATIC_DIRECTION §3).
            var t = Time.unscaledTime;
            var shutter = 0.9f + 0.1f * Mathf.Sign(Mathf.Sin(t * Mathf.PI * 2f * 24f));
            var breathe = 0.95f + 0.05f * Mathf.PerlinNoise(t * 2.3f, 0.4f);
            _spot.intensity = _spotBase * shutter * breathe;
        }

        // ------------------------------------------------------------------ build

        private void FindOrMakeScreen(IMarkerRegistry reg, Vector3 projector, Vector3 camPos, out Vector3 centre, out Vector3 normal, out Vector2 size)
        {
            _film = MakeFilmMaterial();
            if (reg != null && reg.TryGet(ScreenMarker, out SceneMarker m))
            {
                centre = m.Position;
                normal = m.transform.forward;
                size = new Vector2(Mathf.Max(0.5f, m.transform.lossyScale.x), Mathf.Max(0.4f, m.transform.lossyScale.y));
                _screen = FindScreenRenderer(centre, 3f);
            }
            else
            {
                _screen = FindScreenRenderer(projector, 15f);
                if (_screen != null)
                {
                    var b = _screen.bounds;
                    centre = b.center;
                    normal = (camPos - centre);
                    normal.y = 0f;
                    normal = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.back;
                    size = new Vector2(Mathf.Max(b.size.x, b.size.z) * 0.92f, b.size.y * 0.8f);
                }
                else
                {
                    centre = camPos + new Vector3(0f, 0.15f, FallbackScreenDistance);
                    normal = Vector3.back;
                    size = FallbackScreenSize;
                }
            }
            if (_screen != null)
            {
                _screenOriginal = _screen.sharedMaterials;
                var mats = _screen.sharedMaterials;
                _screenSlot = 0;
                for (var i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.Contains("ProjectorScreen") && !mats[i].name.Contains("Frame")) _screenSlot = i;
                if (_film != null) mats[_screenSlot] = _film;
                _screen.sharedMaterials = mats;
                return;
            }
            LSLog.WarnOnce("midb-screen", LogCategory.Cinematics, "MID-B: no projector screen found near " + ProjectorMarker + "; using a quad.", this);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "MidB_Screen";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(_root, false);
            quad.transform.position = centre;
            quad.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            _screen = quad.GetComponent<Renderer>();
            _screen.sharedMaterial = _film;
            _ownScreen = true;
        }

        private static Renderer FindScreenRenderer(Vector3 near, float radius)
        {
            Renderer best = null;
            var bestD = radius * radius;
            foreach (var r in FindObjectsByType<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                var hit = false;
                foreach (var m in mats)
                    if (m != null && m.name.StartsWith("M_ProjectorScreen") && !m.name.Contains("Frame")) hit = true;
                if (!hit) continue;
                var d = (r.bounds.center - near).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = r;
                }
            }
            return best;
        }

        private Material MakeFilmMaterial()
        {
            var template = CinematicLibrary.Lookup<Material>(CinematicAssetIds.MatFilm);
            if (template != null) return new Material(template) { name = "M_FX_Film16mm_Runtime" };
            var shader = Shader.Find(ShaderIds.FilmShader);
            if (shader != null) return new Material(shader) { name = "M_FX_Film16mm_Runtime" };
            LSLog.MissingAsset(LogCategory.Cinematics, ShaderIds.FilmShader, "Assets/_Game/Art/Shaders/SG_Film16mm.shader", this);
            return null;
        }

        private void BuildSpot(Vector3 lens, Vector3 centre, Vector2 size)
        {
            var go = new GameObject("ProjectorSpot");
            go.transform.SetParent(_root, false);
            go.transform.position = lens;
            go.transform.rotation = Quaternion.LookRotation(centre - lens, Vector3.up);
            _spot = go.AddComponent<Light>();
            _spot.type = LightType.Spot;
            var dist = Vector3.Distance(lens, centre);
            var half = Mathf.Atan(Mathf.Max(size.x, size.y) * 0.5f / Mathf.Max(0.5f, dist)) * Mathf.Rad2Deg;
            _spot.spotAngle = Mathf.Clamp(half * 2f * 1.15f, 5f, 120f);
            _spot.innerSpotAngle = _spot.spotAngle * 0.8f;
            _spot.range = dist + 3f;
            _spot.color = new Color(1f, 0.93f, 0.82f);
            _spot.intensity = _spotBase;
            _spot.shadows = LightShadows.Soft;
            _spot.shadowStrength = 0.9f;
            _spot.renderingLayerMask = (int)RenderingLayerIds.WorldMask;
            _spot.enabled = false;
        }

        private void BuildBeam(Vector3 lens, Vector3 centre, Vector3 normal, Vector2 size)
        {
            var template = CinematicLibrary.Lookup<Material>(CinematicAssetIds.MatFilmBeam);
            if (template == null)
            {
                var shader = Shader.Find(ShaderIds.FilmBeamShader);
                if (shader == null) return;
                _beamMat = new Material(shader) { name = "M_FX_FilmBeam_Runtime" };
            }
            else _beamMat = new Material(template) { name = "M_FX_FilmBeam_Runtime" };
            var go = new GameObject("ProjectorBeam");
            go.transform.SetParent(_root, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BeamMesh(lens, centre, normal, size);
            _beam = go.AddComponent<MeshRenderer>();
            _beam.sharedMaterial = _beamMat;
            _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beam.receiveShadows = false;
            _beam.enabled = false;
        }

        /// <summary>A four-sided frustum from the lens to the screen corners (u around, v from lens 0 to screen 1).</summary>
        public static Mesh BeamMesh(Vector3 lens, Vector3 centre, Vector3 normal, Vector2 size)
        {
            var right = Vector3.Cross(Vector3.up, normal).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            var up = Vector3.Cross(normal, right).normalized;
            var hx = right * size.x * 0.5f;
            var hy = up * size.y * 0.5f;
            var corners = new[] { centre - hx - hy, centre + hx - hy, centre + hx + hy, centre - hx + hy };
            var spread = 0.035f; // the aperture is not a point
            var lensCorners = new[] { lens - right * spread - up * spread * 0.75f, lens + right * spread - up * spread * 0.75f,
                                      lens + right * spread + up * spread * 0.75f, lens - right * spread + up * spread * 0.75f };
            var verts = new Vector3[16];
            var uvs = new Vector2[16];
            var tris = new int[24];
            for (var s = 0; s < 4; s++)
            {
                var a = s;
                var b = (s + 1) % 4;
                var v = s * 4;
                verts[v] = lensCorners[a];
                verts[v + 1] = lensCorners[b];
                verts[v + 2] = corners[b];
                verts[v + 3] = corners[a];
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);
                var t = s * 6;
                tris[t] = v;
                tris[t + 1] = v + 1;
                tris[t + 2] = v + 2;
                tris[t + 3] = v;
                tris[t + 4] = v + 2;
                tris[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "MidB_Beam" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildMotes(Vector3 lens, Vector3 centre)
        {
            var go = new GameObject("BeamDust");
            go.transform.SetParent(_root, false);
            go.transform.position = Vector3.Lerp(lens, centre, 0.45f);
            go.transform.rotation = Quaternion.LookRotation(centre - lens, Vector3.up);
            _motes = go.AddComponent<ParticleSystem>();
            _motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _motes.main;
            main.useUnscaledTime = true;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
            main.startColor = new Color(1f, 0.95f, 0.85f, 0.55f);
            main.maxParticles = 260;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _motes.emission;
            emission.rateOverTime = 30f;
            var shape = _motes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.1f, 0.8f, Vector3.Distance(lens, centre) * 0.85f);
            var noise = _motes.noise;
            noise.enabled = true;
            noise.strength = 0.03f;
            noise.frequency = 0.4f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (shader != null)
            {
                var m = new Material(shader) { name = "M_BeamDust_Runtime" };
                m.SetTexture("_BaseMap", BoothSmoke.Puff());
                r.sharedMaterial = m;
            }
        }

        // ------------------------------------------------------------------ video (leader)

        private void PlayVideo(string file)
        {
            if (_video == null)
            {
                _video = gameObject.AddComponent<VideoPlayer>();
                _video.playOnAwake = false;
                _video.source = VideoSource.Url;
                _video.renderMode = VideoRenderMode.RenderTexture;
                _video.audioOutputMode = VideoAudioOutputMode.None;
                _video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                _video.prepareCompleted += vp => vp.Play();
                _video.errorReceived += (vp, msg) => LSLog.WarnOnce("midb-video", LogCategory.Cinematics, "MID-B leader video failed: " + msg, this);
            }
            if (_videoRt == null)
            {
                _videoRt = new RenderTexture(FeedCamera.Width, FeedCamera.Height, 0, RenderTextureFormat.ARGB32) { name = "RT_MidB_Leader" };
                _videoRt.Create();
            }
            _video.targetTexture = _videoRt;
            _video.isLooping = false;
            _video.url = CRTScreen.VideoUrl(file);
            _video.Prepare();
            if (_film != null) _film.SetTexture(ShaderIds.MainTex, _videoRt);
        }

        private void StopVideo()
        {
            if (_video != null && (_video.isPlaying || _video.isPrepared)) _video.Stop();
        }
    }
}
