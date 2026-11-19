using System.Collections.Generic;
using NUnit.Framework;
using RetroFPS.Run.Cards;
using RetroFPS.Stats;
using UnityEditor;

namespace RetroFPS.Tests.EditMode
{
    /// <summary>Verifies the generated catalog against the ordered v2 card contract.</summary>
    public sealed class CurseCardCatalogTests
    {
        /// <summary>All fifteen cards expose the required identities, keys, rarities, modifiers and weights.</summary>
        [Test]
        public void BuiltCatalogMatchesAllFifteenCardsInContractOrder()
        {
            CardCatalog catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(
                "Assets/_Project/Data/Araf/Cards/CardCatalog_Default.asset");
            if (catalog == null) Assert.Ignore("Run RetroFPS/Build Curse Cards to create the catalog first.");
            string[] ids =
            {
                "blood_debt",
                "starving_steel",
                "last_breath",
                "bone_wings",
                "brimming_chalice",
                "glutton_barrel",
                "iron_hide",
                "carrion_crow",
                "pilgrims_flask",
                "gilded_vein",
                "executioners_oath",
                "bell_tower",
                "judges_scale",
                "hollow_saint",
                "red_hour"
            };
            CardRarity[] rarities =
            {
                CardRarity.Common,
                CardRarity.Rare,
                CardRarity.Rare,
                CardRarity.Common,
                CardRarity.Common,
                CardRarity.Common,
                CardRarity.Common,
                CardRarity.Common,
                CardRarity.Common,
                CardRarity.Rare,
                CardRarity.Rare,
                CardRarity.Rare,
                CardRarity.Relic,
                CardRarity.Relic,
                CardRarity.Relic
            };
            StatModifier[][] expected =
            {
                new[]
                {
                    new StatModifier(StatId.DamageDealt, StatOp.AddPercent, 0.3f),
                    new StatModifier(StatId.MaxHealth, StatOp.AddPercent, -0.25f)
                },
                new[]
                {
                    new StatModifier(StatId.MeleeDamage, StatOp.AddPercent, 0.45f),
                    new StatModifier(StatId.HarvestHeal, StatOp.Multiply, 1.75f),
                    new StatModifier(StatId.OrbDropChance, StatOp.Multiply, 0f)
                },
                new[]
                {
                    new StatModifier(StatId.DamageDealt, StatOp.AddPercent, 0.5f, StatCondition.HealthBelowHalf),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, 0.2f, StatCondition.HealthBelowHalf),
                    new StatModifier(StatId.HealReceived, StatOp.AddPercent, -0.35f)
                },
                new[]
                {
                    new StatModifier(StatId.DashRechargeTime, StatOp.Multiply, 0.8f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, 0.12f),
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, 0.25f)
                },
                new[]
                {
                    new StatModifier(StatId.OverhealCap, StatOp.AddFlat, 50f),
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, 0.35f, StatCondition.Overhealed),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, -0.15f, StatCondition.HealthBelowHalf)
                },
                new[]
                {
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, 0.4f),
                    new StatModifier(StatId.AmmoPickupAmount, StatOp.AddPercent, -0.5f)
                },
                new[]
                {
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, -0.2f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, -0.1f)
                },
                new[]
                {
                    new StatModifier(StatId.AmmoPickupAmount, StatOp.AddPercent, 0.75f),
                    new StatModifier(StatId.OrbDropChance, StatOp.Multiply, 0.75f)
                },
                new[]
                {
                    new StatModifier(StatId.HealReceived, StatOp.AddPercent, 0.35f),
                    new StatModifier(StatId.OrbDropChance, StatOp.Multiply, 1.5f),
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, -0.12f)
                },
                new[]
                {
                    new StatModifier(StatId.OverhealCap, StatOp.AddFlat, 50f),
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, -0.25f, StatCondition.Overhealed),
                    new StatModifier(StatId.MaxHealth, StatOp.AddPercent, -0.2f)
                },
                new[]
                {
                    new StatModifier(StatId.MeleeDamage, StatOp.AddPercent, 0.75f),
                    new StatModifier(StatId.DashRechargeTime, StatOp.Multiply, 0.8f),
                    new StatModifier(StatId.DamageDealt, StatOp.AddPercent, -0.2f)
                },
                new[]
                {
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, 0.5f),
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, -0.2f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, -0.15f),
                    new StatModifier(StatId.DashRechargeTime, StatOp.Multiply, 1.2f)
                },
                new[]
                {
                    new StatModifier(StatId.DamageDealt, StatOp.AddPercent, 0.6f),
                    new StatModifier(StatId.DamageTaken, StatOp.AddPercent, 0.4f)
                },
                new[]
                {
                    new StatModifier(StatId.DashRechargeTime, StatOp.Multiply, 0.625f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, 0.25f),
                    new StatModifier(StatId.MaxHealth, StatOp.AddPercent, -0.25f)
                },
                new[]
                {
                    new StatModifier(StatId.FireRate, StatOp.AddPercent, 0.5f),
                    new StatModifier(StatId.MeleeDamage, StatOp.AddPercent, 0.5f),
                    new StatModifier(StatId.MoveSpeed, StatOp.AddPercent, 0.15f),
                    new StatModifier(StatId.HealReceived, StatOp.AddPercent, -0.25f),
                    new StatModifier(StatId.OrbDropChance, StatOp.Multiply, 0.5f)
                }
            };
            Assert.That(catalog.Count, Is.EqualTo(15));
            Assert.That(catalog.GetWeight(CardRarity.Common), Is.EqualTo(10f));
            Assert.That(catalog.GetWeight(CardRarity.Rare), Is.EqualTo(5f));
            Assert.That(catalog.GetWeight(CardRarity.Relic), Is.EqualTo(1.5f));
            HashSet<string> unique = new HashSet<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                CurseBlessingDefinition card = catalog.Get(i);
                Assert.That(card, Is.Not.Null, ids[i]);
                Assert.That(card.Id, Is.EqualTo(ids[i]));
                Assert.That(card.Rarity, Is.EqualTo(rarities[i]), ids[i]);
                Assert.That(unique.Add(card.Id), Is.True, "Duplicate card id");
                string key = "hud.card." + ids[i] + ".";
                Assert.That(card.NameKey, Is.EqualTo(key + "name"));
                Assert.That(card.BoonKey, Is.EqualTo(key + "boon"));
                Assert.That(card.CurseKey, Is.EqualTo(key + "curse"));
                Assert.That(card.FlavorKey, Is.EqualTo(key + "flavor"));
                Assert.That(card.ModifierCount, Is.EqualTo(expected[i].Length), ids[i]);
                for (int j = 0; j < expected[i].Length; j++)
                {
                    StatModifier actual = card.GetModifier(j);
                    StatModifier wanted = expected[i][j];
                    string label = ids[i] + " modifier " + j;
                    Assert.That(actual.Stat, Is.EqualTo(wanted.Stat), label);
                    Assert.That(actual.Op, Is.EqualTo(wanted.Op), label);
                    Assert.That(actual.Value, Is.EqualTo(wanted.Value).Within(0.00001f), label);
                    Assert.That(actual.Condition, Is.EqualTo(wanted.Condition), label);
                }
            }
        }
    }
}
