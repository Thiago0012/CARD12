using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class CardInventoryAuditEditModeTests
    {
        private const string AuditTypeName =
            "ArcaneArena.Editor.CardAudit.CardInventoryAudit";

        [Test]
        public void PhaseOneSnapshotInventoriesPublishedCatalogWithoutFalseApproval()
        {
            object snapshot = BuildSnapshot();
            Array cards = Field<Array>(snapshot, "cards");
            Assert.That(cards, Is.Not.Null);
            Assert.That(cards.Length, Is.GreaterThanOrEqualTo(900));

            string[] officialIds = cards.Cast<object>()
                .Select(card => Field<string>(card, "officialCardId"))
                .Where(value => !string.IsNullOrEmpty(value))
                .ToArray();
            Assert.That(officialIds.Distinct().Count(), Is.EqualTo(officialIds.Length),
                "A matriz deve ter uma linha canonica por OfficialCardId.");
            Assert.That(cards.Cast<object>().Count(card =>
                    Field<string>(card, "status") == "CONCLUIDA"),
                Is.Zero,
                "A Fase 1 nao pode declarar carta concluida sem cenarios semanticos.");
        }

        [Test]
        public void FirstBatchHasReviewableSizeAndOnlyInventoriedCards()
        {
            object snapshot = BuildSnapshot();
            Array cards = Field<Array>(snapshot, "cards");
            Array batch = Field<Array>(snapshot, "firstBatch");
            Assert.That(batch.Length, Is.InRange(25, 50));

            string[] matrixIds = cards.Cast<object>()
                .Select(card => Field<string>(card, "officialCardId"))
                .ToArray();
            foreach (object seed in batch)
            {
                string id = Field<string>(seed, "officialCardId");
                Assert.That(matrixIds, Does.Contain(id),
                    "O primeiro lote deve ser subconjunto da matriz.");
            }
        }

        [Test]
        public void SnapshotRecordsAllRequiredReproducibilityHashes()
        {
            object snapshot = BuildSnapshot();
            object sources = Field<object>(snapshot, "sources");
            foreach (string name in new[]
                     {
                         "cardCatalogSha256", "documentationCsvSha256",
                         "coreDocumentationSha256", "cardsBinSha256",
                         "cardTextsSha256", "visualManifestSha256",
                         "officialScriptsTreeSha256", "windowsCorePluginSha256",
                         "androidCorePluginSha256"
                     })
            {
                Assert.That(Field<string>(sources, name), Is.Not.Empty,
                    name + " deve fazer parte do baseline reproduzivel.");
            }
        }

        private static object BuildSnapshot()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(AuditTypeName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null,
                "A ferramenta Editor-only de auditoria deve estar compilada.");
            MethodInfo method = type.GetMethod("BuildSnapshot",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, null);
        }

        private static T Field<T>(object target, string name)
        {
            Assert.That(target, Is.Not.Null);
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Campo ausente: " + name);
            return (T)field.GetValue(target);
        }
    }
}
