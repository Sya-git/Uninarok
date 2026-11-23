using NUnit.Framework;
using UnityEngine;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-6 (PLAN-0011 4.8): the poses of combat-reference.md at fixed times.
    public class NumberMotionTests
    {
        [Test]
        public void NumberMotion_Table()
        {
            var s = ScriptableObject.CreateInstance<CombatSettings>();
            try
            {
                var rows = new (NumberKind kind, float t, float x, float y, float height, float scale, float alpha)[]
                {
                    (NumberKind.Normal, 0f, 0f, 0f, 2f, 4f, 1f),
                    (NumberKind.Normal, 0.5f, 2f, -2f, 2f + 5f * Mathf.Sin(0.75f * Mathf.PI), 2f, 0.5f),
                    (NumberKind.Critical, 1f, 4f, -4f, -3f, 0f, 0f),
                    (NumberKind.Normal, 1f / 3f, 4f / 3f, -4f / 3f, 7f, 4f * 2f / 3f, 2f / 3f), // the top of the arc
                    (NumberKind.Miss, 0f, 0f, 0f, 3.5f, 0.5f, 1f),
                    (NumberKind.Lucky, 0.5f, 0f, 0f, 5.25f, 0.5f, 0.5f),
                    (NumberKind.Miss, 1f, 0f, 0f, 7f, 0.5f, 0f),
                    (NumberKind.Total, 0.25f, 0f, 0f, 7f, 2f, 0.75f),
                    (NumberKind.Normal, 2f, 4f, -4f, -3f, 0f, 0f), // past the end: held at it
                };
                foreach (var r in rows)
                {
                    var p = NumberMotion.At(r.kind, r.t, s);
                    string at = $"{r.kind} at {r.t}";
                    Assert.AreEqual(r.x, p.X, 1e-4f, at + ": x");
                    Assert.AreEqual(r.y, p.Y, 1e-4f, at + ": y");
                    Assert.AreEqual(r.height, p.Height, 1e-4f, at + ": height");
                    Assert.AreEqual(r.scale, p.Scale, 1e-4f, at + ": scale");
                    Assert.AreEqual(r.alpha, p.Alpha, 1e-4f, at + ": alpha");
                }
                Assert.AreEqual((1500, 800, 800, 3000), (NumberMotion.LifeMs(NumberKind.Critical, s), NumberMotion.LifeMs(NumberKind.Miss, s),
                                                         NumberMotion.LifeMs(NumberKind.Lucky, s), NumberMotion.LifeMs(NumberKind.Total, s)));
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }
    }
}
