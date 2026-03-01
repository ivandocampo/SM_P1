using UnityEngine;
using UnityEngine.AI;

public class SensorOidoOrco : MonoBehaviour
{
    [Header("Audición - Frodo")]
    public Transform objetivoFrodo;
    public float rangoOidoCaminar = 5f;
    public float rangoOidoCorrer = 15f;

    [Header("Audición - Otros Orcos")]
    public float rangoOidoOrcoPersiguiendo = 12f;
    public float rangoOidoOrcoPatrullando = 4f;
    public float umbralVelocidadPersecucion = 5f;

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

        otrosOrcos = FindObjectsByType<CerebroOrco>(FindObjectsSortMode.None);
    }

    public bool OirRuido(out Vector3 posicionRuido)
    {
        posicionRuido = Vector3.zero;

        if (objetivoFrodo != null && cerebroFrodo != null && movimientoFrodo != null)
        {
            float distanciaFrodo = Vector3.Distance(transform.position, objetivoFrodo.position);
            float velocidadFrodo = movimientoFrodo.VelocidadActual();

            if (cerebroFrodo.estaCorriendo && distanciaFrodo < rangoOidoCorrer)
            {
                posicionRuido = objetivoFrodo.position;
                return true;
            }
            if (!cerebroFrodo.estaCorriendo && velocidadFrodo > 0.5f && distanciaFrodo < rangoOidoCaminar)
            {
                posicionRuido = objetivoFrodo.position;
                return true;
            }
        }

        foreach (CerebroOrco compañero in otrosOrcos)
        {
            if (compañero.gameObject == this.gameObject) continue;

            float distanciaOrco = Vector3.Distance(transform.position, compañero.transform.position);
            NavMeshAgent agenteCompañero = compañero.GetComponent<NavMeshAgent>();
            if (agenteCompañero == null) continue;

            float velocidadCompañero = agenteCompañero.velocity.magnitude;

            if (velocidadCompañero > umbralVelocidadPersecucion && distanciaOrco < rangoOidoOrcoPersiguiendo)
            {
                posicionRuido = compañero.transform.position;
                return true;
            }
            if (velocidadCompañero > 0.5f && velocidadCompañero <= umbralVelocidadPersecucion && distanciaOrco < rangoOidoOrcoPatrullando)
            {
                posicionRuido = compañero.transform.position;
                return true;
            }
        }

        return false;
    }

    // ==================== GIZMOS ====================
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, rangoOidoCorrer);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, rangoOidoCorrer);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, rangoOidoCaminar);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, rangoOidoCaminar);
    }
}