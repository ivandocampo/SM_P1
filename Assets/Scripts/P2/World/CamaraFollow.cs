// =============================================================
// Camara de seguimiento de Frodo.
// Mantiene la camara orientada hacia el jugador, permite rotacion y
// zoom, y alterna entre vista normal y vista cenital. Actualiza su
// posicion en LateUpdate para moverse despues del objetivo
// =============================================================

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
    public float distanciaCenital = 40f;
    public float anguloCenital = 75f;

    private float rotX = 45f;
    private float rotY = 0f;
    private bool vistaCenital = false;

    // Inicializa la rotación de la cámara con los valores definidos por el usuario
    void Start()
    {
        rotX = rotxInicial;
        rotY = rotyInicial;
    }

    // Ejecuta la actualización de posición después de que el objetivo se haya movido
    void LateUpdate()
    {
        // Cambia el estado de la vista entre normal y cenital al detectar la tecla Tab
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            vistaCenital = !vistaCenital;
        }

        // Gestiona el comportamiento de la cámara en modo de vista superior
        if (vistaCenital)
        {
            if (!target) return;
            
            // Calcula la posición elevada basándose en el ángulo cenital y la rotación horizontal
            Quaternion rotCenital = Quaternion.Euler(anguloCenital, rotY, 0);
            transform.position = target.position + rotCenital * new Vector3(0, 0, -distanciaCenital);
            
            // Orienta la cámara para que apunte directamente al objetivo
            transform.LookAt(target.position);
            return;
        }

        // Verifica que exista un objetivo asignado antes de procesar la vista normal
        if (!target) return;

        // Procesa la rotación mediante el movimiento del ratón mientras se pulsa el botón derecho
        if (Input.GetMouseButton(1))
        {
            rotY += Input.GetAxis("Mouse X") * sensibilidadRaton;
            rotX -= Input.GetAxis("Mouse Y") * sensibilidadRaton;
        }

        // Gestiona la rotación de la cámara mediante las teclas de dirección asignadas
        rotY += Input.GetKey(KeyCode.E) ? sensibilidadTeclado * Time.deltaTime : 0;
        rotY -= Input.GetKey(KeyCode.Q) ? sensibilidadTeclado * Time.deltaTime : 0;
        rotX -= Input.GetKey(KeyCode.R) ? sensibilidadTeclado * Time.deltaTime : 0;
        rotX += Input.GetKey(KeyCode.F) ? sensibilidadTeclado * Time.deltaTime : 0;

        // Actualiza la distancia del zoom utilizando la rueda del ratón
        distancia -= Input.GetAxis("Mouse ScrollWheel") * 5f;
        
        // Restringe los valores de distancia y ángulo dentro de los límites de seguridad
        distancia = Mathf.Clamp(distancia, zoomMin, zoomMax);
        rotX = Mathf.Clamp(rotX, alturaMin, alturaMax);

        // Calcula la posición final en el espacio tridimensional basándose en la rotación
        Quaternion rotacion = Quaternion.Euler(rotX, rotY, 0);
        transform.position = target.position + rotacion * new Vector3(0, 0, -distancia);
        
        // Enfoca la cámara ligeramente por encima de la base del objetivo para mejorar la visibilidad
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}
