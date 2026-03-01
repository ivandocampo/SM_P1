using UnityEngine;

public class SensorOidoOrco : MonoBehaviour
{
    [Header("Audición - Frodo")]
    public Transform objetivoFrodo;
    public float rangoOidoCaminar = 5f;
    public float rangoOidoCorrer = 15f;

    private CerebroFrodo cerebroFrodo;
    private ActuadorMovimientoFrodo movimientoFrodo;

    void Start()
    {
        if (objetivoFrodo != null)
        {
            cerebroFrodo = objetivoFrodo.GetComponent<CerebroFrodo>();
            movimientoFrodo = objetivoFrodo.GetComponent<ActuadorMovimientoFrodo>();
        }
    }

    public bool OirFrodo(out Vector3 posicionRuido)
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

        return false;
    }

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