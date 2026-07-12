using UnityEngine;

namespace Essentials.BlockbenchImporter
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class BlockbenchBillboard : MonoBehaviour
    {
        public enum FacingMode
        {
            LookAt,
            LookAtYAxis,
            CameraRotation,
            CameraRotationYAxis
        }

        [SerializeField] private FacingMode facingMode;

        public FacingMode Mode
        {
            get => facingMode;
            set => facingMode = value;
        }

        private void LateUpdate()
        {
            if (Application.isPlaying && Camera.main != null) Face(Camera.main);
        }

        private void OnWillRenderObject()
        {
            if (!Application.isPlaying && Camera.current != null) Face(Camera.current);
        }

        private void Face(Camera camera)
        {
            if (facingMode == FacingMode.CameraRotation)
            {
                transform.rotation = camera.transform.rotation;
                return;
            }
            if (facingMode == FacingMode.CameraRotationYAxis)
            {
                Vector3 forward = camera.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.000001f)
                    transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
                return;
            }
            Vector3 direction = camera.transform.position - transform.position;
            if (facingMode == FacingMode.LookAtYAxis) direction.y = 0f;
            if (direction.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);
        }
    }
}
