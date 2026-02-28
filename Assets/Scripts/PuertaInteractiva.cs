using UnityEngine;

public class PuertaInteractiva : MonoBehaviour
{
    [Header("Configuración de la Puerta")]
    public float speed = 150f; // Importante: pon un valor alto (ej: 150) porque ahora son grados por segundo
    public float anguloAbierta = 270f; 
    private float anguloCerrada; 

    private bool frodoCerca = false;
    private bool estaAbierta = false;
    private Transform puertaTransform;
    private float anguloObjetivo;

    void Start()
    {
        puertaTransform = transform.parent;
        // Guardamos el ángulo inicial tal y como esté en el mapa para que vuelva a su sitio exacto
        anguloCerrada = puertaTransform.localEulerAngles.y; 
        anguloObjetivo = anguloCerrada;
    }

    void Update()
    {
        // Detectar pulsación de X solo si Frodo está en la zona
        if (frodoCerca && Input.GetKeyDown(KeyCode.X))
        {
            estaAbierta = !estaAbierta; // Cambia el estado (si estaba abierta se cierra y viceversa)
            anguloObjetivo = estaAbierta ? anguloAbierta : anguloCerrada;
        }

        // Rotar suavemente la puerta hacia el ángulo que toque en este momento
        Quaternion rotacionObjetivo = Quaternion.Euler(0, anguloObjetivo, 0);
        puertaTransform.localRotation = Quaternion.RotateTowards(
            puertaTransform.localRotation, 
            rotacionObjetivo, 
            speed * Time.deltaTime
        );
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            frodoCerca = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            frodoCerca = false;
        }
    }
}