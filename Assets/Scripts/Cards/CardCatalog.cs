using System;
using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Data;
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

        public void ApplyCoreMetadata(CardRecord card)
        {
            if (card == null)
                return;

            officialCardId = card.Code.ToString("00000000");
            displayName = card.Name ?? officialCardId;
            category = CategoryFrom(card.Type);
            monsterFrame = FrameFrom(card.Type);
            typeName = TypeNameFrom(card.Type, monsterFrame);
            raceName = category == CardCategory.Monster
                ? RaceNameFrom(card.Race)
                : string.Empty;
            attribute = AttributeFrom(card.Attribute);
            level = category == CardCategory.Monster
                ? Math.Abs(card.Level)
                : 0;
            attack = category == CardCategory.Monster ? card.Attack : -1;
            defense = category == CardCategory.Monster ? card.Defense : -1;
            effectText = card.Description ?? string.Empty;
            effectId = CardEffectId.None;
            officiallyRegistered = true;
            classificationConfidence = 1f;
            needsManualReview = false;
            reviewNotes = "Metadados de apresentação sincronizados do catálogo compilado do Core.";
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

        private static CardCategory CategoryFrom(uint type)
        {
            if ((type & 0x1U) != 0) return CardCategory.Monster;
            if ((type & 0x2U) != 0) return CardCategory.Spell;
            if ((type & 0x4U) != 0) return CardCategory.Trap;
            return CardCategory.Unknown;
        }

        private static MonsterFrameKind FrameFrom(uint type)
        {
            if ((type & 0x4000000U) != 0) return MonsterFrameKind.Link;
            if ((type & 0x800000U) != 0) return MonsterFrameKind.Xyz;
            if ((type & 0x2000U) != 0) return MonsterFrameKind.Synchro;
            if ((type & 0x40U) != 0) return MonsterFrameKind.Fusion;
            if ((type & 0x80U) != 0) return MonsterFrameKind.Ritual;
            if ((type & 0x1000000U) != 0) return MonsterFrameKind.Pendulum;
            if ((type & 0x10U) != 0) return MonsterFrameKind.Normal;
            return MonsterFrameKind.Effect;
        }

        private static string TypeNameFrom(
            uint type,
            MonsterFrameKind frame)
        {
            if ((type & 0x2U) != 0) return "Carta de Magia";
            if ((type & 0x4U) != 0) return "Carta de Armadilha";
            if ((type & 0x200000U) != 0)
                return "Monstro de Efeito de Virar";
            return frame switch
            {
                MonsterFrameKind.Normal => "Monstro Normal",
                MonsterFrameKind.Ritual => "Monstro de Ritual",
                MonsterFrameKind.Fusion => "Monstro de Fusão",
                MonsterFrameKind.Synchro => "Monstro Sincro",
                MonsterFrameKind.Xyz => "Monstro Xyz",
                MonsterFrameKind.Link => "Monstro Link",
                MonsterFrameKind.Pendulum => "Monstro Pêndulo",
                _ => "Monstro de Efeito"
            };
        }

        private static CardAttribute AttributeFrom(uint attribute)
        {
            if ((attribute & 0x20U) != 0) return CardAttribute.Dark;
            if ((attribute & 0x10U) != 0) return CardAttribute.Light;
            if ((attribute & 0x08U) != 0) return CardAttribute.Wind;
            if ((attribute & 0x04U) != 0) return CardAttribute.Fire;
            if ((attribute & 0x02U) != 0) return CardAttribute.Water;
            if ((attribute & 0x01U) != 0) return CardAttribute.Earth;
            if ((attribute & 0x40U) != 0) return CardAttribute.Divine;
            return CardAttribute.None;
        }

        private static string RaceNameFrom(ulong race)
        {
            return race switch
            {
                1 => "Guerreiro",
                2 => "Mago",
                4 => "Fada",
                8 => "Demônio",
                16 => "Zumbi",
                32 => "Máquina",
                64 => "Aqua",
                128 => "Piro",
                256 => "Rocha",
                512 => "Besta Alada",
                1024 => "Planta",
                2048 => "Inseto",
                4096 => "Trovão",
                8192 => "Dragão",
                16384 => "Besta",
                32768 => "Besta-Guerreira",
                65536 => "Dinossauro",
                131072 => "Peixe",
                262144 => "Serpente Marinha",
                524288 => "Réptil",
                1048576 => "Psíquico",
                2097152 => "Besta Divina",
                4194304 => "Deus Criador",
                8388608 => "Wyrm",
                16777216 => "Ciberso",
                33554432 => "Ilusão",
                _ => "Monstro"
            };
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
