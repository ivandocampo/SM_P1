// =============================================================
// Interfaz común de todos los behaviors del guardia.
// GuardBehaviorController llama a Iniciar al activar el behavior,
// a Ejecutar cada frame (devuelve true cuando termina) y a Detener
// al desactivarlo. Permite intercambiar behaviors sin acoplar al agente
// =============================================================

public interface IBehavior
{
    void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador);

    bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador);

    void Detener(ActuadorMovimiento actuador);
}
