using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class PublishedCardReconciliationEditModeTests
    {
        [Test]
        public void PublishedCatalogDocumentationVisualsAndArtAreReconciled()
        {
            UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<
                UnityEngine.Object>(
                "Assets/Cards/CardCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            IEnumerable entries = (IEnumerable)catalog.GetType()
                .GetProperty("Entries", BindingFlags.Public | BindingFlags.Instance)
                .GetValue(catalog);
            object[] entryObjects = entries.Cast<object>()
                .Where(entry => entry != null)
                .ToArray();
            string[] catalogIds = entryObjects
                .Select(entry => Normalize(Property<string>(
                    entry, "OfficialCardId")))
                .ToArray();
            Assert.That(catalogIds, Has.Length.EqualTo(969));
            Assert.That(catalogIds, Has.All.Not.Empty);
            Assert.That(catalogIds.Distinct().Count(), Is.EqualTo(catalogIds.Length));

            string[] documentationIds = File.ReadAllLines(
                    "Documentation/CardCatalog.csv")
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Split(',')[0])
                .Select(Normalize)
                .ToArray();
            Assert.That(documentationIds, Is.EquivalentTo(catalogIds));

            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            Assert.That(visuals.Count, Is.EqualTo(catalogIds.Length));
            foreach (object entry in entryObjects)
            {
                string officialCardId = Property<string>(
                    entry, "OfficialCardId");
                uint code = uint.Parse(officialCardId);
                Assert.That(visuals.TryGet(code, out CardVisualData visual),
                    Is.True, officialCardId);
                UnityEngine.Object artwork = Property<UnityEngine.Object>(
                    entry, "Artwork");
                Assert.That(artwork, Is.Not.Null, officialCardId);
                Assert.That(File.Exists(AssetDatabase.GetAssetPath(artwork)),
                    Is.True, officialCardId);
                Assert.That(File.Exists(visuals.ArtPath(code)),
                    Is.True, officialCardId + " " + visual.artFile);
            }
        }

        [Test]
        public void FirstBatchDossiersRemainPreparatoryAndCoverRequiredFamilies()
        {
            const string path =
                "Documentation/CardAudit/FirstBatchDossiers.json";
            Assert.That(File.Exists(path), Is.True);
            DossierFile payload = JsonUtility.FromJson<DossierFile>(
                File.ReadAllText(path));
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.batchSize, Is.InRange(25, 50));
            Assert.That(payload.cards, Has.Length.EqualTo(payload.batchSize));
            Assert.That(payload.cards.Select(card => card.officialCardId)
                .Distinct().Count(), Is.EqualTo(payload.batchSize));
            Assert.That(payload.cards.Count(card =>
                card.roleTags != null && card.roleTags.Contains("EXTRA_DECK")),
                Is.GreaterThanOrEqualTo(4));

            foreach (DossierCard card in payload.cards)
            {
                Assert.That(card.dossierStatus,
                    Is.EqualTo(
                        "PRONTO_PARA_REVISAO_SEMANTICA_E_IMPLEMENTACAO_DO_CENARIO"));
                Assert.That(card.currentResult.status, Is.EqualTo("CARREGA"));
                Assert.That(card.currentResult.core, Is.EqualTo("NAO_EXECUTADO"));
                string[] families = card.applicableScenarios
                    .Select(scenario => scenario.family)
                    .ToArray();
                Assert.That(families, Does.Contain("POSITIVO_MINIMO"));
                Assert.That(families, Does.Contain("NEGATIVO"));
                Assert.That(families, Does.Contain("FRONTEIRA"));
            }
        }

        private static string Normalize(string value)
        {
            return uint.TryParse(value, out uint code) && code != 0
                ? code.ToString("00000000")
                : string.Empty;
        }

        private static T Property<T>(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Property missing: " + name);
            return (T)property.GetValue(target);
        }

        [Serializable]
        private sealed class DossierFile
        {
            public int batchSize;
            public DossierCard[] cards;
        }

        [Serializable]
        private sealed class DossierCard
        {
            public string officialCardId;
            public string[] roleTags;
            public DossierScenario[] applicableScenarios;
            public DossierCurrentResult currentResult;
            public string dossierStatus;
        }

        [Serializable]
        private sealed class DossierScenario
        {
            public string family;
        }

        [Serializable]
        private sealed class DossierCurrentResult
        {
            public string status;
            public string core;
        }
    }
}
