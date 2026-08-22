using System;
using System.Collections.Generic;
using System.Text;
using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        [Header("Ícones do inspetor de cartas no editor")]
        [SerializeField] private Sprite deckEditorLevelIconTemplate;
        [SerializeField] private Sprite deckEditorAttackIconTemplate;
        [SerializeField] private Sprite deckEditorDefenseIconTemplate;
        [SerializeField]
        private List<Sprite> deckEditorAttributeIconTemplates = new();
        [SerializeField]
        private List<Sprite> deckEditorTypeIconTemplates = new();

        private Transform _deckEditorCombatInfoRoot;
        private Image _deckEditorDetailAttributeIcon;
        private Image _deckEditorDetailTypeIcon;
        private Image _deckEditorDetailLevelIcon;
        private Image _deckEditorDetailAttackIcon;
        private Image _deckEditorDetailDefenseIcon;
        private Text _deckEditorDetailLevel;
        private Text _deckEditorDetailAttack;
        private Text _deckEditorDetailDefense;

        private void BuildDeckEditorCombatInformation(Transform parent)
        {
            _deckEditorDetailAttributeIcon = CreatePanel(
                parent,
                "Atributo da carta no editor",
                new Vector2(0.865f, 0.925f),
                new Vector2(0.945f, 0.978f),
                Color.clear);
            ConfigureDeckEditorInfoIcon(_deckEditorDetailAttributeIcon);

            Image information = CreatePanel(
                parent,
                "Informações de combate do editor",
                new Vector2(0.605f, 0.56f),
                new Vector2(0.955f, 0.905f),
                new Color(0.006f, 0.025f, 0.035f, 0.92f));
            ApplyCapturedRectTransform(
                information.rectTransform,
                new Vector2(0.605f, 0.56f),
                new Vector2(0.955f, 0.905f),
                0f,
                25.278f,
                0f,
                -25.278f);
            information.raycastTarget = false;
            _deckEditorCombatInfoRoot = information.transform;

            _deckEditorDetailTypeIcon = CreatePanel(
                information.transform,
                "Tipo da carta no editor",
                new Vector2(0.05f, 0.73f),
                new Vector2(0.29f, 0.95f),
                Color.clear);
            ConfigureDeckEditorInfoIcon(_deckEditorDetailTypeIcon);
            _deckEditorDetailType = CreateText(
                information.transform,
                "TIPO DA CARTA",
                13,
                FontStyle.Bold,
                Gold,
                new Vector2(0.32f, 0.69f),
                new Vector2(0.96f, 0.96f),
                TextAnchor.MiddleLeft);
            ApplyCapturedRectTransform(
                _deckEditorDetailType.rectTransform,
                new Vector2(0.32f, 0.69f),
                new Vector2(0.96f, 0.96f),
                25.3f,
                2.9069f,
                -25.3f,
                -2.9069f,
                1.39f);

            _deckEditorDetailLevelIcon = CreatePanel(
                information.transform,
                "Nível da carta no editor",
                new Vector2(0.05f, 0.49f),
                new Vector2(0.29f, 0.68f),
                Color.clear);
            ApplyCapturedRectTransform(
                _deckEditorDetailLevelIcon.rectTransform,
                new Vector2(0.05f, 0.49f),
                new Vector2(0.29f, 0.68f),
                -8.7604f,
                -14.5349f,
                -20.7606f,
                -0.0001001358f);
            ConfigureDeckEditorInfoIcon(_deckEditorDetailLevelIcon);
            _deckEditorDetailLevel = CreateText(
                information.transform,
                "NÍVEL —",
                18,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.33f, 0.49f),
                new Vector2(0.68f, 0.68f),
                TextAnchor.MiddleLeft);
            ApplyCapturedRectTransform(
                _deckEditorDetailLevel.rectTransform,
                new Vector2(0.33f, 0.49f),
                new Vector2(0.68f, 0.68f),
                17.991f,
                -7.2674f,
                -17.991f,
                7.2674f,
                1.19f);

            _deckEditorDetailAttackIcon = CreatePanel(
                information.transform,
                "Ataque da carta no editor",
                new Vector2(0.05f, 0.255f),
                new Vector2(0.29f, 0.46f),
                Color.clear);
            ApplyCapturedRectTransform(
                _deckEditorDetailAttackIcon.rectTransform,
                new Vector2(0.05f, 0.255f),
                new Vector2(0.29f, 0.46f),
                -12.49505f,
                -25.60825f,
                -24.49505f,
                -0.0002498627f);
            ConfigureDeckEditorInfoIcon(_deckEditorDetailAttackIcon);
            _deckEditorDetailAttack = CreateText(
                information.transform,
                "—",
                19,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.33f, 0.255f),
                new Vector2(0.96f, 0.46f),
                TextAnchor.MiddleLeft);
            ApplyCapturedRectTransform(
                _deckEditorDetailAttack.rectTransform,
                new Vector2(0.33f, 0.255f),
                new Vector2(0.96f, 0.46f),
                14.9f,
                -14.624f,
                -14.9f,
                14.624f);

            _deckEditorDetailDefenseIcon = CreatePanel(
                information.transform,
                "Defesa da carta no editor",
                new Vector2(0.05f, 0.035f),
                new Vector2(0.29f, 0.24f),
                Color.clear);
            ApplyCapturedRectTransform(
                _deckEditorDetailDefenseIcon.rectTransform,
                new Vector2(0.05f, 0.035f),
                new Vector2(0.29f, 0.24f),
                -12.4949f,
                -17.0724f,
                -24.4951f,
                0f);
            ConfigureDeckEditorInfoIcon(_deckEditorDetailDefenseIcon);
            _deckEditorDetailDefense = CreateText(
                information.transform,
                "—",
                19,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.33f, 0.035f),
                new Vector2(0.96f, 0.24f),
                TextAnchor.MiddleLeft);
            ApplyCapturedRectTransform(
                _deckEditorDetailDefense.rectTransform,
                new Vector2(0.33f, 0.035f),
                new Vector2(0.96f, 0.24f),
                14.9f,
                -14.624f,
                -14.9f,
                14.624f);

            _deckEditorDetailStats = CreateText(
                information.transform,
                string.Empty,
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.48f),
                TextAnchor.MiddleCenter);
        }

        private void RefreshDeckEditorCombatInformation(
            CardCatalogEntry entry)
        {
            if (entry == null)
                return;

            bool monster = entry.Category == CardCategory.Monster;
            bool spell = entry.Category == CardCategory.Spell;
            bool trap = entry.Category == CardCategory.Trap;
            string attributeKey = spell
                ? "spell"
                : trap
                    ? "trap"
                    : entry.Attribute.ToString();
            SetDeckEditorInfoSprite(
                _deckEditorDetailAttributeIcon,
                FindDeckEditorTemplate(
                    deckEditorAttributeIconTemplates,
                    attributeKey,
                    false),
                monster || spell || trap);

            Sprite typeIcon = monster
                ? FindDeckEditorTemplate(
                    deckEditorTypeIconTemplates,
                    DeckEditorTypeTemplateKey(entry.RaceName),
                    true)
                : null;
            SetDeckEditorInfoSprite(
                _deckEditorDetailTypeIcon,
                typeIcon,
                monster && typeIcon != null);

            if (_deckEditorDetailType != null)
            {
                if (monster)
                {
                    _deckEditorDetailType.text =
                        $"{entry.RaceName}\n{entry.TypeName}";
                    _deckEditorDetailType.color = Gold;
                }
                else
                {
                    _deckEditorDetailType.text = spell
                        ? $"CARTA DE MAGIA\n{entry.TypeName}"
                        : trap
                            ? $"CARTA DE ARMADILHA\n{entry.TypeName}"
                            : entry.TypeName;
                    _deckEditorDetailType.color = trap
                        ? new Color(0.98f, 0.4f, 0.78f)
                        : Cyan;
                }
            }

            bool hasLevel = monster && entry.Level > 0;
            SetDeckEditorInfoSprite(
                _deckEditorDetailLevelIcon,
                deckEditorLevelIconTemplate,
                hasLevel);
            SetDeckEditorInfoText(
                _deckEditorDetailLevel,
                hasLevel,
                hasLevel ? entry.Level.ToString() : string.Empty);

            bool hasAttack = monster && entry.Attack >= 0;
            SetDeckEditorInfoSprite(
                _deckEditorDetailAttackIcon,
                deckEditorAttackIconTemplate,
                hasAttack);
            SetDeckEditorInfoText(
                _deckEditorDetailAttack,
                hasAttack,
                hasAttack ? FormatCardStat(entry.Attack) : string.Empty);

            bool hasDefense = monster &&
                              entry.Defense >= 0 &&
                              entry.MonsterFrame != MonsterFrameKind.Link;
            SetDeckEditorInfoSprite(
                _deckEditorDetailDefenseIcon,
                deckEditorDefenseIconTemplate,
                hasDefense);
            SetDeckEditorInfoText(
                _deckEditorDetailDefense,
                hasDefense,
                hasDefense ? FormatCardStat(entry.Defense) : string.Empty);

            if (_deckEditorDetailStats != null)
            {
                _deckEditorDetailStats.gameObject.SetActive(!monster);
                _deckEditorDetailStats.text = !monster
                    ? $"ID {DeckRepository.StableCardId(entry)}"
                    : string.Empty;
            }
        }

        private static void ConfigureDeckEditorInfoIcon(Image icon)
        {
            if (icon == null)
                return;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.gameObject.SetActive(false);
        }

        private static void SetDeckEditorInfoSprite(
            Image image,
            Sprite sprite,
            bool visible)
        {
            if (image == null)
                return;
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : Color.clear;
            image.gameObject.SetActive(visible && sprite != null);
        }

        private static void SetDeckEditorInfoText(
            Text text,
            bool visible,
            string value)
        {
            if (text == null)
                return;
            text.text = value ?? string.Empty;
            text.gameObject.SetActive(visible);
        }

        private static Sprite FindDeckEditorTemplate(
            IReadOnlyList<Sprite> templates,
            string key,
            bool typeTemplate)
        {
            if (templates == null || string.IsNullOrWhiteSpace(key))
                return null;

            string normalizedKey = NormalizeDeckEditorIconKey(key);
            string expectedTypeKey = $"type{normalizedKey}madu";
            foreach (Sprite sprite in templates)
            {
                if (sprite == null)
                    continue;
                string candidate = NormalizeDeckEditorIconKey(sprite.name);
                if (typeTemplate
                        ? candidate.Contains(expectedTypeKey)
                        : candidate.StartsWith(normalizedKey))
                {
                    return sprite;
                }
            }
            return null;
        }

        private static string DeckEditorTypeTemplateKey(string raceName)
        {
            return NormalizeDeckEditorIconKey(raceName) switch
            {
                "bestaguerreira" or "beastwarrior" => "beastwarrior",
                "bestaalada" or "wingedbeast" => "wingedbeast",
                "bestadivina" or "divinebeast" => "divinebeast",
                "deuscriador" or "creatorgod" => "creatorgod",
                "serpentemarinha" or "seaserpent" => "seaserpent",
                "mago" or "spellcaster" => "spellcaster",
                "guerreiro" or "warrior" => "warrior",
                "dragao" or "dragon" => "dragon",
                "demonio" or "fiend" => "fiend",
                "fada" or "fairy" => "fairy",
                "maquina" or "machine" => "machine",
                "trovao" or "thunder" => "thunder",
                "dinossauro" or "dinosaur" => "dinosaur",
                "ciberso" or "cyberse" => "cyberse",
                "zumbi" or "zombie" => "zombie",
                "besta" or "beast" => "beast",
                "peixe" or "fish" => "fish",
                "ilusao" or "illusion" => "illusion",
                "inseto" or "insect" => "insect",
                "planta" or "plant" => "plant",
                "psiquico" or "psychic" => "psychic",
                "piro" or "pyro" => "pyro",
                "reptil" or "reptile" => "reptile",
                "rocha" or "rock" => "rock",
                "wyrm" => "wyrm",
                _ => NormalizeDeckEditorIconKey(raceName)
            };
        }

        private static string NormalizeDeckEditorIconKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string normalized = value.Normalize(NormalizationForm.FormD);
            StringBuilder builder = new(normalized.Length);
            foreach (char character in normalized)
            {
                if (char.IsLetterOrDigit(character) &&
                    character < 128)
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }
            return builder.ToString();
        }
    }
}
