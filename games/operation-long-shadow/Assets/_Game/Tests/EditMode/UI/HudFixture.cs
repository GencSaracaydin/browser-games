using LongShadow.Core;
using LongShadow.UI.Common;
using LongShadow.UI.Dev;
using LongShadow.UI.Editor;
using LongShadow.UI.Hud;
using NUnit.Framework;
using UnityEngine;

namespace LongShadow.Tests.EditMode.UI
{
    /// <summary>
    /// A HUD built in edit mode with the UI package's fake services and a free camera at the origin looking +Z. Needs TMP
    /// Essential Resources (tests are ignored otherwise: run LongShadow/UI/1 Import TMP Essential Resources).
    /// </summary>
    internal sealed class HudFixture
    {
        public HudRoot Hud;
        public GameObject HudObject;
        public GameObject PlayerObject;
        public FakePlayerRig Rig;
        public FakeWeaponHudState Weapon;
        public FakeObjectiveService Objectives;
        public FakeLightStage Light;
        public FakeSettingsService Settings;
        public FakePauseService Pause;
        public FakeMissionDirector Director;
        public FakePlayerHealth Health;
        public FakeSquadService Squad;
        public FakeCheckpointService Checkpoints;
        public FakeInputReader Input;
        public RenderTexture Target;

        public static void RequireTmp()
        {
            if (!FontAssetBuilder.TmpEssentialsPresent)
                Assert.Ignore("TMP Essential Resources are not imported (LongShadow/UI/1 Import TMP Essential Resources).");
        }

        public static HudFixture Create()
        {
            RequireTmp();
            GameServices.Clear();
            var f = new HudFixture();
            f.Weapon = new FakeWeaponHudState();
            f.Objectives = new FakeObjectiveService();
            f.Light = new FakeLightStage();
            f.Settings = new FakeSettingsService();
            f.Pause = new FakePauseService();
            f.Director = new FakeMissionDirector();
            f.Health = new FakePlayerHealth();
            f.Squad = new FakeSquadService();
            f.Checkpoints = new FakeCheckpointService();
            f.Input = new FakeInputReader();
            GameServices.Register<IWeaponHudState>(f.Weapon);
            GameServices.Register<IObjectiveService>(f.Objectives);
            GameServices.Register<ILightStage>(f.Light);
            GameServices.Register<ISettingsService>(f.Settings);
            GameServices.Register<IPauseService>(f.Pause);
            GameServices.Register<IMissionDirector>(f.Director);
            GameServices.Register<IPlayerHealth>(f.Health);
            GameServices.Register<ISquadService>(f.Squad);
            GameServices.Register<ICheckpointService>(f.Checkpoints);
            GameServices.Register<IInputReader>(f.Input);

            f.PlayerObject = new GameObject("TestPlayer");
            f.Rig = f.PlayerObject.AddComponent<FakePlayerRig>();
            var camGo = new GameObject("TestCamera");
            camGo.transform.SetParent(f.PlayerObject.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            // A 1920x1080 target makes projection independent of the editor's game-view size.
            f.Target = new RenderTexture(1920, 1080, 16) { name = "HudFixture RT" };
            cam.targetTexture = f.Target;
            f.Rig.SetCamera(cam);
            GameServices.Register<IPlayerRig>(f.Rig);

            f.HudObject = new GameObject("TestHUD");
            f.Hud = f.HudObject.AddComponent<HudRoot>();
            f.Hud.Initialize(null);
            f.Hud.SetEventsEnabled(true);
            return f;
        }

        public void Tick(float seconds, float dt = 1f / 60f)
        {
            for (var t = 0f; t < seconds; t += dt) Hud.TickAll(dt);
        }

        public void Dispose()
        {
            if (Hud != null) Hud.SetEventsEnabled(false);
            if (HudObject != null) Object.DestroyImmediate(HudObject);
            if (PlayerObject != null) Object.DestroyImmediate(PlayerObject);
            if (Target != null)
            {
                Target.Release();
                Object.DestroyImmediate(Target);
            }
            GameServices.Clear();
            Time.timeScale = 1f;
        }
    }
}
