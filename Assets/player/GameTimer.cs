using UnityEngine;
using UnityEngine.UI;

public class GameTimer : MonoBehaviour
{
    [Header("Configuración del tiempo")]
    [Tooltip("Duración total de la partida en segundos.")]
    public float tiempoTotal = 120f;

    [Header("Referencias UI")]
    [Tooltip("Texto (Text o TextMeshProUGUI) que muestra el tiempo restante.")]
    public Text textoTiempo;  // si usas TextMeshPro, cámbialo a TMP_Text
    [Tooltip("Panel que se mostrará cuando se acabe el tiempo.")]
    public GameObject panelPerdiste;

    private float tiempoRestante;
    private bool terminado = false;

    void Start()
    {
        tiempoRestante = tiempoTotal;

        if (panelPerdiste != null)
            panelPerdiste.SetActive(false);
    }

    void Update()
    {
        if (terminado) return;

        tiempoRestante -= Time.deltaTime;

        if (textoTiempo != null)
        {
            int minutos = Mathf.FloorToInt(tiempoRestante / 60f);
            int segundos = Mathf.FloorToInt(tiempoRestante % 60f);
            textoTiempo.text = $"{minutos:00}:{segundos:00}";
        }

        if (tiempoRestante <= 0f)
        {
            tiempoRestante = 0f;
            TerminarJuego();
        }
    }

    void TerminarJuego()
    {
        terminado = true;

        if (panelPerdiste != null)
            panelPerdiste.SetActive(true);

        // Opcional: detener el tiempo del juego
        Time.timeScale = 0f;

        Debug.Log("⛔ Se acabó el tiempo — ¡Perdiste!");
    }

    // Llamar desde un botón de “Reintentar” si lo deseas
    public void ReiniciarNivel()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }
}
