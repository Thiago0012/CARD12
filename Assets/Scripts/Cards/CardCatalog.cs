using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneArena.Cards
{
    public enum CardCategory
    {
        Unknown = 0,
        Monster = 1,
        Spell = 2,
        Trap = 3
    }

    public enum MonsterFrameKind
    {
        None = 0,
        Unknown = 1,
        Normal = 2,
        Effect = 3,
        Ritual = 4,
        Fusion = 5,
        Synchro = 6,
        Xyz = 7,
        Link = 8,
        Pendulum = 9,
        Token = 10
    }

    [Serializable]
    public sealed class CardCatalogEntry
    {
        [SerializeField] private string stableId;
        [SerializeField] private Sprite artwork;
        [SerializeField] private string displayName;
        [SerializeField] private CardCategory category;
        [SerializeField] private MonsterFrameKind monsterFrame;
        [SerializeField] private string officialCardId;
        [SerializeField] private string typeName;
        [SerializeField] private string raceName;
        [SerializeField] private CardAttribute attribute;
        [SerializeField] private int level;
        [SerializeField] private int attack = -1;
        [SerializeField] private int defense = -1;
        [SerializeField] private CardEffectId effectId;
        [SerializeField] private bool officiallyRegistered;
        [SerializeField, Range(0f, 1f)] private float classificationConfidence;
        [SerializeField] private bool needsManualReview;
        [SerializeField] private bool manuallyConfirmed;
        [SerializeField, TextArea(3, 10)] private string effectText;
        [SerializeField, TextArea(2, 6)] private string reviewNotes;

        public string StableId => stableId;
        public Sprite Artwork => artwork;
        public string DisplayName => displayName;
        public CardCategory Category => category;
        public MonsterFrameKind MonsterFrame => monsterFrame;
        public string OfficialCardId => officialCardId;
        public string TypeName => typeName;
        public string RaceName => raceName;
        public CardAttribute Attribute => attribute;
        public int Level => level;
        public int Attack => attack;
        public int Defense => defense;
        public CardEffectId EffectId => effectId;
        public bool OfficiallyRegistered => officiallyRegistered;
        public bool HasCombatStats =>
            category == CardCategory.Monster && attack >= 0 && defense >= 0;
        public float ClassificationConfidence => classificationConfidence;
        public bool NeedsManualReview => needsManualReview;
        public bool ManuallyConfirmed => manuallyConfirmed;
        public string EffectText => effectText;
        public string ReviewNotes => reviewNotes;
        public bool IsReadyForGameplay => category != CardCategory.Unknown && !needsManualReview;

        public CardCatalogEntry(string id, Sprite sprite)
        {
            stableId = id;
            artwork = sprite;
            displayName = FriendlyName(sprite != null ? sprite.name : id);
            category = CardCategory.Unknown;
            monsterFrame = MonsterFrameKind.None;
            officialCardId = string.Empty;
            typeName = string.Empty;
            raceName = string.Empty;
            attribute = CardAttribute.None;
            level = 0;
            attack = -1;
            defense = -1;
            effectId = CardEffectId.None;
            officiallyRegistered = false;
            classificationConfidence = 0f;
            needsManualReview = true;
            manuallyConfirmed = false;
            effectText = string.Empty;
            reviewNotes = string.Empty;
        }

        public void RefreshAssetReference(Sprite sprite)
        {
            artwork = sprite;
            if (string.IsNullOrWhiteSpace(displayName) && sprite != null)
                displayName = FriendlyName(sprite.name);
        }

        public void RefreshGeneratedDisplayName(string assetName)
        {
            if (!manuallyConfirmed && !officiallyRegistered && !string.IsNullOrWhiteSpace(assetName))
                displayName = FriendlyName(assetName);
        }

        public void ApplyOfficialMetadata(CardMetadata metadata)
        {
            if (metadata == null)
                return;

            officialCardId = metadata.OfficialId;
            displayName = metadata.LocalizedName;
            category = metadata.Category;
            monsterFrame = metadata.MonsterFrame;
            typeName = metadata.TypeName;
            raceName = metadata.RaceName;
            attribute = metadata.Attribute;
            level = metadata.Level;
            attack = metadata.Attack;
            defense = metadata.Defense;
            effectText = metadata.Description;
            effectId = metadata.EffectId;
            officiallyRegistered = true;
            classificationConfidence = 1f;
            needsManualReview = false;
            reviewNotes = "Dados oficiais cadastrados localmente pelo código da carta.";
        }

        public void ApplyAutomaticClassification(
            CardCategory detectedCategory,
            MonsterFrameKind detectedFrame,
            float confidence,
            string notes)
        {
            if (manuallyConfirmed)
                return;

            officiallyRegistered = false;
            category = detectedCategory;
            monsterFrame = detectedCategory == CardCategory.Monster
                ? detectedFrame
                : MonsterFrameKind.None;
            classificationConfidence = Mathf.Clamp01(confidence);
            needsManualReview =
                detectedCategory == CardCategory.Unknown ||
                classificationConfidence < 0.72f ||
                (detectedCategory == CardCategory.Monster &&
                 detectedFrame == MonsterFrameKind.Unknown);
            reviewNotes = notes ?? string.Empty;
        }

        public void ApplyManualIdentification(
            string name,
            CardCategory selectedCategory,
            MonsterFrameKind selectedFrame,
            string selectedOfficialId,
            string selectedTypeName,
            string selectedRaceName,
            CardAttribute selectedAttribute,
            int selectedLevel,
            int selectedAttack,
            int selectedDefense,
            CardEffectId selectedEffectId,
            string parsedEffect,
            string notes)
        {
            displayName = string.IsNullOrWhiteSpace(name)
                ? FriendlyName(artwork != null ? artwork.name : stableId)
                : name.Trim();
            category = selectedCategory;
            monsterFrame = selectedCategory == CardCategory.Monster
                ? selectedFrame
                : MonsterFrameKind.None;
            officialCardId = selectedOfficialId?.Trim() ?? string.Empty;
            typeName = selectedTypeName?.Trim() ?? string.Empty;
            raceName = selectedCategory == CardCategory.Monster
                ? selectedRaceName?.Trim() ?? string.Empty
                : string.Empty;
            attribute = selectedCategory == CardCategory.Monster
                ? selectedAttribute
                : CardAttribute.None;
            level = selectedCategory == CardCategory.Monster
                ? Mathf.Max(0, selectedLevel)
                : 0;
            attack = selectedCategory == CardCategory.Monster
                ? Mathf.Max(0, selectedAttack)
                : -1;
            defense = selectedCategory == CardCategory.Monster
                ? Mathf.Max(0, selectedDefense)
                : -1;
            effectId = selectedEffectId;
            effectText = parsedEffect ?? string.Empty;
            reviewNotes = notes ?? string.Empty;
            officiallyRegistered = false;
            manuallyConfirmed = selectedCategory != CardCategory.Unknown;
            needsManualReview = !manuallyConfirmed;
            classificationConfidence = manuallyConfirmed ? 1f : classificationConfidence;
        }

        public void ReturnToAutomaticReview()
        {
            manuallyConfirmed = false;
            needsManualReview = true;
        }

        private static string FriendlyName(string assetName)
        {
            return string.IsNullOrWhiteSpace(assetName)
                ? "Carta sem nome"
                : assetName.Replace('_', ' ').Trim();
        }
    }

    [CreateAssetMenu(fileName = "CardCatalog", menuName = "Card Game/Card Catalog")]
    public sealed class CardCatalog : ScriptableObject
    {
        [SerializeField] private List<CardCatalogEntry> entries = new();

        public IReadOnlyList<CardCatalogEntry> Entries => entries;

        public CardCatalogEntry FindByStableId(string stableId)
        {
            return entries.Find(entry =>
                entry != null &&
                string.Equals(entry.StableId, stableId, StringComparison.Ordinal));
        }

        public CardCatalogEntry FindBySprite(Sprite sprite)
        {
            return sprite == null
                ? null
                : entries.Find(entry => entry != null && entry.Artwork == sprite);
        }

        public CardCatalogEntry FindByOfficialId(string officialCardId)
        {
            return string.IsNullOrWhiteSpace(officialCardId)
                ? null
                : entries.Find(entry =>
                    entry != null &&
                    string.Equals(
                        entry.OfficialCardId,
                        officialCardId,
                        StringComparison.Ordinal));
        }

        public CardCategory CategoryFor(Sprite sprite)
        {
            var entry = FindBySprite(sprite);
            return entry != null && entry.IsReadyForGameplay
                ? entry.Category
                : CardCategory.Unknown;
        }

        public CardCatalogEntry GetOrCreate(string stableId, Sprite sprite)
        {
            var entry = FindByStableId(stableId);
            if (entry != null)
            {
                entry.RefreshAssetReference(sprite);
                return entry;
            }

            entry = new CardCatalogEntry(stableId, sprite);
            entries.Add(entry);
            return entry;
        }

        public void AddEntry(CardCatalogEntry entry)
        {
            if (entry != null && !entries.Contains(entry))
            {
                entries.Add(entry);
            }
        }

        public void RemoveMissingEntries(HashSet<string> existingIds)
        {
            entries.RemoveAll(entry =>
                entry == null ||
                !existingIds.Contains(entry.StableId));
        }
    }
}
