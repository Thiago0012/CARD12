using ArcaneDuel.DuelEngine.State;
using UnityEngine;

namespace ArcaneArena
{
    [DisallowMultipleComponent]
    public sealed class WorldCardInstanceView : MonoBehaviour
    {
        public CardInstanceKey InstanceKey { get; private set; }
        public bool IsFaceUp { get; private set; }
        public bool IsVisuallyReady =>
            gameObject.activeInHierarchy &&
            transform.Find("Frente") != null &&
            transform.Find("Verso") != null;

        public void Bind(CardInstanceKey key, bool faceUp)
        {
            InstanceKey = key;
            IsFaceUp = faceUp;
        }
    }
}
