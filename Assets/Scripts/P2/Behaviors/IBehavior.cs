public interface IBehavior
{
    void Iniciar(BeliefBase creencias, ActuadorMovimiento actuador);

    bool Ejecutar(BeliefBase creencias, ActuadorMovimiento actuador);

    void Detener(ActuadorMovimiento actuador);
}
