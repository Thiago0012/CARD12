using System.IO;
using ArcaneDuel.DuelEngine.Data;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class CardDatabaseEditModeTests
    {
        private static readonly uint[] VerticalSlice =
        {
            89631139, 46986414, 74131780, 71413901, 7089711, 93920745,
            97268402, 53129443, 5318639, 44095762, 11901678, 77585513
        };

        private static readonly uint[] LegacyPresentationCompatibility =
        {
            2314238, 2857636, 4335645, 7198399, 7922915, 9287078,
            10515412, 11443677, 13756293, 14315573, 15960641, 16699558,
            17725109, 17947697, 20747792, 24382602, 33698022, 34318086,
            34755994, 36262024, 38120068, 38342335, 39030163, 43219114,
            43321985, 43892408, 46411259, 47963370, 51632798, 52684508,
            54332792, 54693926, 56132807, 56532353, 60948488, 63391643,
            63519819, 65741786, 68462976, 71044499, 75190122, 77754944,
            80088625, 80326401, 83011278, 83555666, 85442146, 86331741,
            88177324, 88240808, 89604813, 93437091, 94259633, 95477924,
            96561011, 96729612, 98502113
        };

        [Test]
        public void CompiledCatalogContainsAuthoredCardsAndAllCoreAliases()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            Assert.That(database.Count, Is.GreaterThanOrEqualTo(257));
            Assert.That(database.TryGet(83011277, out _), Is.True,
                "Mystic Tomato canonical alias must be available to ocgcore.");
            foreach (CardRecord compiled in database.Cards)
            {
                if (compiled.Alias == 0) continue;
                Assert.That(database.TryGet(compiled.Alias, out _), Is.True,
                    $"Alias {compiled.Alias:00000000} required by {compiled.Code:00000000} is missing.");
            }
            foreach (uint code in VerticalSlice)
            {
                CardRecord card = database.Get(code);
                Assert.That(card.Name, Is.Not.Empty, code.ToString());
                Assert.That(File.Exists(Path.Combine(Application.streamingAssetsPath, "Ygo", "Art", code + ".jpg")),
                    Is.True,
                    $"Featured art is missing for {code}.");
                bool normalMonster = code == 89631139 || code == 46986414;
                if (!normalMonster)
                {
                    Assert.That(
                        File.Exists(Path.Combine(
                            Application.streamingAssetsPath,
                            "Ygo",
                            "Scripts",
                            "official",
                            "c" + code + ".lua")),
                        Is.True,
                        $"Featured effect script is missing for {code}.");
                }
            }

            foreach (uint code in LegacyPresentationCompatibility)
            {
                Assert.That(database.TryGet(code, out CardRecord card), Is.True, code.ToString());
                Assert.That(card.Name, Is.Not.Empty, code.ToString());
            }
        }

        [Test]
        public void CoinDragonEffectIsFullyLocalizedInPortuguese()
        {
            CardRecord coinDragon = CardDatabase.LoadDefault().Get(9000988);
            Assert.That(coinDragon.Name,
                Is.EqualTo("Camarartista Dragão Moeda"));
            Assert.That(coinDragon.Description,
                Does.Contain("[ Efeito de Pêndulo ]"));
            Assert.That(coinDragon.Description,
                Does.Not.Contain("Once per turn"));
        }

        [Test]
        public void PendulumDescriptionSeparatesItsSpellAndMonsterSections()
        {
            CardRecord card = CardDatabase.LoadDefault().Get(41546);
            Assert.That(card.Description,
                Does.Contain("[ Efeito de Pêndulo ]"));
            Assert.That(card.Description,
                Does.Contain("[ Efeito de Monstro ]"));
            Assert.That(card.Description,
                Does.Not.Contain("em revisão"));
        }

        [Test]
        public void EntireCatalogIsPresentedWithoutTranslationReviewPlaceholders()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            foreach (CardRecord card in database.Cards)
            {
                Assert.That(
                    LooksLikeUntranslatedEnglish(card.Description),
                    Is.False,
                    $"{card.Code} · {card.Name}");
            }
        }

        [Test]
        public void EveryPendulumCardSeparatesSpellAndMonsterText()
        {
            const uint pendulumType = 0x01000000U;
            CardDatabase database = CardDatabase.LoadDefault();
            foreach (CardRecord card in database.Cards)
            {
                if ((card.Type & pendulumType) == 0U)
                    continue;

                Assert.That(
                    card.Description,
                    Does.Contain("[ Efeito de Pêndulo ]"),
                    $"{card.Code} · {card.Name}");
                Assert.That(
                    card.Description,
                    Does.Contain("[ Efeito de Monstro ]"),
                    $"{card.Code} · {card.Name}");
            }
        }

        private static bool LooksLikeUntranslatedEnglish(string value)
        {
            string normalized = " " + (value ?? string.Empty)
                .ToLowerInvariant()
                .Replace('\r', ' ')
                .Replace('\n', ' ') + " ";
            string[] markers =
            {
                " once per ", " you can ", " your opponent ",
                " this card ", " this turn ", " target 1 ",
                " special summon", " normal summon", " from your ",
                " to your hand", " on the field", " in your graveyard",
                " destroy that", " banish that", " when this ",
                " if this ", " during your "
            };
            foreach (string marker in markers)
            {
                if (normalized.Contains(marker))
                    return true;
            }
            return false;
        }
    }
}
