#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Editor
{
    public static class DuelCardPlacementInstaller
    {
        private const string ScenePath = "Assets/Scenes/DuelArena.unity";
        private const string PreviewName =
            "LAYOUT EDITAVEL DA ARENA (SOMENTE EDICAO)";
        private const string PreviewImageName =
            "IMAGEM DA ARENA (ARRASTE SEU PNG AQUI)";
        private const string FieldTexturePath =
            "Assets/Templates/Field/field1.png";
        private const string CardBackPath =
            "Assets/Cards/Background/" +
            "dal6wsb-fc4aaba4-d6ff-4029-a83f-9b518abd511d.png";
        private const string SampleCardPath =
            "Assets/Cards/Cards/Monstros/Normais/89631139.jpg";
        private const float FieldWidth = 18.8f;
        private const float FieldDepth = 10.586f;

        [MenuItem("Card Game/Duelo/Preparar layout completo editavel")]
        [MenuItem("Card Game/Duelo/Preparar posicoes editaveis das cartas")]
        public static void Install()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            DuelZone3D[] zones = FindArenaZones();
            if (zones.Length == 0)
                throw new InvalidOperationException(
                    "Nenhuma zona da arena foi encontrada.");

            foreach (DuelZone3D zone in zones)
            {
                zone.EnsurePresentationAnchors();
                EditorUtility.SetDirty(zone);
            }

            RectTransform frame = FindUniversalFrame();
            PrepareHandAnchors(frame);
            Texture2D field =
                AssetDatabase.LoadAssetAtPath<Texture2D>(FieldTexturePath);
            PrepareScenePreview(frame, zones, field);
            RefreshRegistry();
            Save(scene);
            Debug.Log(
                $"ARCANE_DUEL_AUTHORING_LAYOUT=READY; zones={zones.Length}; " +
                "a previa e EditorOnly e nao entra no build.");
        }

        [MenuItem("Card Game/Duelo/Aplicar posicoes editadas ao duelo")]
        public static void ApplyPreviewPositions()
        {
            Scene scene = EnsureDuelScene();
            RectTransform frame = FindUniversalFrame();
            DuelSceneZonePreview[] handles = FindPreviewHandles();
            NormalizeMainMonsterPreviewSizes(handles);
            int applied = 0;
            foreach (DuelSceneZonePreview handle in handles)
            {
                if (ApplyHandle(frame, handle))
                    applied++;
            }
            ApplyHandPreviews(frame);

            RefreshRegistry();
            Save(scene);
            Debug.Log(
                $"ARCANE_DUEL_AUTHORING_POSITIONS_APPLIED={applied}; " +
                "identidades e regras das zonas foram preservadas.");
        }

        private static void NormalizeMainMonsterPreviewSizes(
            IReadOnlyList<DuelSceneZonePreview> handles)
        {
            if (handles == null)
                return;
            foreach (DuelPlayerSide owner in new[]
                     {
                         DuelPlayerSide.PlayerOne,
                         DuelPlayerSide.PlayerTwo
                     })
            {
                DuelSceneZonePreview reference = handles.FirstOrDefault(
                    handle =>
                        handle != null &&
                        handle.TargetZone != null &&
                        handle.TargetZone.Owner == owner &&
                        handle.TargetZone.Kind == DuelZoneKind.Monster &&
                        handle.TargetZone.ZoneIndex == 2);
                if (reference == null ||
                    !(reference.transform is RectTransform referenceRect))
                {
                    continue;
                }

                foreach (DuelSceneZonePreview handle in handles)
                {
                    if (handle == null ||
                        handle.TargetZone == null ||
                        handle.TargetZone.Owner != owner ||
                        handle.TargetZone.Kind != DuelZoneKind.Monster ||
                        handle.TargetZone.ZoneIndex < 0 ||
                        handle.TargetZone.ZoneIndex > 4 ||
                        !(handle.transform is RectTransform rect))
                    {
                        continue;
                    }
                    rect.sizeDelta = referenceRect.sizeDelta;
                    rect.localScale = referenceRect.localScale;
                    EditorUtility.SetDirty(rect);
                }
            }
        }

        [MenuItem("Card Game/Duelo/Aplicar imagem escolhida a arena")]
        public static void ApplySelectedArenaImage()
        {
            Scene scene = EnsureDuelScene();
            RectTransform frame = FindUniversalFrame();
            Transform preview = frame.Find(PreviewName);
            RawImage image = preview != null
                ? preview.Find(PreviewImageName)?.GetComponent<RawImage>()
                : null;
            Texture2D texture = image != null
                ? image.texture as Texture2D
                : null;
            if (texture == null)
            {
                throw new InvalidOperationException(
                    "Arraste um PNG para o campo Texture do objeto '" +
                    PreviewImageName + "' antes de aplicar.");
            }

            List<HandleSnapshot> snapshots = CaptureHandles(frame);
            MasterDuelArena3D arena = FindMasterArena();
            if (arena == null)
                throw new InvalidOperationException(
                    "MasterDuelArena3D nao foi encontrado.");

            arena.ConfigureStaticField(texture, texture);
            arena.Rebuild();
            EditorUtility.SetDirty(arena);
            DuelZone3D[] zones = arena
                .GetComponentsInChildren<DuelZone3D>(true);
            RestoreSnapshots(frame, zones, snapshots);
            PrepareScenePreview(frame, zones, texture);
            RefreshRegistry();
            Save(scene);
            Debug.Log(
                $"ARCANE_DUEL_ARENA_IMAGE_APPLIED={texture.name}; " +
                $"zones={zones.Length}; layout manual preservado.");
        }

        private static void PrepareScenePreview(
            RectTransform frame,
            DuelZone3D[] zones,
            Texture2D field)
        {
            Transform old = frame.Find(PreviewName);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);

            var rootObject = new GameObject(
                PreviewName,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(DuelSceneAuthoringPreview));
            rootObject.tag = "EditorOnly";
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(frame, false);
            Stretch(root);
            root.SetAsFirstSibling();

            RawImage background = CreateRawImage(root, PreviewImageName);
            background.texture = field;
            background.color = Color.white;
            background.raycastTarget = false;
            Stretch(background.rectTransform);

            RectTransform handlesRoot = EnsureRect(
                root,
                "PONTOS EDITAVEIS DAS CARTAS E PILHAS");
            Stretch(handlesRoot);
            Sprite cardBack = LoadFirstSprite(CardBackPath);
            Sprite sampleCard = LoadFirstSprite(SampleCardPath);
            foreach (DuelZone3D zone in zones
                         .Where(value => value != null)
                         .OrderBy(value => value.Owner)
                         .ThenBy(value => value.Kind)
                         .ThenBy(value => value.ZoneIndex))
            {
                CreateZoneHandle(
                    handlesRoot,
                    zone,
                    cardBack,
                    sampleCard);
            }

            CreateHandPreview(
                frame,
                "POSICAO DA MAO DO JOGADOR",
                cardBack,
                false);
            CreateHandPreview(
                frame,
                "POSICAO DA MAO DO OPONENTE",
                cardBack,
                true);
        }

        private static void CreateZoneHandle(
            RectTransform parent,
            DuelZone3D zone,
            Sprite cardBack,
            Sprite sampleCard)
        {
            Transform anchor = zone.CardPresentationAnchor;
            Vector3 world = anchor.position;
            Vector2 normalized = new(
                world.x / FieldWidth + 0.5f,
                world.z / FieldDepth + 0.5f);
            Vector2 size = IsPile(zone.Kind)
                ? new Vector2(88f, 126f)
                : new Vector2(76f, 110f);
            float authoredAngle = -anchor.eulerAngles.y;
            DuelAuthoredZoneLayout authoredLayout =
                parent.GetComponentInParent<DuelAuthoredZoneLayout>(true);
            if (authoredLayout != null &&
                authoredLayout.TryGet(
                    zone.StableId,
                    out DuelAuthoredZoneLayout.Entry authored))
            {
                normalized = authored.ViewportCenter;
                RectTransform frame = authoredLayout.transform as RectTransform;
                if (frame != null)
                {
                    Vector2 rightPixels = new Vector2(
                        authored.ViewportRightHalfAxis.x * frame.rect.width,
                        authored.ViewportRightHalfAxis.y * frame.rect.height);
                    Vector2 upPixels = new Vector2(
                        authored.ViewportUpHalfAxis.x * frame.rect.width,
                        authored.ViewportUpHalfAxis.y * frame.rect.height);
                    size = new Vector2(
                        rightPixels.magnitude * 2f,
                        upPixels.magnitude * 2f);
                }
                authoredAngle = authored.ScreenAngle;
            }

            var item = new GameObject(
                $"{SideName(zone.Owner)} - {KindName(zone.Kind)} " +
                $"{zone.ZoneIndex + 1}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline),
                typeof(DuelSceneZonePreview));
            item.tag = "EditorOnly";
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = normalized;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localEulerAngles = new Vector3(
                0f,
                0f,
                authoredAngle);

            bool representativeMonster =
                zone.Kind == DuelZoneKind.Monster &&
                zone.ZoneIndex == 2;
            Image card = item.GetComponent<Image>();
            card.sprite = representativeMonster ? sampleCard : cardBack;
            card.preserveAspect = true;
            card.raycastTarget = false;
            card.color = IsPile(zone.Kind) || representativeMonster
                ? new Color(1f, 1f, 1f, 0.94f)
                : new Color(1f, 1f, 1f, 0.20f);

            Outline outline = item.GetComponent<Outline>();
            outline.effectColor = ZoneColor(zone.Kind);
            outline.effectDistance = new Vector2(2f, -2f);
            item.GetComponent<DuelSceneZonePreview>().Configure(zone, size);

            Text label = CreateText(rect, "NOME DO PONTO");
            label.text = $"{KindName(zone.Kind)} {zone.ZoneIndex + 1}";
            label.fontSize = 11;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.LowerCenter;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 3f);
            labelRect.sizeDelta = new Vector2(0f, 22f);
        }

        private static void PrepareHandAnchors(RectTransform frame)
        {
            RectTransform local = EnsureRect(
                frame,
                "POSICAO DA MAO DO JOGADOR");
            local.anchorMin = new Vector2(0.5f, 0f);
            local.anchorMax = new Vector2(0.5f, 0f);
            local.pivot = new Vector2(0.5f, 0f);
            if (local.sizeDelta.sqrMagnitude < 1f ||
                (Vector2.Distance(
                     local.sizeDelta,
                     new Vector2(100f, 100f)) < 0.01f &&
                 local.anchoredPosition.sqrMagnitude < 0.01f))
            {
                local.sizeDelta = new Vector2(1000f, 330f);
                local.anchoredPosition = new Vector2(0f, -189f);
            }
            DuelHandLayoutAnchor localLayout =
                local.GetComponent<DuelHandLayoutAnchor>() ??
                local.gameObject.AddComponent<DuelHandLayoutAnchor>();
            localLayout.ConfigureOwner(
                DuelHandLayoutAnchor.HandOwner.LocalPlayer);
            EditorUtility.SetDirty(localLayout);

            RectTransform opponent = EnsureRect(
                frame,
                "POSICAO DA MAO DO OPONENTE");
            opponent.anchorMin = new Vector2(0.365f, 0.865f);
            opponent.anchorMax = new Vector2(0.635f, 0.995f);
            opponent.offsetMin = Vector2.zero;
            opponent.offsetMax = Vector2.zero;
            DuelHandLayoutAnchor opponentLayout =
                opponent.GetComponent<DuelHandLayoutAnchor>() ??
                opponent.gameObject.AddComponent<DuelHandLayoutAnchor>();
            opponentLayout.ConfigureOwner(
                DuelHandLayoutAnchor.HandOwner.Opponent);
            EditorUtility.SetDirty(opponentLayout);
        }

        private static void CreateHandPreview(
            RectTransform frame,
            string anchorName,
            Sprite cardBack,
            bool opponent)
        {
            RectTransform anchor = frame.Find(anchorName) as RectTransform;
            if (anchor == null || cardBack == null)
                return;
            const string name =
                "CARTAS DEMONSTRATIVAS (SOMENTE EDICAO)";
            Transform old = anchor.Find(name);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old.gameObject);

            RectTransform root = EnsureRect(anchor, name);
            root.gameObject.tag = "EditorOnly";
            if (root.GetComponent<CanvasGroup>() == null)
                root.gameObject.AddComponent<CanvasGroup>();
            if (root.GetComponent<DuelSceneAuthoringPreview>() == null)
                root.gameObject.AddComponent<DuelSceneAuthoringPreview>();
            Stretch(root);

            DuelHandLayoutAnchor layout =
                anchor.GetComponent<DuelHandLayoutAnchor>();
            DuelSceneHandPreview handPreview =
                root.GetComponent<DuelSceneHandPreview>() ??
                root.gameObject.AddComponent<DuelSceneHandPreview>();
            RectTransform representativeCard = null;
            const int count = 5;
            for (int index = 0; index < count; index++)
            {
                Image card = CreateImage(root, $"Carta {index + 1}");
                card.sprite = cardBack;
                card.preserveAspect = true;
                card.raycastTarget = false;
                RectTransform rect = card.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, opponent ? 0.5f : 0f);
                rect.sizeDelta = layout != null
                    ? layout.CardSize
                    : opponent
                        ? new Vector2(42f, 61f)
                        : new Vector2(178f, 258f);
                rect.anchoredPosition = layout != null
                    ? layout.PositionFor(index, count)
                    : new Vector2((index - 2) * 72f, 0f);
                rect.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    layout != null ? layout.AngleFor(index, count) : 0f);
                if (representativeCard == null)
                    representativeCard = rect;
            }
            handPreview.Configure(layout, representativeCard);
            EditorUtility.SetDirty(handPreview);
        }

        private static bool ApplyHandle(
            RectTransform frame,
            DuelSceneZonePreview handle)
        {
            if (handle == null || handle.TargetZone == null ||
                !(handle.transform is RectTransform rect))
            {
                return false;
            }

            handle.SyncNow();
            return true;
        }

        private static void ApplyHandPreviews(RectTransform frame)
        {
            foreach (string name in new[]
                     {
                         "POSICAO DA MAO DO JOGADOR",
                         "POSICAO DA MAO DO OPONENTE"
                     })
            {
                RectTransform anchor = frame.Find(name) as RectTransform;
                DuelHandLayoutAnchor layout =
                    anchor != null
                        ? anchor.GetComponent<DuelHandLayoutAnchor>()
                        : null;
                Transform preview = anchor != null
                    ? anchor.Find(
                        "CARTAS DEMONSTRATIVAS (SOMENTE EDICAO)")
                    : null;
                RectTransform card = preview != null
                    ? preview.Find("Carta 1") as RectTransform
                    : null;
                if (layout == null || card == null)
                    continue;
                layout.ConfigureCardSize(card.rect.size);
                DuelSceneHandPreview link =
                    preview.GetComponent<DuelSceneHandPreview>() ??
                    preview.gameObject.AddComponent<DuelSceneHandPreview>();
                link.Configure(layout, card);
                EditorUtility.SetDirty(layout);
                EditorUtility.SetDirty(link);
            }
        }

        private static List<HandleSnapshot> CaptureHandles(
            RectTransform frame)
        {
            var result = new List<HandleSnapshot>();
            foreach (DuelSceneZonePreview handle in FindPreviewHandles())
            {
                if (handle == null || handle.TargetZone == null ||
                    !(handle.transform is RectTransform rect))
                {
                    continue;
                }
                if (!DuelSceneZonePreview.TryCaptureLayout(
                        frame,
                        rect,
                        out Vector2 center,
                        out Vector2 rightHalfAxis,
                        out Vector2 upHalfAxis,
                        out float angle))
                {
                    continue;
                }
                result.Add(new HandleSnapshot(
                    handle.TargetZone.StableId,
                    center,
                    rightHalfAxis,
                    upHalfAxis,
                    angle));
            }
            return result;
        }

        private static void RestoreSnapshots(
            RectTransform frame,
            DuelZone3D[] zones,
            List<HandleSnapshot> snapshots)
        {
            Dictionary<string, DuelZone3D> byId = zones
                .Where(zone => zone != null &&
                               !string.IsNullOrWhiteSpace(zone.StableId))
                .GroupBy(zone => zone.StableId)
                .ToDictionary(group => group.Key, group => group.First());
            DuelAuthoredZoneLayout layout =
                frame.GetComponent<DuelAuthoredZoneLayout>();
            if (layout == null)
                layout = frame.gameObject.AddComponent<DuelAuthoredZoneLayout>();
            foreach (HandleSnapshot snapshot in snapshots)
            {
                if (!byId.TryGetValue(snapshot.StableId, out DuelZone3D zone))
                    continue;
                Transform pile = zone.transform.Find("Card Stack");
                Transform surface = zone.transform.Find("Card Inset");
                BoxCollider collider = zone.GetComponent<BoxCollider>();
                layout.Upsert(
                    zone,
                    snapshot.Center,
                    snapshot.RightHalfAxis,
                    snapshot.UpHalfAxis,
                    snapshot.Angle,
                    pile != null ? pile.localScale : Vector3.zero,
                    surface != null ? surface.localScale : Vector3.zero,
                    collider != null ? collider.size : Vector3.zero);
                layout.ApplyOne(zone);
            }
            EditorUtility.SetDirty(layout);
        }

        private static Scene EnsureDuelScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.path == ScenePath
                ? scene
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static RectTransform FindUniversalFrame()
        {
            CardArenaBootstrap bootstrap =
                UnityEngine.Object.FindAnyObjectByType<CardArenaBootstrap>(
                    FindObjectsInactive.Include);
            Transform canvas = bootstrap != null
                ? bootstrap.transform.Find("Arena Canvas")
                : null;
            RectTransform frame = canvas != null
                ? canvas.Find("Area Segura Universal 16x9") as RectTransform
                : null;
            if (frame == null)
                throw new InvalidOperationException(
                    "Area Segura Universal 16x9 nao foi encontrada.");
            return frame;
        }

        private static MasterDuelArena3D FindMasterArena()
        {
            return UnityEngine.Object
                .FindObjectsByType<MasterDuelArena3D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .OrderByDescending(value =>
                    value.GetComponentsInChildren<DuelZone3D>(true).Length)
                .FirstOrDefault();
        }

        private static DuelZone3D[] FindArenaZones()
        {
            MasterDuelArena3D arena = FindMasterArena();
            return arena != null
                ? arena.GetComponentsInChildren<DuelZone3D>(true)
                : Array.Empty<DuelZone3D>();
        }

        private static DuelSceneZonePreview[] FindPreviewHandles()
        {
            return UnityEngine.Object.FindObjectsByType<DuelSceneZonePreview>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static void RefreshRegistry()
        {
            DuelFieldRegistry registry =
                UnityEngine.Object.FindAnyObjectByType<DuelFieldRegistry>(
                    FindObjectsInactive.Include);
            registry?.RebuildIndex();
            if (registry != null)
                EditorUtility.SetDirty(registry);
        }

        private static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static bool IsPile(DuelZoneKind kind)
        {
            return kind == DuelZoneKind.MainDeck ||
                   kind == DuelZoneKind.ExtraDeck;
        }

        private static string SideName(DuelPlayerSide side)
        {
            return side == DuelPlayerSide.PlayerOne
                ? "JOGADOR"
                : "OPONENTE";
        }

        private static string KindName(DuelZoneKind kind)
        {
            switch (kind)
            {
                case DuelZoneKind.Monster: return "MONSTRO";
                case DuelZoneKind.SpellTrap: return "MAGIA ARMADILHA";
                case DuelZoneKind.Field: return "CAMPO";
                case DuelZoneKind.MainDeck: return "DECK";
                case DuelZoneKind.ExtraDeck: return "EXTRA";
                case DuelZoneKind.Graveyard: return "CEMITERIO";
                case DuelZoneKind.Banishment: return "BANIMENTO";
                default: return kind.ToString().ToUpperInvariant();
            }
        }

        private static Color ZoneColor(DuelZoneKind kind)
        {
            switch (kind)
            {
                case DuelZoneKind.Monster:
                    return new Color(1f, 0f, 0.95f, 0.9f);
                case DuelZoneKind.SpellTrap:
                    return new Color(0f, 1f, 0.22f, 0.9f);
                case DuelZoneKind.Field:
                    return new Color(0.78f, 0.42f, 0f, 0.9f);
                case DuelZoneKind.MainDeck:
                    return new Color(1f, 0.68f, 0f, 0.9f);
                case DuelZoneKind.ExtraDeck:
                    return new Color(1f, 0f, 0f, 0.9f);
                case DuelZoneKind.Graveyard:
                    return new Color(0.2f, 0.55f, 1f, 0.9f);
                case DuelZoneKind.Banishment:
                    return new Color(0.65f, 0.2f, 1f, 0.9f);
                default:
                    return Color.cyan;
            }
        }

        private static Sprite LoadFirstSprite(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .FirstOrDefault();
        }

        private static RawImage CreateRawImage(
            Transform parent,
            string objectName)
        {
            var item = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(RawImage));
            item.transform.SetParent(parent, false);
            return item.GetComponent<RawImage>();
        }

        private static Image CreateImage(
            Transform parent,
            string objectName)
        {
            var item = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image));
            item.transform.SetParent(parent, false);
            return item.GetComponent<Image>();
        }

        private static Text CreateText(
            Transform parent,
            string objectName)
        {
            var item = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Text));
            item.transform.SetParent(parent, false);
            Text text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform EnsureRect(
            Transform parent,
            string objectName)
        {
            Transform existing = parent.Find(objectName);
            if (existing is RectTransform rect)
                return rect;
            var root = new GameObject(objectName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            return root.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private readonly struct HandleSnapshot
        {
            public readonly string StableId;
            public readonly Vector2 Center;
            public readonly Vector2 RightHalfAxis;
            public readonly Vector2 UpHalfAxis;
            public readonly float Angle;

            public HandleSnapshot(
                string stableId,
                Vector2 center,
                Vector2 rightHalfAxis,
                Vector2 upHalfAxis,
                float angle)
            {
                StableId = stableId;
                Center = center;
                RightHalfAxis = rightHalfAxis;
                UpHalfAxis = upHalfAxis;
                Angle = angle;
            }
        }
    }
}
#endif
