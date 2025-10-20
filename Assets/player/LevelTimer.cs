using UnityEngine;
using TMPro;

public class LevelTimer : MonoBehaviour
{
    [Header("Tiempo")]
    public float tiempoInicialSegundos = 180f;
    public bool iniciarAutomaticamente = true;

    [Header("Referencias")]
    public LevelState level;
    public TMP_Text textoTiempo; // opcional, para mostrar mm:ss

    private float t;
    private bool running;

    void Start()
    {
        t = Mathf.Max(0f, tiempoInicialSegundos);
        running = iniciarAutomaticamente;
        Pintar();
    }

    void Update()
    {
        if (!running || level == null || level.IsEnded) return;

        t -= Time.deltaTime;
        if (t <= 0f)
        {
            t = 0f;
            running = false;
            // Si no se ha ganado, perder
            if (!level.IsWon)
                level.Lose();
        }
        Pintar();
    }

    public void StartTimer()  { if (!level || level.IsEnded) return; running = true; }
    public void PauseTimer()  { running = false; }
    public void ResetTimer()  { running = false; t = tiempoInicialSegundos; Pintar(); }

    private void Pintar()
    {
        if (!textoTiempo) return;
        int s = Mathf.CeilToInt(t);
        int mm = s / 60;
        int ss = s % 60;
        textoTiempo.text = $"{mm:00}:{ss:00}";
    }
}
