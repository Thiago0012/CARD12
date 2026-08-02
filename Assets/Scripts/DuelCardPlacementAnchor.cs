using UnityEngine;

namespace ArcaneArena
{
    public enum DuelCardAnchorRole
    {
        Card,
        CombatLabel
    }

    /// <summary>
    /// Marcador visual editavel. Nao possui identidade nem regra de duelo;
    /// apenas define onde a apresentacao confirmada sera desenhada.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class DuelCardPlacementAnchor : MonoBehaviour
    {
        [SerializeField] private DuelCardAnchorRole role;
        public DuelCardAnchorRole Role => role;

        public void Configure(DuelCardAnchorRole anchorRole)
        {
            role = anchorRole;
        }

        private void OnDrawGizmos()
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            if (role == DuelCardAnchorRole.Card)
            {
                Gizmos.color = new Color(0.15f, 0.9f, 1f, 0.9f);
                Gizmos.DrawWireCube(
                    Vector3.zero,
                    new Vector3(1.33f, 0.02f, 1.92f));
            }
            else
            {
                Gizmos.color = new Color(1f, 0.82f, 0.2f, 0.9f);
                Gizmos.DrawWireCube(
                    Vector3.zero,
                    new Vector3(1.8f, 0.02f, 0.38f));
            }
            Gizmos.matrix = previous;
        }
    }
}
