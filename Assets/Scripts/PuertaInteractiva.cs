using UnityEngine;

public class PuertaInteractiva : MonoBehaviour
{
    [Header("Configuración de la Puerta")]
    public float speed = 150f; 
    public float anguloAbierta = 270f; 
    private float anguloCerrada; 

    private bool frodoCerca = false;
    private bool estaAbierta = false;
    private Transform puertaTransform;
    private float anguloObjetivo;

    // Registra la rotación inicial y la referencia al objeto raíz de la puerta
    void Start()
    {
        puertaTransform = transform.parent;
        
        // Almacena el ángulo de cierre basado en la orientación original en la escena
        anguloCerrada = puertaTransform.localEulerAngles.y; 
        anguloObjetivo = anguloCerrada;
    }

    // Gestiona la interacción del usuario y ejecuta la animación de rotación
    void Update()
    {
        // Detecta la pulsación de la tecla de interacción si el personaje está en el rango
        if (frodoCerca && Input.GetKeyDown(KeyCode.X))
        {
            // Invierte el estado actual de la puerta y actualiza el ángulo de destino
            estaAbierta = !estaAbierta; 
            anguloObjetivo = estaAbierta ? anguloAbierta : anguloCerrada;
        }

        // Realiza una rotación progresiva hacia el ángulo objetivo definido
        Quaternion rotacionObjetivo = Quaternion.Euler(0, anguloObjetivo, 0);
        puertaTransform.localRotation = Quaternion.RotateTowards(
            puertaTransform.localRotation, 
            rotacionObjetivo, 
            speed * Time.deltaTime
        );
    }

    // Activa la posibilidad de interacción cuando el personaje entra en el área
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            frodoCerca = true;
        }
    }

    // Desactiva la posibilidad de interacción cuando el personaje sale del área
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            frodoCerca = false;
        }
    }
}