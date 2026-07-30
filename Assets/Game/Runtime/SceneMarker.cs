using UnityEngine;

namespace ArcaneDuel.Game
{
    public enum SceneRole
    {
        Bootstrap,
        Duel,
        CardLab
    }

    public sealed class SceneMarker : MonoBehaviour
    {
        [SerializeField] private SceneRole role;

        public SceneRole Role => role;

        public void Configure(SceneRole value)
        {
            role = value;
        }
    }
}

