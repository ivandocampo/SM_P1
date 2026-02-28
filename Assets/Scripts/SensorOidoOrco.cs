using UnityEngine;
using UnityEngine.AI;

public class SensorOidoOrco : MonoBehaviour
{
    [Header("Audición - Frodo")]
    public Transform objetivoFrodo;
    public float rangoOidoCaminar = 5f;       // Frodo caminando: ruido bajo, rango corto
    public float rangoOidoCorrer = 15f;       // Frodo corriendo: ruido alto, rango largo

    [Header("Audición - Otros Orcos")]
    public float rangoOidoOrcoPersiguiendo = 12f;   // Un orco persiguiendo hace mucho ruido
    public float rangoOidoOrcoPatrullando = 4f;     // Un orco patrullando hace poco ruido
    public float umbralVelocidadPersecucion = 5f;   // Por encima de esta velocidad se considera "persiguiendo"

    private CerebroFrodo cerebroFrodo;
    private ActuadorMovimientoFrodo movimientoFrodo;
    private CerebroOrco[] otrosOrcos;

    void Start()
    {
        if (objetivoFrodo != null)
        {
            cerebroFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
            movimientoFrodo = objetivoFrodo.GetComponent<ActuadorMovimientoFrodo>();
        }

        otrosOrcos = FindObjectsOfType<CerebroOrco>();
    }

    // Evalúa si percibe CUALQUIER ruido (Frodo moviéndose u otro Orco)
    public bool OirRuido(out Vector3 posicionRuido)
    {
        posicionRuido = Vector3.zero;

        // 1. Intentar escuchar a Frodo
        if (objetivoFrodo != null && cerebroFrodo != null && movimientoFrodo != null)
        {
            float distanciaFrodo = Vector3.Distance(transform.position, objetivoFrodo.position);
            float velocidadFrodo = movimientoFrodo.VelocidadActual();

            // Frodo corriendo: se oye desde lejos
            if (cerebroFrodo.estaCorriendo && distanciaFrodo < rangoOidoCorrer)
            {
                posicionRuido = objetivoFrodo.position;
                return true;
            }
            // Frodo caminando (moviéndose pero sin correr): se oye de cerca
            if (!cerebroFrodo.estaCorriendo && velocidadFrodo > 0.5f && distanciaFrodo < rangoOidoCaminar)
            {
                posicionRuido = objetivoFrodo.position;
                return true;
            }
            // Frodo quieto: silencio total, no se detecta
        }

        // 2. Escuchar a otros Orcos (Mecánica de confusión)
        foreach (CerebroOrco compañero in otrosOrcos)
        {
            if (compañero.gameObject == this.gameObject) continue;

            float distanciaOrco = Vector3.Distance(transform.position, compañero.transform.position);
            NavMeshAgent agenteCompañero = compañero.GetComponent<NavMeshAgent>();
            if (agenteCompañero == null) continue;

            float velocidadCompañero = agenteCompañero.velocity.magnitude;

            // Orco persiguiendo (velocidad alta): mucho ruido, se oye desde lejos
            if (velocidadCompañero > umbralVelocidadPersecucion && distanciaOrco < rangoOidoOrcoPersiguiendo)
            {
                posicionRuido = compañero.transform.position;
                return true;
            }
            // Orco patrullando (velocidad baja pero moviéndose): poco ruido, solo de cerca
            if (velocidadCompañero > 0.5f && velocidadCompañero <= umbralVelocidadPersecucion && distanciaOrco < rangoOidoOrcoPatrullando)
            {
                posicionRuido = compañero.transform.position;
                return true;
            }
        }

        return false;
    }
}