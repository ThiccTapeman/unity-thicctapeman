using UnityEngine;

namespace ThiccTapeman.Utils.Camera
{
    public class SmoothFollowCamera3D : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 5f, -10f);
        [Range(0.01f, 20f)] public float followSpeed = 5f;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, t);
        }
    }
}
