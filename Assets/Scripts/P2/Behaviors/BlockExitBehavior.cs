using UnityEngine;

[System.Serializable]
public class BlockExitBehavior : IBehavior
{
    private Transform puntoSalida;
    private Transform[] puntosBloqueo;
    private int indiceBloqueo = 0;

    public BlockExitBehavior(Transform salida, Transform[] puntosBloqueo)
    {
        this.puntoSalida = salida;
        this.puntosBloqueo = puntosBloqueo;
    }

    public void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (TienePuntosBloqueoValidos())
        {
            indiceBloqueo = BuscarSiguientePuntoValido(indiceBloqueo);
            actuador.SetDestino(puntosBloqueo[indiceBloqueo].position, TipoVelocidad.Alerta);
        }
        else if (puntoSalida != null)
        {
            actuador.SetDestino(puntoSalida.position, TipoVelocidad.Alerta);
        }
    }

    public bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador)
    {
        if (TienePuntosBloqueoValidos())
        {
            if (actuador.HaLlegado(0.5f))
            {
                indiceBloqueo = BuscarSiguientePuntoValido(indiceBloqueo + 1);
                actuador.SetDestino(puntosBloqueo[indiceBloqueo].position, TipoVelocidad.Alerta);
            }
        }
        else if (puntoSalida != null)
        {
            actuador.SetDestino(puntoSalida.position, TipoVelocidad.Alerta);
        }

        return false;
    }

    private bool TienePuntosBloqueoValidos()
    {
        if (puntosBloqueo == null || puntosBloqueo.Length == 0)
            return false;

        for (int i = 0; i < puntosBloqueo.Length; i++)
        {
            if (puntosBloqueo[i] != null)
                return true;
        }

        return false;
    }

    private int BuscarSiguientePuntoValido(int inicio)
    {
        for (int offset = 0; offset < puntosBloqueo.Length; offset++)
        {
            int indice = (inicio + offset) % puntosBloqueo.Length;
            if (puntosBloqueo[indice] != null)
                return indice;
        }

        return 0;
    }

    public void Detener(ActuadorMovimiento actuador) { }
}
