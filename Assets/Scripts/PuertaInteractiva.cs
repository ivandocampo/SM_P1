using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class PuertaInteractiva : MonoBehaviour
{
    public float speed;  // Velocidad de la rotación de la puerta
    public float angle; // Ángulo de apertura de la puerta
    public Vector3 direction = Vector3.up; // Dirección de la rotación (para abrir)

    public bool FrodoCerca;
    public bool abrir;
    private Transform puertaTransform;  // Para almacenar la referencia al objeto vacío

    // Start is called before the first frame update
    void Start()
    {
        puertaTransform = transform.parent;
        // Establece el ángulo inicial
        angle = puertaTransform.eulerAngles.y;
    }

    // Update is called once per frame
    void Update()
    {
        // Solo rota si la puerta no ha llegado al ángulo de apertura (80 grados)
        if (Mathf.Round(puertaTransform.eulerAngles.y) != angle)
        {
            puertaTransform.Rotate(direction * speed); // Rota la puerta
        }

        // Cuando Frodo está cerca y presiona P, la puerta se abre
        if (Input.GetKeyDown(KeyCode.X) && FrodoCerca && abrir == false)
        {
            // Establecer el ángulo objetivo (80 grados)
            angle = 270; 
            direction = Vector3.up;  // Definir la dirección de la rotación
            abrir=true;
        }
        else if (Input.GetKeyDown(KeyCode.X) && FrodoCerca && abrir)
        {
            angle = 0;
            direction = Vector3.down;
            abrir=false;
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            FrodoCerca = true; // Si Frodo está cerca, actualizamos el estado
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            FrodoCerca = false; // Si Frodo sale del área, actualizamos el estado
        }
    }
}