using UnityEngine;
using TMPro;

public class LevelGameManager : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("Total de basura a depositar para ganar.")]
    public int objetivoBasura = 20;

    [Header("Tiempo")]
    [Tooltip("Segundos de duración del nivel.")]
    public float tiempoLimiteSeg = 180f;
    public bool iniciarAutomaticamente = true;

    [Header("UI (opcional)")]
    public TMP_Text textoTimer;          // "MM:SS"
    public TMP_Text textoProgreso;       // "Recolectado: 7 / 20"
    public GameObject panelGanaste;
    public GameObject panelPerdiste;

    [Header("Control del juego")]
    public bool pausarTimeScaleAlFinal = true;

    // Estado
    private int _depositado = 0;
    private float _tiempoRestante;
    private bool _corriendo = false;
    private bool _terminado = false;

    void Start()
    {
        _tiempoRestante = Mathf.Max(0f, tiempoLimiteSeg);
        _corriendo = iniciarAutomaticamente;

        ActualizarUI();
        OcultarPaneles();
    }

    void Update()
    {
        if (_terminado || !_corriendo) return;

        _tiempoRestante -= Time.deltaTime;
        if (_tiempoRestante <= 0f)
        {
            _tiempoRestante = 0f;
            // Solo perder si NO hemos ganado antes
            if (!_terminado)
            {
                Perder();
            }
        }

        ActualizarUI();
    }

    public void RegistrarEntrega(int cantidad)
    {
        if (_terminado) return;

        _depositado += Mathf.Max(0, cantidad);
        ActualizarUI();

        if (_depositado >= objetivoBasura)
        {
            Ganar();
        }
    }

    public void Iniciar()  { if (!_terminado) _corriendo = true; }
    public void Pausar()   { _corriendo = false; }
    public void ReiniciarTiempo(float nuevoTiempoSeg)
    {
        _tiempoRestante = Mathf.Max(0f, nuevoTiempoSeg);
        _terminado = false;
        _corriendo = true;
        OcultarPaneles();
        ActualizarUI();
    }

    private void Ganar()
    {
        if (_terminado) return;
        _terminado = true;
        _corriendo = false;

        if (panelGanaste) panelGanaste.SetActive(true);
        if (panelPerdiste) panelPerdiste.SetActive(false);

        if (pausarTimeScaleAlFinal) Time.timeScale = 0f;
    }

    private void Perder()
    {
        if (_terminado) return; // si ya ganamos, no mostramos derrota
        _terminado = true;
        _corriendo = false;

        if (panelPerdiste) panelPerdiste.SetActive(true);
        if (panelGanaste) panelGanaste.SetActive(false);

        if (pausarTimeScaleAlFinal) Time.timeScale = 0f;
    }

    private void ActualizarUI()
    {
        if (textoTimer) textoTimer.text = FormatearTiempo(_tiempoRestante);
        if (textoProgreso) textoProgreso.text = $"Recolectado: {_depositado} / {objetivoBasura}";
    }

    private void OcultarPaneles()
    {
        if (panelGanaste) panelGanaste.SetActive(false);
        if (panelPerdiste) panelPerdiste.SetActive(false);
    }

    private string FormatearTiempo(float segundos)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(segundos + 0.5f));
        int min = s / 60;
        int sec = s % 60;
        return $"{min:00}:{sec:00}";
    }
}
