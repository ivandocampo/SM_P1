using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    public GameObject pantallaVictoria;

    // Estado global del entorno
    public bool PartidaActiva { get; private set; } = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Invocado cuando Frodo alcanza la zona de salida portando el Anillo
    public void FrodoEscapa()
    {
        if (!PartidaActiva) return;
        PartidaActiva = false;
        if (pantallaVictoria != null) pantallaVictoria.SetActive(true);
    }

    // Invocado por el CerebroOrco cuando la distancia de captura se satisface
    public void FrodoCapturado()
    {
        if (!PartidaActiva) return;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Update()
    {
        if (!PartidaActiva && Input.GetKeyDown(KeyCode.Return))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}