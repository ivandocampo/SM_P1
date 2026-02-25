using UnityEngine;

public class CamaraFollow : MonoBehaviour
{
    public Transform target;
    public float distancia = 8f;
    public float sensibilidadRaton = 3f;
    public float sensibilidadTeclado = 60f;
    public float alturaMin = 5f;
    public float alturaMax = 80f;
    public float zoomMin = 3f;
    public float zoomMax = 20f;

    public float rotxInicial = 45f;
    public float rotyInicial = 180f;

    private float rotX = 45f;
    private float rotY = 0f;

    void Start()
    {
        rotX = rotxInicial;
        rotY = rotyInicial;
    }

    void LateUpdate()
    {
        if (!target) return;

        if (Input.GetMouseButton(1))
        {
            rotY += Input.GetAxis("Mouse X") * sensibilidadRaton;
            rotX -= Input.GetAxis("Mouse Y") * sensibilidadRaton;
        }

        rotY += Input.GetKey(KeyCode.E) ? sensibilidadTeclado * Time.deltaTime : 0;
        rotY -= Input.GetKey(KeyCode.Q) ? sensibilidadTeclado * Time.deltaTime : 0;
        rotX -= Input.GetKey(KeyCode.R) ? sensibilidadTeclado * Time.deltaTime : 0;
        rotX += Input.GetKey(KeyCode.F) ? sensibilidadTeclado * Time.deltaTime : 0;

        distancia -= Input.GetAxis("Mouse ScrollWheel") * 5f;
        distancia = Mathf.Clamp(distancia, zoomMin, zoomMax);
        rotX = Mathf.Clamp(rotX, alturaMin, alturaMax);

        Quaternion rotacion = Quaternion.Euler(rotX, rotY, 0);
        transform.position = target.position + rotacion * new Vector3(0, 0, -distancia);
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}