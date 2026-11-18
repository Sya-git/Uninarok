using System.Text;
using NUnit.Framework;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-8: texture settings depend on usage, decided by path and not by extension. Plan 4.4, D8. TASKS T3.4.
    public class RoTextureUsageTests
    {
        [TestCase(@"data\texture\유저인터페이스\basic_interface\btn.bmp", RoTextureUsage.Ui, TestName = "Usage: UI folder is Ui")]
        [TestCase("texture/유저인터페이스/a.tga", RoTextureUsage.Ui, TestName = "Usage: any extension in the UI folder is Ui")]
        [TestCase("texture/나무잡초꽃/leaf.bmp", RoTextureUsage.World, TestName = "Usage: model texture is World")]
        [TestCase("texture/backside.bmp", RoTextureUsage.World, TestName = "Usage: top-level texture is World")]
        [TestCase("texture/유저인터페이스.bmp", RoTextureUsage.World, TestName = "Usage: a file named like the folder is World")]
        [TestCase("model/a.rsm", RoTextureUsage.World, TestName = "Usage: a non-texture path is World")]
        public void Usage_IsDecidedByThePath(string raw, RoTextureUsage expected)
        {
            Assert.AreEqual(expected, RoTextureUsageRules.For(RoPath.Parse(raw)));
        }

        [Test]
        public void UiFolder_MatchesTheDecomposedSpellingToo()
        {
            string decomposed = "texture/유저인터페이스/btn.bmp".Normalize(NormalizationForm.FormD);

            Assert.AreEqual(RoTextureUsage.Ui, RoTextureUsageRules.For(RoPath.Parse(decomposed)));
        }
    }
}
