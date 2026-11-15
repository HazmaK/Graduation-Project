using NUnit.Framework;
using RetroFPS.Settings;
using RetroFPS.Tests.EditMode;
using UnityEngine;

namespace RetroFPS.Tests
{
    public sealed class CardHudStringTests
    {
        [TestCase(LanguageSetting.Turkish)]
        [TestCase(LanguageSetting.English)]
        public void ProductionManifestContainsAllCardKeys(LanguageSetting language)
        {
            var table = LocalizationHudTestContext.CreateTable();
            try
            {
                string[] keys =
                {
                    "hud.card.title",
                    "hud.card.boon.label",
                    "hud.card.curse.label",
                    "hud.card.refuse",
                    "hud.card.hint",
                    "hud.card.acquired.banner",
                    "hud.card.strip.title",
                    "hud.card.blood_debt.name",
                    "hud.card.blood_debt.boon",
                    "hud.card.blood_debt.curse",
                    "hud.card.blood_debt.flavor",
                    "hud.card.starving_steel.name",
                    "hud.card.starving_steel.boon",
                    "hud.card.starving_steel.curse",
                    "hud.card.starving_steel.flavor",
                    "hud.card.last_breath.name",
                    "hud.card.last_breath.boon",
                    "hud.card.last_breath.curse",
                    "hud.card.last_breath.flavor",
                    "hud.card.bone_wings.name",
                    "hud.card.bone_wings.boon",
                    "hud.card.bone_wings.curse",
                    "hud.card.bone_wings.flavor",
                    "hud.card.brimming_chalice.name",
                    "hud.card.brimming_chalice.boon",
                    "hud.card.brimming_chalice.curse",
                    "hud.card.brimming_chalice.flavor",
                    "hud.card.glutton_barrel.name",
                    "hud.card.glutton_barrel.boon",
                    "hud.card.glutton_barrel.curse",
                    "hud.card.glutton_barrel.flavor",
                };
                foreach (string key in keys)
                {
                    string text = table.Get(key, language);
                    Assert.That(text, Is.Not.Null.And.Not.Empty, key);
                    Assert.That(text, Does.Not.Contain(key), key + " returned a missing-key marker");
                    if (key == "hud.card.acquired.banner") Assert.That(text, Does.Contain("{0}"));
                }
            }
            finally { Object.DestroyImmediate(table); }
        }
    }
}
