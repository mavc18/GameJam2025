using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("🕒 Tiempo de juego")]
    [Tooltip("Duración total del nivel en segundos.")]
    public float tiempoTotal = 180f;

    [Header("🧹 Basura a recolectar")]
    [Tooltip("Cantidad total de objetos de basura en el nivel.")]
    public int totalBasura = 20;
    private int basuraRecolectada = 0;

    [Header("📺 Referencias UI (TextMeshPro)")]
    public TMP_Text textoTiempo;   // ← ahora usa TMP
    public TMP_Text textoBasura;   // ← ahora usa TMP
    public GameObject panelGanaste;
    public GameObject panelPerdiste;

    [Header("🎵 Sonidos opcionales")]
    public AudioSource audioVictoria;
    public AudioSource audioDerrota;

    private float tiempoRestante;
    private bool juegoTerminado = false;

    void Start()
    {
        tiempoRestante = tiempoTotal;

        if (panelGanaste != null) panelGanaste.SetActive(false);
        if (panelPerdiste != null) panelPerdiste.SetActive(false);

        Time.timeScale = 1f;
        ActualizarUI();
    }

    void Update()
    {
        if (juegoTerminado) return;

        tiempoRestante -= Time.deltaTime;
        if (tiempoRestante < 0f) tiempoRestante = 0f;

        ActualizarUI();

        if (tiempoRestante <= 0f && !juegoTerminado)
            MostrarDerrota();
    }

    public void RegistrarBasuraRecolectada()
    {
        if (juegoTerminado) return;

        basuraRecolectada++;
        Debug.Log($"🧹 Basura recolectada: {basuraRecolectada}/{totalBasura}");

        ActualizarUI();

        if (basuraRecolectada >= totalBasura)
        {
            Debug.Log("🎉 Llamando a MostrarVictoria()");
            MostrarVictoria();
        }
    }

    


    private void MostrarVictoria()
    {
        juegoTerminado = true;
        if (audioVictoria) audioVictoria.Play();

        if (panelGanaste) panelGanaste.SetActive(true);
        if (panelPerdiste) panelPerdiste.SetActive(false);

        Time.timeScale = 0f;
        Debug.Log("🎉 ¡Ganaste! Has recolectado toda la basura.");
    }

    private void MostrarDerrota()
    {
        juegoTerminado = true;
        if (audioDerrota) audioDerrota.Play();

        if (panelPerdiste) panelPerdiste.SetActive(true);
        Time.timeScale = 0f;
        Debug.Log("⛔ Se acabó el tiempo — ¡Perdiste!");
    }

    public void ReiniciarNivel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void SalirDelJuego()
    {
        Debug.Log("👋 Saliendo del juego...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void ActualizarUI()
    {
        if (textoTiempo != null)
        {
            int minutos = Mathf.FloorToInt(tiempoRestante / 60f);
            int segundos = Mathf.FloorToInt(tiempoRestante % 60f);
            textoTiempo.text = $"{minutos:00}:{segundos:00}";
        }

        if (textoBasura != null)
        {
            textoBasura.text = $"{basuraRecolectada}/{totalBasura}";
        }
    }
}
