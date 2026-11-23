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
                    // T9.3 (footage A): scale 4 -> 1 smooth-step over 400 ms, then 1; x drift 3.1 cells decelerating (sin(π t / 2)).
                    (NumberKind.Normal, 0f, 0f, 0f, 2f, 4f, 1f),
                    (NumberKind.Normal, 0.1f, 3.1f * Mathf.Sin(0.05f * Mathf.PI), -0.4f, 2f + 5f * Mathf.Sin(0.15f * Mathf.PI), 4f - 3f * 0.31640625f, 0.9f), // 150 ms: shrinking
                    (NumberKind.Normal, 0.5f, 3.1f * Mathf.Sin(0.25f * Mathf.PI), -2f, 2f + 5f * Mathf.Sin(0.75f * Mathf.PI), 1f, 0.5f),
                    (NumberKind.Critical, 1f, 3.1f, -4f, -3f, 1f, 0f),
                    (NumberKind.Normal, 1f / 3f, 1.55f, -4f / 3f, 7f, 1f, 2f / 3f), // the top of the arc
                    // The miss rises at a constant speed from 0.4 to 3.5 cells (footage A, D31).
                    (NumberKind.Miss, 0f, 0f, 0f, 0.4f, 0.5f, 1f),
                    (NumberKind.Miss, 0.5f, 0f, 0f, 1.95f, 0.5f, 0.5f),
                    (NumberKind.Miss, 1f, 0f, 0f, 3.5f, 0.5f, 0f),
                    (NumberKind.Lucky, 0.5f, 0f, 0f, 5.25f, 0.5f, 0.5f), // as lead
                    // The total grows from 0.6 to 1.5 over 100 ms, then stays (footage C, D32).
                    (NumberKind.Total, 0f, 0f, 0f, 7f, 0.6f, 1f),
                    (NumberKind.Total, 0.02f, 0f, 0f, 7f, 1.05f, 0.98f),
                    (NumberKind.Total, 0.25f, 0f, 0f, 7f, 1.5f, 0.75f),
                    (NumberKind.Normal, 2f, 3.1f, -4f, -3f, 1f, 0f), // past the end: held at it
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
                Assert.AreEqual((1500, 1500, 800, 2500),(NumberMotion.LifeMs(NumberKind.Critical, s), NumberMotion.LifeMs(NumberKind.Miss, s),
                                                         NumberMotion.LifeMs(NumberKind.Lucky, s), NumberMotion.LifeMs(NumberKind.Total, s)));
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }
    }
}
