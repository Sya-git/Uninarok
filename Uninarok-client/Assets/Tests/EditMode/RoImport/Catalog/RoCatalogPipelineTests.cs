using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Editor.Catalog;
using Uninarok.RoImport.Editor.Pipeline;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T5.2 (FR-9; PLAN-0008 4.7): the catalog job kinds reach their converters through the batch.
    public class RoCatalogPipelineTests : RoRunFixture
    {
        [Test]
        public void TheRegistryHasTheCatalogConverters()
        {
            Assert.That(RoConverterRegistry.For(RoJobKind.CatalogEntry), Is.InstanceOf<RoCatalogEntryConverter>());
            Assert.That(RoConverterRegistry.For(RoJobKind.CatalogIndex), Is.InstanceOf<RoCatalogIndexConverter>());
            Assert.That(new RoCatalogEntryConverter().Handles(RoPath.Parse("catalog/monsters/1002")), Is.True);
            Assert.That(new RoCatalogIndexConverter().Handles(RoPath.Parse("catalog/monsters/_index")), Is.True);
            Assert.That(new RoCatalogIndexConverter().Handles(RoPath.Parse("catalog/monsters/1002")), Is.False);
        }

        [Test]
        public void EntryJobsRunBeforeIndexJobs()
        {
            var ordered = RoBatch.Order(new[]
            {
                new RoJob(RoJobKind.CatalogIndex, RoPath.Parse("catalog/monsters/_index")),
                new RoJob(RoJobKind.CatalogEntry, RoPath.Parse("catalog/npcs/105")),
            });

            Assert.That(ordered.Select(j => j.Kind), Is.EqualTo(new[] { RoJobKind.CatalogEntry, RoJobKind.CatalogIndex }), "D6: the index after every entry");
        }

        [Test]
        public void AJobOfEachNewKind_ReachesItsConverter()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[RoImport\] Created "));

            var report = RoImportApi.Run(new[]
            {
                new RoJob(RoJobKind.CatalogEntry, RoPath.Parse("catalog/monsters/1002")),
                new RoJob(RoJobKind.CatalogIndex, RoPath.Parse("catalog/monsters/_index")),
            }, Options());

            Assert.That(report.Failures.Select(f => f.Message), Has.Some.Contains("entry converter is pending").And.Some.Contains("index converter is pending"));
        }
    }
}
