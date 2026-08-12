using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class ShopSceneViewEditModeTests
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [Test]
        public void MainMenuContainsPersistentConfiguredShopView()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);

            MonoBehaviour shop = FindSceneBehaviour(
                "ArcaneArena.Frontend.ShopSceneView");
            MonoBehaviour mainMenu = FindSceneBehaviour(
                "ArcaneArena.Frontend.MainMenuSceneView");

            Assert.That(shop, Is.Not.Null);
            Assert.That(mainMenu, Is.Not.Null);
            Assert.That(Property<bool>(shop, "IsConfigured"), Is.True);
            RectTransform root = Property<RectTransform>(shop, "Root");
            RectTransform dynamicRoot =
                Property<RectTransform>(mainMenu, "DynamicRoot");
            Assert.That(root.name, Is.EqualTo("LOJA EDITAVEL"));
            Assert.That(root.gameObject.activeSelf, Is.False);
            Assert.That(root.parent, Is.EqualTo(dynamicRoot.parent));
            Assert.That(root.IsChildOf(dynamicRoot), Is.False);
            Assert.That(root.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(root.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void ShopCatalogUsesAuthoredScrollAndDynamicContentOnly()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MonoBehaviour shop = FindSceneBehaviour(
                "ArcaneArena.Frontend.ShopSceneView");

            Assert.That(shop, Is.Not.Null);
            ScrollRect scroll = Property<ScrollRect>(shop, "CatalogScroll");
            RectTransform content =
                Property<RectTransform>(shop, "CatalogContent");
            GridLayoutGroup grid =
                Property<GridLayoutGroup>(shop, "CatalogGrid");
            Assert.That(scroll.content, Is.EqualTo(content));
            Assert.That(scroll.viewport, Is.Not.Null);
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(grid.constraint,
                Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(3));
            Assert.That(
                grid.constraintCount * grid.cellSize.x +
                (grid.constraintCount - 1) * grid.spacing.x +
                grid.padding.left + grid.padding.right,
                Is.LessThan(1590f),
                "As tres colunas precisam caber no viewport de referencia.");
            Assert.That(content.GetComponent<ContentSizeFitter>(),
                Is.Not.Null);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(),
                Is.Not.Null);
            Assert.That(content.childCount, Is.Zero,
                "Somente os produtos do catalogo podem ser criados em runtime.");
        }

        private static MonoBehaviour FindSceneBehaviour(string fullName)
        {
            return Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include)
                .FirstOrDefault(component =>
                    component != null &&
                    component.gameObject.scene.IsValid() &&
                    component.GetType().FullName == fullName);
        }

        private static T Property<T>(object source, string name)
        {
            PropertyInfo property = source.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                $"Propriedade publica ausente: {source.GetType().FullName}.{name}");
            return (T)property.GetValue(source);
        }
    }
}
