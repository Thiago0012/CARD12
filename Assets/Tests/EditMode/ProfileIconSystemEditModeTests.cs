using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class ProfileIconSystemEditModeTests
    {
        [Test]
        public void CatalogKeepsAnimatedIconsExclusiveAndTheOthersAtExactPrice()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            object[] icons = Values(catalog.GetProperty("All").GetValue(null));
            object[] purchasable = icons.Where(icon =>
                (bool)Property(icon, "IsPurchasable")).ToArray();

            Assert.That(icons, Has.Length.EqualTo(26));
            Assert.That(purchasable, Has.Length.EqualTo(22));
            Assert.That(purchasable.All(icon =>
                (int)Property(icon, "PriceCoins") == 35), Is.True);
            Assert.That(icons.Count(icon =>
                !(bool)Property(icon, "IsPurchasable")), Is.EqualTo(4));
            Assert.That(icons.Count(icon =>
                (bool)Property(icon, "IsExclusive")), Is.EqualTo(3));
            Assert.That(icons.Count(icon =>
                Property(icon, "AssetMode").ToString() == "PreframedHex"),
                Is.EqualTo(10),
                "O ícone padrão e as nove artes originais preservam o recorte legado.");
            Assert.That(icons.Count(icon =>
                Property(icon, "AssetMode").ToString() == "UnframedPortrait"),
                Is.EqualTo(16),
                "As novas artes devem receber a máscara e a moldura oficiais em runtime.");
            Assert.That(icons.Select(icon => Property(icon, "IconId") as string)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(icons.Length));
            Assert.That(purchasable.Select(icon =>
                    Property(icon, "ResourcePath") as string)
                .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(purchasable.Length));
        }

        [Test]
        public void ProfileIdentityPlacesNameByIconAndSignatureAboveIt()
        {
            var root = new GameObject(
                "Identidade do Perfil em Teste",
                typeof(RectTransform));
            try
            {
                Type bootstrap = FindType(
                    "ArcaneArena.Frontend.GameFrontendBootstrap");
                MethodInfo buildSignature = bootstrap.GetMethod(
                    "BuildProfileIdentitySignature",
                    BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo buildNamePlate = bootstrap.GetMethod(
                    "BuildProfilePlayerNamePlate",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(buildSignature, Is.Not.Null);
                Assert.That(buildNamePlate, Is.Not.Null);

                buildSignature.Invoke(null, new object[]
                {
                    root.transform,
                    "Maga do Eclipse Violeta",
                    "7f3a91c2deadbeef"
                });
                buildNamePlate.Invoke(
                    null,
                    new object[] { root.transform, "KimDelas" });

                Transform signature = root.transform.Find(
                    "Assinatura do Duelista");
                Transform namePlate = root.transform.Find(
                    "Nome Próximo ao Ícone");
                Assert.That(signature, Is.Not.Null);
                Assert.That(namePlate, Is.Not.Null);
                Assert.That(
                    signature.GetComponent<RectTransform>().anchorMin.y,
                    Is.GreaterThan(
                        namePlate.GetComponent<RectTransform>().anchorMax.y));

                string[] signatureTexts = signature
                    .GetComponentsInChildren<Text>(true)
                    .Select(text => text.text)
                    .ToArray();
                Assert.That(signatureTexts, Does.Contain("TÍTULO DO PERFIL"));
                Assert.That(signatureTexts,
                    Does.Contain("MAGA DO ECLIPSE VIOLETA"));
                Assert.That(signatureTexts, Does.Contain("ID DA CONTA"));
                Assert.That(signatureTexts.Any(value =>
                    value != null &&
                    value.Length == 12 &&
                    value.All(char.IsDigit)),
                    Is.True,
                    "O ID público deve conter somente doze números.");
                Assert.That(signature.Find("Divisor da Assinatura"),
                    Is.Not.Null);

                string[] nameTexts = namePlate
                    .GetComponentsInChildren<Text>(true)
                    .Select(text => text.text)
                    .ToArray();
                Assert.That(nameTexts, Does.Contain("NOME DO DUELISTA"));
                Assert.That(nameTexts, Does.Contain("KimDelas"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CrimsonIconUsesExclusiveAuraWithoutTheBlueFrame()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            Type viewType = FindType("ArcaneArena.Frontend.HexIconView");
            object[] icons = Values(catalog.GetProperty("All").GetValue(null));
            object crimson = icons.Single(icon => string.Equals(
                Property(icon, "IconId") as string,
                "icon-crimson-veil-arcanist",
                StringComparison.Ordinal));
            Assert.That(Property(crimson, "AuraTheme").ToString(),
                Is.EqualTo("CrimsonLegendary"));
            Assert.That(icons.Count(icon =>
                Property(icon, "AuraTheme").ToString() != "None"),
                Is.EqualTo(3));

            var parent = new GameObject("Exclusive Aura Bounds",
                typeof(RectTransform));
            var root = new GameObject("Exclusive Aura Icon",
                typeof(RectTransform));
            root.transform.SetParent(parent.transform, false);
            try
            {
                Component view = root.AddComponent(viewType);
                MethodInfo setIcon = viewType.GetMethod("SetIcon");
                setIcon.Invoke(view, new object[]
                {
                    "icon-crimson-veil-arcanist"
                });

                Image standardFrame = root.GetComponent<Image>();
                Transform aura = root.transform.Find(
                    "Aura Viva da Moldura");
                Transform clip = root.transform.Find("Recorte Hexagonal");
                Assert.That(standardFrame.enabled, Is.False,
                    "A moldura azul padrão não pode aparecer no ícone especial.");
                Assert.That(aura, Is.Not.Null);
                Assert.That(aura.gameObject.activeSelf, Is.True);
                Assert.That(aura.parent, Is.EqualTo(root.transform));
                Assert.That(aura.IsChildOf(clip), Is.False,
                    "A aura deve ficar fora da máscara para não cobrir o retrato.");
                Assert.That(aura.GetComponent<Graphic>().raycastTarget,
                    Is.False);
                RectTransform auraRect = (RectTransform)aura;
                Assert.That(auraRect.anchorMin.x,
                    Is.EqualTo(-0.16f).Within(0.0001f));
                Assert.That(auraRect.anchorMax.x,
                    Is.EqualTo(1.16f).Within(0.0001f));

                setIcon.Invoke(view, new object[]
                {
                    catalog.GetField("DefaultIconId").GetValue(null)
                });
                Assert.That(standardFrame.enabled, Is.True);
                Assert.That(aura.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void AzureTempestDragonUsesItsExclusiveAnimatedAura()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            Type viewType = FindType("ArcaneArena.Frontend.HexIconView");
            object[] icons = Values(catalog.GetProperty("All").GetValue(null));
            object dragon = icons.Single(icon => string.Equals(
                Property(icon, "IconId") as string,
                "icon-azure-tempest-dragon",
                StringComparison.Ordinal));
            Assert.That(Property(dragon, "AuraTheme").ToString(),
                Is.EqualTo("AzureTempest"));
            Assert.That(Property(dragon, "AssetMode").ToString(),
                Is.EqualTo("UnframedPortrait"));

            var root = new GameObject("Azure Tempest Icon",
                typeof(RectTransform));
            try
            {
                Component view = root.AddComponent(viewType);
                viewType.GetMethod("SetIcon").Invoke(view, new object[]
                {
                    "icon-azure-tempest-dragon"
                });

                Image standardFrame = root.GetComponent<Image>();
                Transform aura = root.transform.Find(
                    "Aura Viva da Moldura");
                Assert.That(standardFrame.enabled, Is.False,
                    "A aura elétrica deve substituir integralmente a moldura azul comum.");
                Assert.That(aura, Is.Not.Null);
                Assert.That(aura.gameObject.activeSelf, Is.True);
                Component auraView = aura.GetComponent(
                    FindType("ArcaneArena.Frontend.HexIconAuraView"));
                Assert.That(Property(auraView, "Theme").ToString(),
                    Is.EqualTo("AzureTempest"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VioletEclipseSorceressUsesItsExclusiveAnimatedAura()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            Type viewType = FindType("ArcaneArena.Frontend.HexIconView");
            object[] icons = Values(catalog.GetProperty("All").GetValue(null));
            object sorceress = icons.Single(icon => string.Equals(
                Property(icon, "IconId") as string,
                "icon-violet-eclipse-sorceress",
                StringComparison.Ordinal));
            Assert.That(Property(sorceress, "AuraTheme").ToString(),
                Is.EqualTo("VioletEclipse"));
            Assert.That(Property(sorceress, "AssetMode").ToString(),
                Is.EqualTo("UnframedPortrait"));

            var root = new GameObject("Violet Eclipse Icon",
                typeof(RectTransform));
            try
            {
                Component view = root.AddComponent(viewType);
                viewType.GetMethod("SetIcon").Invoke(view, new object[]
                {
                    "icon-violet-eclipse-sorceress"
                });

                Image standardFrame = root.GetComponent<Image>();
                Transform aura = root.transform.Find(
                    "Aura Viva da Moldura");
                Assert.That(standardFrame.enabled, Is.False,
                    "A aura violeta deve substituir integralmente a moldura azul comum.");
                Assert.That(aura, Is.Not.Null);
                Assert.That(aura.gameObject.activeSelf, Is.True);
                Component auraView = aura.GetComponent(
                    FindType("ArcaneArena.Frontend.HexIconAuraView"));
                Assert.That(Property(auraView, "Theme").ToString(),
                    Is.EqualTo("VioletEclipse"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EveryProfileIconUsesTheSameRegularHexagonalViewport()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            Type viewType = FindType("ArcaneArena.Frontend.HexIconView");
            object[] icons = Values(catalog.GetProperty("All").GetValue(null));
            var parent = new GameObject("Profile Icon Bounds",
                typeof(RectTransform));
            var root = new GameObject("Profile Icon Scale Test",
                typeof(RectTransform));
            root.transform.SetParent(parent.transform, false);
            try
            {
                Component view = root.AddComponent(viewType);
                MethodInfo setIcon = viewType.GetMethod("SetIcon");
                foreach (object icon in icons)
                {
                    setIcon.Invoke(
                        view,
                        new[] { Property(icon, "IconId") });
                    Transform clip = root.transform.Find("Recorte Hexagonal");
                    Transform portrait = clip?.Find("Retrato");
                    AspectRatioFitter rootFitter =
                        root.GetComponent<AspectRatioFitter>();
                    Assert.That(portrait, Is.Not.Null);
                    AspectRatioFitter portraitFitter =
                        portrait.GetComponent<AspectRatioFitter>();
                    Assert.That(clip.GetComponent<Mask>().enabled, Is.True);
                    Assert.That(rootFitter, Is.Not.Null);
                    Assert.That(rootFitter.aspectRatio,
                        Is.EqualTo(0.8660254f).Within(0.0001f));
                    Assert.That(portraitFitter.aspectRatio,
                        Is.EqualTo(0.8660254f).Within(0.0001f));
                    Assert.That(((RectTransform)clip).anchorMin.x,
                        Is.EqualTo(0.055f).Within(0.0001f));
                    Assert.That(((RectTransform)clip).anchorMin.y,
                        Is.EqualTo(0.055f).Within(0.0001f));
                    Assert.That(((RectTransform)clip).anchorMax.x,
                        Is.EqualTo(0.945f).Within(0.0001f));
                    Assert.That(((RectTransform)clip).anchorMax.y,
                        Is.EqualTo(0.945f).Within(0.0001f));
                    Assert.That(portrait.localScale.x,
                        Is.EqualTo(1f).Within(0.001f));
                    Assert.That(portrait.localScale.y,
                        Is.EqualTo(1f).Within(0.001f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void EveryPurchasableIconLoadsAsAProjectResource()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            object[] icons = Values(catalog.GetProperty("All").GetValue(null));
            foreach (object icon in icons.Where(candidate =>
                         (bool)Property(candidate, "IsPurchasable")))
            {
                string path = Property(icon, "ResourcePath") as string;
                Assert.That(Resources.Load<Texture2D>(path), Is.Not.Null,
                    "Recurso ausente: " + path);
            }
        }

        [Test]
        public void PurchaseIsAtomicIdempotentAndEquipPersists()
        {
            string path = TemporarySave("purchase");
            try
            {
                object repository = CreateRepository(path);
                SetCoinBalance(repository, 75);
                string iconId = PurchasableIds()[0];

                object[] first = { iconId, "icon-tx-1", null, null };
                bool purchased = (bool)repository.GetType()
                    .GetMethod("TryPurchaseIcon").Invoke(repository, first);
                Assert.That(purchased, Is.True, first[3] as string);
                Assert.That(CoinBalance(repository), Is.EqualTo(40));

                object[] repeated = { iconId, "icon-tx-1", null, null };
                bool replayed = (bool)repository.GetType()
                    .GetMethod("TryPurchaseIcon").Invoke(repository, repeated);
                Assert.That(replayed, Is.True, repeated[3] as string);
                Assert.That(CoinBalance(repository), Is.EqualTo(40));

                object[] equip = { iconId, null };
                bool equipped = (bool)repository.GetType()
                    .GetMethod("TryEquipIcon").Invoke(repository, equip);
                Assert.That(equipped, Is.True, equip[1] as string);

                object reloaded = CreateRepository(path);
                Assert.That(Property(reloaded, "EquippedIconId"),
                    Is.EqualTo(iconId));
                Assert.That(CoinBalance(reloaded), Is.EqualTo(40));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void AnimatedExclusiveIconsCannotBePurchasedWithCoins()
        {
            string path = TemporarySave("exclusive-icon");
            try
            {
                object repository = CreateRepository(path);
                SetCoinBalance(repository, 999);
                object[] purchase =
                {
                    "icon-azure-tempest-dragon", "exclusive-icon-tx", null, null
                };
                bool purchased = (bool)repository.GetType()
                    .GetMethod("TryPurchaseIcon").Invoke(repository, purchase);
                Assert.That(purchased, Is.False);
                Assert.That(purchase[3] as string,
                    Does.Contain("exclusivo").IgnoreCase);
                Assert.That(CoinBalance(repository), Is.EqualTo(999));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void DuelIdentityIsFrozenAfterMatchStarts()
        {
            string path = TemporarySave("identity");
            try
            {
                object repository = CreateRepository(path);
                SetCoinBalance(repository, 100);
                string[] ids = PurchasableIds().Take(2).ToArray();
                PurchaseAndEquip(repository, ids[0], "identity-tx-1");
                object snapshot = repository.GetType()
                    .GetMethod("CaptureDuelIdentitySnapshot")
                    .Invoke(repository, null);

                PurchaseAndEquip(repository, ids[1], "identity-tx-2");
                Assert.That(Field(snapshot, "equippedIconId"),
                    Is.EqualTo(ids[0]),
                    "A identidade apresentada no duelo deve ser imutável.");
                Assert.That(Property(repository, "EquippedIconId"),
                    Is.EqualTo(ids[1]));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void ConfirmedStatisticsAreScopedAndIdempotent()
        {
            string path = TemporarySave("statistics");
            try
            {
                object repository = CreateRepository(path);
                Type eventType = FindType(
                    "ArcaneArena.Frontend.DuelStatisticEventType");
                object specialSummon = Enum.Parse(eventType, "SpecialSummon");
                MethodInfo recordEvent = repository.GetType().GetMethod(
                    "TryRecordAuthoritativeStatisticEvent");
                object[] first =
                    { "match-1:event-1", specialSummon, 1L, true, true, null };
                Assert.That((bool)recordEvent.Invoke(repository, first), Is.True,
                    first[5] as string);
                object[] replay =
                    { "match-1:event-1", specialSummon, 1L, true, true, null };
                Assert.That((bool)recordEvent.Invoke(repository, replay), Is.True,
                    replay[5] as string);

                MethodInfo recordResult = repository.GetType().GetMethod(
                    "TryRecordAuthoritativeDuelResult");
                object[] result =
                    { "match-1", true, false, true, true, 2500L, 900L, null };
                Assert.That((bool)recordResult.Invoke(repository, result), Is.True,
                    result[7] as string);
                object[] resultReplay =
                    { "match-1", true, false, true, true, 2500L, 900L, null };
                Assert.That((bool)recordResult.Invoke(repository, resultReplay),
                    Is.True, resultReplay[7] as string);

                object statistics = Property(repository, "Statistics");
                foreach (string scopeName in new[] { "overall", "online", "ranked" })
                {
                    object scope = Field(statistics, scopeName);
                    Assert.That(Field(scope, "duelsPlayed"), Is.EqualTo(1L));
                    Assert.That(Field(scope, "wins"), Is.EqualTo(1L));
                    Assert.That(Field(scope, "damageDealt"), Is.EqualTo(2500L));
                    Assert.That(Field(scope, "damageReceived"), Is.EqualTo(900L));
                    Assert.That(
                        Field(scope, "maxDamageDealtInSingleDuel"),
                        Is.EqualTo(2500L));
                    Assert.That(
                        Field(scope, "maxDamageReceivedInSingleDuel"),
                        Is.EqualTo(900L));
                    Assert.That(Field(scope, "specialSummons"), Is.EqualTo(1L));
                    Assert.That(Field(scope, "monstersSummoned"), Is.EqualTo(1L));
                }
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void LegacyStatisticsMigrateWithoutResettingExistingCounters()
        {
            string path = TemporarySave("statistics-migration");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(
                    path,
                    "{\"schemaVersion\":9,\"statistics\":{" +
                    "\"overall\":{\"duelsPlayed\":7," +
                    "\"wins\":4,\"damageDealt\":12345}," +
                    "\"online\":{},\"ranked\":{}}}");

                object repository = CreateRepository(path);
                object state = Property(repository, "State");
                object statistics = Property(repository, "Statistics");
                object overall = Field(statistics, "overall");

                Assert.That(Field(state, "schemaVersion"), Is.EqualTo(12));
                Assert.That(Field(overall, "duelsPlayed"), Is.EqualTo(7L));
                Assert.That(Field(overall, "wins"), Is.EqualTo(4L));
                Assert.That(Field(overall, "damageDealt"), Is.EqualTo(12345L));
                Assert.That(Field(overall, "damageReceived"), Is.EqualTo(0L));
                Assert.That(
                    Field(overall, "maxDamageDealtInSingleDuel"),
                    Is.EqualTo(0L));
                Assert.That(
                    Field(overall, "maxDamageReceivedInSingleDuel"),
                    Is.EqualTo(0L));
            }
            finally
            {
                DeleteSave(path);
            }
        }

        [Test]
        public void DuelProfileNormalizationIsSafeForEmptyProfiles()
        {
            Type config = FindType(
                "ArcaneArena.Frontend.DuelStatsVisualizationConfig");
            MethodInfo normalize = config.GetMethod(
                "Normalize",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo resolve = config.GetMethod(
                "Resolve",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(normalize, Is.Not.Null);
            Assert.That(resolve, Is.Not.Null);
            object configuredCaps = resolve.Invoke(null, null);
            Assert.That(Field(configuredCaps, "damagePerDuelCap"),
                Is.EqualTo(8000f));
            Assert.That(
                (float)normalize.Invoke(null, new object[] { 100f, 0f }),
                Is.EqualTo(1f));
            Assert.That(
                (float)normalize.Invoke(null, new object[] { 0f, 0f }),
                Is.Zero);
            Assert.That(
                (float)normalize.Invoke(null, new object[] { -10f, 100f }),
                Is.Zero);
        }

        [Test]
        public void HudSafeAreaUsesIntersectionWithArenaViewport()
        {
            Type fitter = FindType("ArcaneArena.Frontend.DuelHudSafeAreaFitter");
            MethodInfo intersect = fitter.GetMethod(
                "Intersect", BindingFlags.Public | BindingFlags.Static);
            Rect actual = (Rect)intersect.Invoke(null, new object[]
            {
                new Rect(30f, 20f, 1900f, 1040f),
                new Rect(100f, 0f, 1720f, 1080f)
            });
            Assert.That(actual, Is.EqualTo(new Rect(100f, 20f, 1720f, 1040f)));
        }

        [Test]
        public void DuelPlateReusesAuthoredLabelAndBindsExactProfileIcon()
        {
            var root = new GameObject("LP do Player", typeof(RectTransform));
            try
            {
                CreateAuthoredText(root.transform, "PLAYER");
                CreateAuthoredText(root.transform, "LP");
                CreateAuthoredText(root.transform, "8000");

                Type identityType = FindType(
                    "ArcaneArena.Frontend.DuelIdentitySnapshot");
                object identity = Activator.CreateInstance(identityType);
                identityType.GetField("stablePlayerId").SetValue(
                    identity, "profile-kim");
                identityType.GetField("nickname").SetValue(identity, "KimDelas");
                identityType.GetField("equippedIconId").SetValue(
                    identity, "icon-crimson-knight");
                Type rankType = identityType.GetField("rankTier").FieldType;
                identityType.GetField("rankTier").SetValue(
                    identity, Enum.Parse(rankType, "Gold"));
                identityType.GetField("cosmeticsCatalogVersion").SetValue(
                    identity, 1);

                Type plateType = FindType(
                    "ArcaneArena.Frontend.DuelPlayerPlateView");
                Component plate = root.AddComponent(plateType);
                Type sideType = plateType.GetNestedType("PlateSide");
                plateType.GetMethod("Bind").Invoke(plate, new[]
                {
                    identity,
                    Enum.Parse(sideType, "Local")
                });

                Assert.That(root.transform.Find("PLAYER")
                    .GetComponent<Text>().text, Is.EqualTo("KIMDELAS"));
                Assert.That(root.transform.Find("Identidade do Duelista"),
                    Is.Null, "A HUD não deve duplicar o nome já desenhado.");
                Transform icon = root.transform.Find("Ícone do Perfil");
                Assert.That(icon, Is.Not.Null);
                object iconView = icon.GetComponent(
                    FindType("ArcaneArena.Frontend.HexIconView"));
                Assert.That(Property(iconView, "IconId"),
                    Is.EqualTo("icon-crimson-knight"));
                Assert.That(root.transform.Find("Patente do Duelista"),
                    Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Text CreateAuthoredText(Transform parent, string name)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.text = name;
            return text;
        }

        private static void PurchaseAndEquip(
            object repository,
            string iconId,
            string transactionId)
        {
            object[] purchase = { iconId, transactionId, null, null };
            Assert.That((bool)repository.GetType().GetMethod("TryPurchaseIcon")
                .Invoke(repository, purchase), Is.True, purchase[3] as string);
            object[] equip = { iconId, null };
            Assert.That((bool)repository.GetType().GetMethod("TryEquipIcon")
                .Invoke(repository, equip), Is.True, equip[1] as string);
        }

        private static object CreateRepository(string path)
        {
            Type type = FindType("ArcaneArena.Frontend.DeckRepository");
            object repository = Activator.CreateInstance(type, path);
            type.GetMethod("Load").Invoke(repository, new object[] { null, false });
            return repository;
        }

        private static string[] PurchasableIds()
        {
            Type catalog = FindType("ArcaneArena.Frontend.ProfileIconCatalog");
            return Values(catalog.GetProperty("All").GetValue(null))
                .Where(icon => (bool)Property(icon, "IsPurchasable"))
                .Select(icon => Property(icon, "IconId") as string)
                .ToArray();
        }

        private static void SetCoinBalance(object repository, int value)
        {
            object state = Property(repository, "State");
            state.GetType().GetField("coinBalance").SetValue(state, value);
        }

        private static int CoinBalance(object repository) =>
            (int)Property(repository, "CoinBalance");

        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "Tipo runtime ausente: " + fullName);
            return type;
        }

        private static object Property(object source, string name) =>
            source.GetType().GetProperty(name).GetValue(source);

        private static object Field(object source, string name) =>
            source.GetType().GetField(name).GetValue(source);

        private static object[] Values(object source) =>
            ((IEnumerable)source).Cast<object>().ToArray();

        private static string TemporarySave(string suffix) => Path.Combine(
            Path.GetFullPath(Path.Combine("Temp", "ArcaneProfileIconTests")),
            "profile-" + suffix + "-" + Guid.NewGuid().ToString("N") + ".json");

        private static void DeleteSave(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;
            foreach (string candidate in Directory.GetFiles(
                         directory, Path.GetFileName(path) + "*"))
            {
                File.Delete(candidate);
            }
        }
    }
}
