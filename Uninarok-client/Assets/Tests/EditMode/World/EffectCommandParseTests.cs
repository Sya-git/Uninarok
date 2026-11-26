using NUnit.Framework;
using Uninarok.World.Dev;

namespace Uninarok.World.Tests
{
    // SPEC-0012 FR-8 (PLAN-0012 4.8): /effect arguments, parsed from the end.
    public class EffectCommandParseTests
    {
        static EffectCommand Parse(string args)
        {
            Assert.IsTrue(EffectCommand.TryParse(args, out var cmd, out var error), $"'{args}' failed: {error}");
            return cmd;
        }

        static void AssertError(string args)
        {
            Assert.IsFalse(EffectCommand.TryParse(args, out _, out var error), $"'{args}' should not parse");
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [Test]
        public void PathWithSpacesCellAndLoop()
        {
            var c = Parse("a b.str 10 20 loop");
            Assert.AreEqual(EffectCommandKind.PlayAtCell, c.Kind);
            Assert.AreEqual("a b.str", c.Path);
            Assert.AreEqual(10, c.CellX);
            Assert.AreEqual(20, c.CellY);
            Assert.IsTrue(c.Loop);
        }

        [Test]
        public void QuotedPathWithSpaces()
        {
            var c = Parse("\"x y.str\"");
            Assert.AreEqual(EffectCommandKind.PlayOnSelf, c.Kind);
            Assert.AreEqual("x y.str", c.Path);
            Assert.IsFalse(c.Loop);
        }

        [Test]
        public void QuotedPathWithCell()
        {
            var c = Parse("\"x y.str\" 10 20");
            Assert.AreEqual(EffectCommandKind.PlayAtCell, c.Kind);
            Assert.AreEqual("x y.str", c.Path);
            Assert.AreEqual(10, c.CellX);
            Assert.AreEqual(20, c.CellY);
            Assert.IsFalse(c.Loop);
        }

        [Test]
        public void PathAndLoopPlaysOnSelfLooped()
        {
            var c = Parse("p loop");
            Assert.AreEqual(EffectCommandKind.PlayOnSelf, c.Kind);
            Assert.AreEqual("p", c.Path);
            Assert.IsTrue(c.Loop);
        }

        [Test]
        public void LoopOnlyCountsAsLastToken()
        {
            // "loop" in front of the cell is part of the path (parsing from the end).
            var c = Parse("loop a.str 3 4");
            Assert.AreEqual(EffectCommandKind.PlayAtCell, c.Kind);
            Assert.AreEqual("loop a.str", c.Path);
            Assert.IsFalse(c.Loop);
            Assert.AreEqual(3, c.CellX);
            Assert.AreEqual(4, c.CellY);
        }

        [Test]
        public void StopIsCaseInsensitive()
        {
            Assert.AreEqual(EffectCommandKind.Stop, Parse("stop").Kind);
            Assert.AreEqual(EffectCommandKind.Stop, Parse("STOP").Kind);
        }

        [Test]
        public void QuotedStopIsAPath()
        {
            var c = Parse("\"stop\"");
            Assert.AreEqual(EffectCommandKind.PlayOnSelf, c.Kind);
            Assert.AreEqual("stop", c.Path);
        }

        [Test]
        public void StopWithLoopIsAPathNamedStopLooped()
        {
            var c = Parse("stop loop");
            Assert.AreEqual(EffectCommandKind.PlayOnSelf, c.Kind);
            Assert.AreEqual("stop", c.Path);
            Assert.IsTrue(c.Loop);
        }

        [Test]
        public void SingleOrNonNumericTrailingTokensStayInThePath()
        {
            // Only two trailing integers make a cell (PLAN-0012 4.8); anything else is path text.
            var one = Parse("a.str 10");
            Assert.AreEqual(EffectCommandKind.PlayOnSelf, one.Kind);
            Assert.AreEqual("a.str 10", one.Path);

            var bad = Parse("a.str x y");
            Assert.AreEqual(EffectCommandKind.PlayOnSelf, bad.Kind);
            Assert.AreEqual("a.str x y", bad.Path);

            var overflow = Parse("a.str 99999999999 5");
            Assert.AreEqual(EffectCommandKind.PlayOnSelf, overflow.Kind);
            Assert.AreEqual("a.str 99999999999 5", overflow.Path);
        }

        [Test]
        public void NegativeCellIsAccepted()
        {
            // Range checking is PlayAtCell's job, not the parser's.
            var c = Parse("a.str -3 -4");
            Assert.AreEqual(EffectCommandKind.PlayAtCell, c.Kind);
            Assert.AreEqual("a.str", c.Path);
            Assert.AreEqual(-3, c.CellX);
            Assert.AreEqual(-4, c.CellY);
        }

        [Test]
        public void MissingPathIsAnError()
        {
            AssertError("loop");
            AssertError("10 20");
            AssertError("10 20 loop");
            AssertError("\"\"");
        }

        [Test]
        public void EmptyArgumentsAreAUsageError()
        {
            AssertError("");
            AssertError("   ");
        }
    }
}
