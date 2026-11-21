using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Editor.Catalog;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T5.1 (FR-12; PLAN-0008 4.8, D8): the item table language setting and its per-run override.
    public class RoImportSettingsTests
    {
        [Test]
        public void TheDefaultIsPortuguese()
        {
            Assert.That(RoImportSettings.DefaultLanguage, Is.EqualTo(RoItemTableLanguage.Portuguese));
            Assert.That(new RoImportOptions().ItemTableLanguage, Is.Null, "no override unless a run asks for one");
        }

        [TestCase(RoItemTableLanguage.Portuguese, "iteminfo_new.lub")]
        [TestCase(RoItemTableLanguage.English, "english/iteminfo_new.lub")]
        [TestCase(RoItemTableLanguage.Spanish, "spanish/iteminfo_new.lub")]
        public void EachLanguageMapsToItsFile(RoItemTableLanguage language, string file)
        {
            Assert.That(RoImportSettings.ItemTableFile(language), Is.EqualTo(file));
        }

        [Test]
        public void TheOverrideBeatsTheSetting()
        {
            Assert.That(RoImportSettings.Effective(RoItemTableLanguage.Spanish, RoItemTableLanguage.Portuguese), Is.EqualTo(RoItemTableLanguage.Spanish));
            Assert.That(RoImportSettings.Effective(null, RoItemTableLanguage.English), Is.EqualTo(RoItemTableLanguage.English));
            Assert.That(RoImportSettings.LanguageFor(new RoImportOptions { ItemTableLanguage = RoItemTableLanguage.English }), Is.EqualTo(RoItemTableLanguage.English));
            Assert.That(RoImportSettings.LanguageFor(null), Is.EqualTo(RoImportSettings.instance.ItemTableLanguage));
        }
    }
}
