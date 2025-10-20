using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DepositZone : MonoBehaviour
{
    [Tooltip("Manager del nivel que recibe la entrega de basura.")]
    public LevelGameManager manager;

    [Header("Feedback opcional")]
    public AudioSource sfxDeposito;
    public ParticleSystem vfxDeposito;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var vacuum = other.GetComponentInParent<VacuumController>();
        if (!vacuum) return;

        // Vaciar contenedor de la aspiradora
        int entregadas = vacuum.VaciarContenedorInterno();
        if (entregadas <= 0) return;

        if (sfxDeposito) sfxDeposito.Play();
        if (vfxDeposito) vfxDeposito.Play();

        // Avisar al manager
        if (manager) manager.RegistrarEntrega(entregadas);
    }
}
