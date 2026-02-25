using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class PuertaInteractiva : MonoBehaviour
{
    [Header("Configuración")]
    public Transform puertaVisual;
    public float anguloAbierta = 90f;
    public float velocidadApertura = 2f;
    public bool aperturaLenta = false;

    [Header("Ruido")]
    public float radioRuido = 12f;

    private bool abierta = false;
    private bool frodoCerca = false;
    private bool animando = false;

    private NavMeshObstacle obstacle;

    void Start()
    {
        obstacle = GetComponent<NavMeshObstacle>();
    }

    void Update()
    {
        if (frodoCerca && Input.GetKeyDown(KeyCode.Space) && !animando)
        {
            StartCoroutine(MoverPuerta());
        }
    }

    IEnumerator MoverPuerta()
    {
        animando = true;

        float anguloInicial = puertaVisual.localEulerAngles.y;
        float anguloFinal = abierta ? 0f : anguloAbierta;

        if (!abierta && obstacle != null)
            obstacle.enabled = false;

        if (aperturaLenta)
        {
            float t = 0;
            while (t < 1)
            {
                t += Time.deltaTime * velocidadApertura;
                float angulo = Mathf.LerpAngle(anguloInicial, anguloFinal, t);
                puertaVisual.localEulerAngles = new Vector3(0, angulo, 0);
                yield return null;
            }
        }
        else
        {
            puertaVisual.localEulerAngles = new Vector3(0, anguloFinal, 0);
        }

        abierta = !abierta;

        if (!abierta && obstacle != null)
            obstacle.enabled = true;

        GenerarRuido();

        animando = false;
    }

    void GenerarRuido()
    {
        CerebroOrco[] orcos = FindObjectsByType<CerebroOrco>(FindObjectsSortMode.None);

        foreach (var orco in orcos)
        {
            float dist = Vector3.Distance(transform.position, orco.transform.position);

            if (dist < radioRuido)
            {
                orco.SendMessage("InvestigarPuerta", transform.position, SendMessageOptions.DontRequireReceiver);
            }
        }
    }


    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<CerebroFrodo>() != null)
            frodoCerca = false;
    }

    void OnGUI()
    {
        if (frodoCerca)
        {
            GUI.Label(new Rect(Screen.width / 2 - 70, Screen.height - 80, 200, 30), "Pulsa SPACE para abrir");
        }
    }
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("Entró algo al trigger: " + other.name);
        if (other.GetComponent<CerebroFrodo>() != null)
        {
            Debug.Log("¡Frodo detectado!");
            frodoCerca = true;
        }
    }

}