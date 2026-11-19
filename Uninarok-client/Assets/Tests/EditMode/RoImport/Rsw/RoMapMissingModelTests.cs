using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsw;
using Uninarok.RoImport.Editor.Map;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T5.1 / T5.3 (FR-10): a missing model gives one warning naming the model and the map; each of its instances
    // becomes a named placeholder in its place, and the map converts.
    [Ignore("pending T5.3")]
    public class RoMapMissingModelTests
    {
        RoMapTestBed _bed;

        [SetUp]
        public void SetUp() => _bed = new RoMapTestBed();

        [TearDown]
        public void TearDown() => _bed.Dispose();

        [Test]
        public void AMissingModel_GivesOneWarning_NamingTheModelAndTheMap()
        {
            var result = _bed.Convert();

            Assert.AreEqual(RoStatus.Created, result.Status, "the map still converts");
            var warnings = _bed.LastReport.Issues.Where(i => i.Code == RoIssueCode.ModelMissing).ToList();
            Assert.AreEqual(1, warnings.Count, "one warning for the two instances");
            StringAssert.Contains("missing.rsm", warnings[0].Item);
            Assert.AreEqual("sample.rsw", warnings[0].Referrer);
        }

        [Test]
        public void EachInstanceOfAMissingModel_IsAPlaceholder_WithItsNameAndTransform()
        {
            _bed.Convert();
            var models = RoMapNames.FindGroup(_bed.OpenScene().transform, RoMapGroup.Models);
            var space = new RoMapSpace(RoMapTestBed.Width, RoMapTestBed.Height, 10f);

            foreach (int i in new[] { 4, 5 })
            {
                var model = RoMapTestBed.Sample().Models[i];
                var t = models.GetChild(i);
                Assert.AreEqual(model.Name, t.name);
                Assert.AreEqual(model.File, t.GetComponent<MapMissingModelMarker>().modelFile);
                Assert.AreEqual(0, t.GetComponentsInChildren<Renderer>().Length, "empty");
                RoRswPlacement.ToUnity(RoRswPlacement.ModelMatrixRo(model, space, RoRswRules.V1), out var p, out var r, out _);
                Assert.Less(Vector3.Distance(p, t.position), 1e-4f, model.Name + " position");
                Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(r, t.rotation)), 1e-4f, model.Name + " rotation");
            }
        }

        [Test]
        public void TheOtherInstances_AreNotAffected()
        {
            _bed.Convert();
            var models = RoMapNames.FindGroup(_bed.OpenScene().transform, RoMapGroup.Models);

            Assert.AreEqual(7, models.childCount);
            foreach (int i in new[] { 0, 1, 2, 3, 6 })
                Assert.Greater(models.GetChild(i).GetComponentsInChildren<Renderer>().Length, 0, models.GetChild(i).name);
        }
    }
}
