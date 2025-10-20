using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RechargeZone : MonoBehaviour
{
    [Header("Recarga de energía (unidades por segundo, mismas que VacuumController)")]
    [Tooltip("Ej: 20 significa +20 unidades por segundo. Si es 0 o negativo, usará la recarga base del VacuumController.")]
    public float recargaPorSegundo = 20f;

    [Tooltip("Si es true, recarga mientras el jugador permanezca dentro. Si es false, recarga una sola vez al entrar (instantánea).")]
    public bool recargaContinua = true;

    [Header("Feedback opcional")]
    public ParticleSystem efectoRecarga;
    public AudioSource sonidoRecarga;

    private VacuumController vacuum;
    private bool jugadorDentro = false;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true; // Trigger obligatorio
    }

    void OnTriggerEnter(Collider other)
    {
        var v = other.GetComponentInParent<VacuumController>();
        if (!v) return;

        vacuum = v;
        jugadorDentro = true;

        if (!recargaContinua)
        {
            // Recarga instantánea en unidades absolutas
            float cantidad = (recargaPorSegundo > 0f) ? recargaPorSegundo : vacuum.recargaBasePorSeg;
            vacuum.ModEnergia(cantidad); // UNA sola vez al entrar
        }

        if (efectoRecarga && !efectoRecarga.isPlaying) efectoRecarga.Play();
        if (sonidoRecarga && !sonidoRecarga.isPlaying) sonidoRecarga.Play();
    }

    void OnTriggerExit(Collider other)
    {
        if (vacuum && other.GetComponentInParent<VacuumController>() == vacuum)
        {
            jugadorDentro = false;
            if (efectoRecarga && efectoRecarga.isPlaying) efectoRecarga.Stop();
            if (sonidoRecarga && sonidoRecarga.isPlaying)   sonidoRecarga.Stop();
        }
    }

    void Update()
    {
        if (!recargaContinua || !jugadorDentro || vacuum == null) return;

        // Recarga continua en unidades por segundo (absolutas)
        float porSegundo = (recargaPorSegundo > 0f) ? recargaPorSegundo : vacuum.recargaBasePorSeg;
        float delta = porSegundo * Time.deltaTime;
        vacuum.ModEnergia(delta);
    }
}
