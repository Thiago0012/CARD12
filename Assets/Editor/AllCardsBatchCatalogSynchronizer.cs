using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneDuel.DuelEngine.Data;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor
{
    /// <summary>
    /// Materializes streaming-art catalog entries after the deterministic
    /// Python importer has compiled the corresponding Core database.
    /// </summary>
    public static class AllCardsBatchCatalogSynchronizer
    {
        private const string CatalogPath =
            "Assets/Cards/CardCatalog.asset";
        private const string DefaultManifestPath =
            "Documentation/CardImports/AllCardsBatch001.json";

        [MenuItem("Arcane Arena/Content/Sync AllCards Numeric Batch 001")]
        public static void SyncBatch001()
        {
            Sync(DefaultManifestPath);
        }

        public static void SyncFromCommandLine()
        {
            string manifest = CommandLineValue("-allCardsManifest") ??
                              DefaultManifestPath;
            Sync(manifest);
        }

        private static void Sync(string manifestPath)
        {
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException(manifestPath);
            CardCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
                throw new FileNotFoundException(CatalogPath);
            CardDatabase database = CardDatabase.LoadDefault();
            JObject manifest = JObject.Parse(
                File.ReadAllText(manifestPath));
            var requested = new HashSet<uint>();
            int added = 0;
            int refreshed = 0;
            int tokens = 0;

            foreach (JToken item in manifest["entries"] ?? new JArray())
            {
                if (item.Value<bool?>("catalogEligible") != true)
                    continue;
                uint code = item.Value<uint>("imageId");
                requested.Add(code);
                CardCatalogEntry entry = Find(catalog, code);
                if (entry == null)
                {
                    entry = catalog.GetOrCreate(
                        "streaming:" + code.ToString("00000000"),
                        null);
                    added++;
                }
                else
                {
                    refreshed++;
                }
                CardRecord card = database.Get(code);
                entry.ApplyCoreMetadata(card);
                entry.SetRuntimeArtworkAvailable(true);
                entry.SetRarityVariant(ParseVariant(
                    item.Value<string>("artVariant")));
                if (entry.MonsterFrame == MonsterFrameKind.Token)
                    tokens++;
            }

            foreach (JToken dependency in
                     manifest["dependencies"] ?? new JArray())
            {
                if (dependency.Value<bool?>("imageAvailable") != true)
                    continue;
                uint code = dependency.Value<uint>("officialCode");
                requested.Add(code);
                CardCatalogEntry entry = Find(catalog, code);
                if (entry == null)
                {
                    entry = catalog.GetOrCreate(
                        "streaming:dependency:" + code.ToString("00000000"),
                        null);
                    added++;
                }
                else
                {
                    refreshed++;
                }
                entry.ApplyCoreMetadata(database.Get(code));
                entry.SetRuntimeArtworkAvailable(true);
                entry.SetRarityVariant(CardArtVariant.Base);
                if (entry.MonsterFrame == MonsterFrameKind.Token)
                    tokens++;
            }

            string[] duplicateCodes = catalog.Entries
                .Where(entry => entry != null &&
                                !string.IsNullOrWhiteSpace(entry.OfficialCardId))
                .GroupBy(entry => Normalize(entry.OfficialCardId),
                    StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (duplicateCodes.Length > 0)
            {
                throw new InvalidDataException(
                    "CardCatalog contains duplicate official IDs: " +
                    string.Join(", ", duplicateCodes));
            }
            uint[] missing = requested
                .Where(code => Find(catalog, code) == null)
                .OrderBy(code => code)
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidDataException(
                    "AllCards entries missing after synchronization: " +
                    string.Join(", ", missing));
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"ARCANE_ALLCARDS_CATALOG_SYNC_OK requested={requested.Count} " +
                $"added={added} refreshed={refreshed} tokens={tokens} " +
                $"catalog={catalog.Entries.Count}");
        }

        private static CardCatalogEntry Find(
            CardCatalog catalog,
            uint code)
        {
            return catalog.FindByOfficialId(code.ToString("00000000")) ??
                   catalog.FindByOfficialId(code.ToString());
        }

        private static CardArtVariant ParseVariant(string value)
        {
            return Enum.TryParse(
                value,
                true,
                out CardArtVariant variant)
                ? variant
                : CardArtVariant.Auto;
        }

        private static string Normalize(string value)
        {
            return uint.TryParse(value, out uint code)
                ? code.ToString("00000000")
                : value ?? string.Empty;
        }

        private static string CommandLineValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(
                        arguments[index],
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }
            return null;
        }
    }
}
