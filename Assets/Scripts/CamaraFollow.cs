using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 6f, -6f);

    void LateUpdate()
    {
        if (!target) return;
        transform.position = target.position + offset;
        transform.LookAt(target.position);
    }
}