using System;
using NUnit.Framework;
using RetroFPS.Run;
using RetroFPS.Run.Cards;
using RetroFPS.Settings;
using RetroFPS.Tests.EditMode;
using RetroFPS.UI.HUD;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RetroFPS.Tests
{
    public sealed class RunSummaryModelTests
    {
        [TestCase(0f, "00:00")]
        [TestCase(59f, "00:59")]
        [TestCase(61f, "01:01")]
        [TestCase(3600f, "1:00:00")]
        [TestCase(3661f, "1:01:01")]
        [TestCase(-5f, "00:00")]
        [TestCase(float.NaN, "00:00")]
        public void TimeFormatting_FormatsMinutesSecondsAndHours(float seconds, string expected)
        {
            Assert.That(RunSummaryModel.FormatTime(seconds), Is.EqualTo(expected));
        }

        [TestCase(0f, 0)]
        [TestCase(12.4f, 12)]
        [TestCase(12.5f, 13)]
        [TestCase(99.6f, 100)]
        [TestCase(-3f, 0)]
        public void DamageRounding_RoundsToNearestInt(float damage, int expected)
        {
            Assert.That(RunSummaryModel.RoundDamage(damage), Is.EqualTo(expected));
        }

        [Test]
        public void BuildRows_FormatsStatsArafTowerSplitAndCardNamesInBothLanguages()
        {
            var table = LocalizationHudTestContext.CreateTable();
            var first = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            var second = ScriptableObject.CreateInstance<CurseBlessingDefinition>();
            try
            {
                first.Configure("blood_debt", "hud.card.blood_debt.name", "", "", "", Color.white, null);
                second.Configure("bone_wings", "hud.card.bone_wings.name", "", "", "", Color.white, null);
                var summary = new RunSummary(
                    floorNumber: 2,
                    totalSeconds: 125f,
                    arafSeconds: 45f,
                    totalKills: 18,
                    towerKills: 12,
                    arafKills: 6,
                    criticalKills: 5,
                    gibKills: 3,
                    damageTaken: 42.5f,
                    arafChargeUsed: true,
                    cards: new[] { first, second });

                RunSummaryRow[] trRows = RunSummaryModel.BuildRows(in summary, table, LanguageSetting.Turkish);
                Assert.That(trRows.Length, Is.EqualTo(9));
                Assert.That(RunSummaryModel.StatRowCount, Is.EqualTo(8));
                Assert.That(trRows[0].Label, Is.EqualTo("Kat"));
                Assert.That(trRows[0].Value, Is.EqualTo("2"));
                Assert.That(trRows[1].Value, Is.EqualTo("02:05"));
                Assert.That(trRows[2].Value, Is.EqualTo("00:45"));
                Assert.That(trRows[3].Value, Is.EqualTo("18"));
                Assert.That(trRows[4].Label, Is.EqualTo("Kule / Araf öldürme"));
                Assert.That(trRows[4].Value, Is.EqualTo("12 / 6"));
                Assert.That(trRows[5].Value, Is.EqualTo("5"));
                Assert.That(trRows[6].Value, Is.EqualTo("43"));
                Assert.That(trRows[7].Value, Is.EqualTo("Kullan\u0131ld\u0131"));
                Assert.That(trRows[8].Value, Is.EqualTo("2 (Kan Borcu, Kemik Kanatlar)"));

                RunSummaryRow[] enRows = RunSummaryModel.BuildRows(in summary, table, LanguageSetting.English);
                Assert.That(enRows[0].Label, Is.EqualTo("Floor"));
                Assert.That(enRows[4].Label, Is.EqualTo("Tower / Purgatory kills"));
                Assert.That(enRows[4].Value, Is.EqualTo("12 / 6"));
                Assert.That(enRows[7].Value, Is.EqualTo("Used"));
                Assert.That(enRows[8].Value, Is.EqualTo("2 (Blood Debt, Bone Wings)"));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void BuildRows_EmptyCardListFormatsAsEmDashAndReadyCharge()
        {
            var table = LocalizationHudTestContext.CreateTable();
            try
            {
                var summary = new RunSummary(
                    floorNumber: 1,
                    totalSeconds: 30f,
                    arafSeconds: 0f,
                    totalKills: 4,
                    towerKills: 4,
                    arafKills: 0,
                    criticalKills: 1,
                    gibKills: 0,
                    damageTaken: 0f,
                    arafChargeUsed: false,
                    cards: Array.Empty<CurseBlessingDefinition>());

                RunSummaryRow[] trRows = RunSummaryModel.BuildRows(in summary, table, LanguageSetting.Turkish);
                Assert.That(trRows[7].Value, Is.EqualTo("Haz\u0131r"));
                Assert.That(trRows[8].Value, Is.EqualTo("\u2014"));

                RunSummaryRow[] enRows = RunSummaryModel.BuildRows(in summary, table, LanguageSetting.English);
                Assert.That(enRows[7].Value, Is.EqualTo("Ready"));
                Assert.That(enRows[8].Value, Is.EqualTo("\u2014"));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }
    }
}
