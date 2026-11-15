using NUnit.Framework;
using RetroFPS.Run.Cards;
using RetroFPS.Settings;
using RetroFPS.Tests.EditMode;
using RetroFPS.UI.HUD;
using UnityEngine;

namespace RetroFPS.Tests
{
    public sealed class CardAcquiredBannerTests
    {
        [Test]
        public void QueuedAcquisitionRetainsPayloadAndLiveLanguageSwitchKeepsTiming()
        {
            var table = LocalizationHudTestContext.CreateTable();
            var first = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            var second = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            try
            {
                first.Configure("blood_debt", "hud.card.blood_debt.name", "", "", "", Color.white, null);
                second.Configure("bone_wings", "hud.card.bone_wings.name", "", "", "", Color.white, null);
                var model = new CardAcquiredBannerModel(1d, 0d, 0d);
                model.Enqueue(first, table, LanguageSetting.English, 0d);
                model.Enqueue(second, table, LanguageSetting.English, 0.1d);
                Assert.That(model.Text, Is.EqualTo("PRICE PAID: Blood Debt"));
                model.RefreshLanguage(table, LanguageSetting.Turkish);
                Assert.That(model.Text, Is.EqualTo(string.Format(table.Get("hud.card.acquired.banner", LanguageSetting.Turkish), table.Get(first.NameKey, LanguageSetting.Turkish))));
                model.Tick(1.01d);
                Assert.That(model.Text, Does.Contain(table.Get(second.NameKey, LanguageSetting.Turkish)));
                model.Tick(2.01d); Assert.That(model.Visible, Is.False);
                model.Clear(); Assert.That(model.Text, Is.Empty);
                model.Enqueue(null, table, LanguageSetting.English, 3d); Assert.That(model.Visible, Is.False);
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); Object.DestroyImmediate(table); }
        }
    }
}
