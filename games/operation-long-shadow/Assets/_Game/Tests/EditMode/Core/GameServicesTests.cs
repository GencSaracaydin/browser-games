using System.Text.RegularExpressions;
using LongShadow.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LongShadow.Tests.EditMode.Core
{
    public class GameServicesTests
    {
        private interface IFoo
        {
            int N { get; }
        }

        private sealed class Foo : IFoo
        {
            public int N { get; set; }
        }

        private sealed class FooBehaviour : MonoBehaviour, IFoo
        {
            public int N => 9;
        }

        [SetUp]
        public void SetUp() => GameServices.Clear();

        [TearDown]
        public void TearDown()
        {
            GameServices.Clear();
            MarkerRegistry.ResetForTests();
        }

        [Test]
        public void RegisterGetUnregister()
        {
            var f = new Foo { N = 3 };
            GameServices.Register<IFoo>(f);
            Assert.AreSame(f, GameServices.Get<IFoo>());
            Assert.IsTrue(GameServices.TryGet(out IFoo got));
            Assert.AreSame(f, got);
            Assert.IsFalse(GameServices.Unregister<IFoo>(new Foo()), "only the registered instance unregisters");
            Assert.IsTrue(GameServices.Unregister<IFoo>(f));
            Assert.IsFalse(GameServices.Has<IFoo>());
        }

        [Test]
        public void MissingServiceLogsOnceAndReturnsNull()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Service IFoo is not registered"));
            Assert.IsNull(GameServices.Get<IFoo>());
            Assert.IsNull(GameServices.Get<IFoo>()); // second call: no second warning
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ServiceRefRefreshesOnChange()
        {
            var r = new ServiceRef<IFoo>();
            Assert.IsNull(r.Value);
            var a = new Foo { N = 1 };
            GameServices.Register<IFoo>(a);
            Assert.AreSame(a, r.Value);
            var b = new Foo { N = 2 };
            LogAssert.Expect(LogType.Warning, new Regex("replaced"));
            GameServices.Register<IFoo>(b);
            Assert.AreSame(b, r.Value);
        }

        [Test]
        public void DestroyedBehaviourCountsAsMissing()
        {
            var go = new GameObject("svc");
            var fb = go.AddComponent<FooBehaviour>();
            GameServices.Register<IFoo>(fb);
            Assert.IsTrue(GameServices.Has<IFoo>());
            Object.DestroyImmediate(go);
            Assert.IsFalse(GameServices.TryGet(out IFoo _));
        }

        [Test]
        public void MarkerRegistryRegistersItself()
        {
            MarkerRegistry.ResetForTests();
            var reg = MarkerRegistry.Instance;
            Assert.AreSame(reg, GameServices.Get<IMarkerRegistry>());
        }
    }
}
