using LongShadow.Core.Editor;
using LongShadow.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LongShadow.Tests.EditMode.Input
{
    public class InputTests
    {
        private static InputActionAsset Asset() => AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProjectSetup.InputActionsPath);

        [Test]
        public void AssetHasEveryAction()
        {
            var a = Asset();
            Assert.IsNotNull(a, ProjectSetup.InputActionsPath);
            foreach (var n in InputIds.PlayerActions)
                Assert.IsNotNull(a.FindAction(InputIds.PlayerMap + "/" + n), "Player/" + n);
            foreach (var n in InputIds.UIActions)
                Assert.IsNotNull(a.FindAction(InputIds.UIMap + "/" + n), "UI/" + n);
        }

        [Test]
        public void NeverBindsCtrl()
        {
            // Ctrl+W closes the browser tab (GDD §12, webgl.md §4.8).
            foreach (var map in Asset().actionMaps)
            foreach (var b in map.bindings)
                Assert.That(b.path.ToLowerInvariant(), Does.Not.Contain("ctrl"), map.name + ": " + b.path);
        }

        [Test]
        public void KeyBindingsMatchGdd12()
        {
            var a = Asset();
            string Path(string action) => a.FindAction(InputIds.PlayerMap + "/" + action).bindings[0].path;
            Assert.That(Path(InputIds.Crouch), Is.EqualTo("<Keyboard>/c"));
            Assert.That(Path(InputIds.Frag), Is.EqualTo("<Keyboard>/g"));
            Assert.That(Path(InputIds.Melee), Is.EqualTo("<Keyboard>/v"));
            Assert.That(Path(InputIds.Interact), Is.EqualTo("<Keyboard>/e"));
            Assert.That(Path(InputIds.Objectives), Is.EqualTo("<Keyboard>/tab"));
            Assert.That(a.FindAction(InputIds.PlayerMap + "/" + InputIds.Skip).interactions, Does.Contain("Hold"));
        }

        [Test]
        public void ProjectWideActionsAreOurs()
        {
            Assert.AreSame(Asset(), InputSystem.actions, "run LongShadow/Setup/Apply Project Settings");
        }

        [Test]
        public void SpikeFilterDropsIsolatedSpikesOnly()
        {
            var f = new SpikeFilter(15, 8f, 40f);
            for (var i = 0; i < 20; i++) Assert.That(f.Filter(new Vector2(6f, 2f)), Is.EqualTo(new Vector2(6f, 2f)));
            Assert.That(f.Median, Is.EqualTo(new Vector2(6f, 2f).magnitude).Within(1e-4f));
            Assert.That(f.Filter(new Vector2(900f, 0f)), Is.EqualTo(Vector2.zero), "one-frame spike dropped");
            Assert.That(f.Filter(new Vector2(900f, 0f)), Is.EqualTo(new Vector2(900f, 0f)), "second large frame is real movement");
            Assert.That(f.Dropped, Is.EqualTo(1));
            // After stillness, a small first move is never a spike.
            f.Reset();
            Assert.That(f.Filter(new Vector2(30f, 0f)), Is.EqualTo(new Vector2(30f, 0f)));
            Assert.That(f.Filter(Vector2.zero), Is.EqualTo(Vector2.zero));
        }
    }
}
