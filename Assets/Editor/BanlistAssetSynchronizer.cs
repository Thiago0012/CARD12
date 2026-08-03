using System.IO;
using ArcaneDuel.Game;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor
{
    public static class BanlistAssetSynchronizer
    {
        private const string SeedPath =
            "Assets/Resources/Banlist/tcg_eu_2026_05_18.json";
        private const string AssetPath =
            "Assets/Resources/Banlist/tcg_eu_2026_05_18.asset";
        private const string ForbiddenPath =
            "Assets/Templates/BanListIcon/forbiden.png";
        private const string LimitedPath =
            "Assets/Templates/BanListIcon/1.png";
        private const string SemiLimitedPath =
            "Assets/Templates/BanListIcon/2.png";

        [MenuItem("Arcane Arena/Content/Sync Active Banlist")]
        public static void SyncActiveBanlist()
        {
            if (!File.Exists(SeedPath))
                throw new FileNotFoundException(SeedPath);

            BanlistSeedFile seed = JsonUtility.FromJson<BanlistSeedFile>(
                File.ReadAllText(SeedPath));
            if (seed == null || seed.schemaVersion != 1 ||
                seed.id != BanlistService.ActiveBanlistId ||
                seed.entries == null || seed.entries.Count != 226)
            {
                throw new InvalidDataException(
                    "A seed da banlist ativa está inválida ou incompleta.");
            }

            BanlistDefinition definition =
                AssetDatabase.LoadAssetAtPath<BanlistDefinition>(AssetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BanlistDefinition>();
                AssetDatabase.CreateAsset(definition, AssetPath);
            }

            definition.Initialize(
                seed,
                LoadSprite(ForbiddenPath),
                LoadSprite(LimitedPath),
                LoadSprite(SemiLimitedPath));
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"ARCANE_BANLIST_SYNC_OK id={definition.Id} " +
                $"entries={definition.Entries.Count} hash={definition.SourceSha256}");
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new FileNotFoundException(path);
            return sprite;
        }
    }
}
