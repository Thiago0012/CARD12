using UnityEngine;

namespace ArcaneArena
{
    public sealed class CameraFacingCardLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;
        }
    }
}
