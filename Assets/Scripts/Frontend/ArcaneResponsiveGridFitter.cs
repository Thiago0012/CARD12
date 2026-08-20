using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Mantém colunas com a mesma largura independentemente da resolução.
    /// A fórmula reserva o padding e os vãos antes de dividir o espaço útil.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(GridLayoutGroup))]
    public sealed class ArcaneResponsiveGridFitter : MonoBehaviour
    {
        private GridLayoutGroup _grid;
        private RectTransform _rect;
        private int _columns = 1;
        private float _cellHeight = 100f;
        private float _lastWidth = -1f;

        public void Configure(int columns, float cellHeight, float spacing)
        {
            _columns = Mathf.Max(1, columns);
            _cellHeight = Mathf.Max(1f, cellHeight);
            EnsureReferences();
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = _columns;
            _grid.spacing = new Vector2(Mathf.Max(0f, spacing), _grid.spacing.y);
            ApplyWidth(true);
        }

        private void OnEnable()
        {
            EnsureReferences();
            ApplyWidth(true);
        }

        private void LateUpdate()
        {
            ApplyWidth(false);
        }

        private void EnsureReferences()
        {
            if (_grid == null)
                _grid = GetComponent<GridLayoutGroup>();
            if (_rect == null)
                _rect = GetComponent<RectTransform>();
        }

        private void ApplyWidth(bool force)
        {
            EnsureReferences();
            if (_grid == null || _rect == null)
                return;
            float width = _rect.rect.width;
            if (width <= 0f || (!force && Mathf.Abs(width - _lastWidth) < 0.25f))
                return;
            _lastWidth = width;
            float spacingWidth = _grid.spacing.x * (_columns - 1);
            float usableWidth = Mathf.Max(
                1f,
                width - _grid.padding.horizontal - spacingWidth);
            _grid.cellSize = new Vector2(usableWidth / _columns, _cellHeight);
            LayoutRebuilder.MarkLayoutForRebuild(_rect);
        }
    }
}
