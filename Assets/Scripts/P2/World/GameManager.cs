// =============================================================
// Gestor global del estado de la partida.
// Controla si la partida sigue activa, muestra la pantalla de victoria
// cuando Frodo escapa y reinicia la escena cuando es capturado.
// Se expone como singleton para que otros scripts puedan notificar
// eventos principales de juego
// =============================================================

using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // Proporciona un punto de acceso global a la instancia única del gestor
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public GameObject pantallaVictoria;

    // Registra si el flujo de la partida se encuentra en curso o detenido
    public bool PartidaActiva { get; private set; } = true;

    // Configura el patrón Singleton al inicializar el objeto para evitar duplicados
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Gestiona la condición de victoria cuando el objetivo alcanza la zona de escape
    public void FrodoEscapa()
    {
        // Interrumpe la ejecución si la partida ya ha finalizado previamente
        if (!PartidaActiva) return;
        
        // Cambia el estado global y activa los elementos visuales de victoria
        PartidaActiva = false;
        if (pantallaVictoria != null) pantallaVictoria.SetActive(true);
    }

    // Gestiona la condición de derrota reiniciando la escena tras la captura
    public void FrodoCapturado()
    {
        if (!PartidaActiva) return;
        
        // Carga de nuevo la escena actual para reiniciar el nivel
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Monitoriza la entrada del teclado para reiniciar el juego tras finalizar
    void Update()
    {
        // Permite reiniciar la escena al pulsar la tecla Enter si la partida ha concluido
        if (!PartidaActiva && Input.GetKeyDown(KeyCode.Return))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
