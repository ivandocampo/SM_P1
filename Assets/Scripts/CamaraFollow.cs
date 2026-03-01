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

    [Header("Vista Cenital (Tab para alternar)")]
    public float distanciaCenital = 40f;     // Distancia de la cámara en vista cenital
    public float anguloCenital = 75f;        // Ángulo desde arriba (90 = recto, 75 = ligeramente inclinada)

    private float rotX = 45f;
    private float rotY = 0f;
    private bool vistaCenital = false;

    void Start()
    {
        rotX = rotxInicial;
        rotY = rotyInicial;
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            vistaCenital = !vistaCenital;
        }

        if (vistaCenital)
        {
            if (!target) return;
            // Sigue a Frodo desde arriba con un ligero ángulo
            Quaternion rotCenital = Quaternion.Euler(anguloCenital, rotY, 0);
            transform.position = target.position + rotCenital * new Vector3(0, 0, -distanciaCenital);
            transform.LookAt(target.position);
            return;
        }

        // Vista normal: sigue a Frodo
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