using UnityEngine;

public class VacuumCarryCounter : MonoBehaviour
{
    [Header("Referencias")]
    public VacuumController vacuum;

    [Header("Estado (solo lectura)")]
    [SerializeField] private int carriedCount = 0;
    public int CarriedCount => carriedCount;

    void Reset()
    {
        if (!vacuum) vacuum = GetComponentInChildren<VacuumController>() ?? FindObjectOfType<VacuumController>();
    }

    void OnEnable()
    {
        if (!vacuum) vacuum = GetComponentInChildren<VacuumController>() ?? FindObjectOfType<VacuumController>();
        if (vacuum != null)
        {
            vacuum.OnCapturadoMicro.AddListener(OnPickedMicro);
            vacuum.OnCapturadoNormal.AddListener(OnPickedNormal);
        }
    }

    void OnDisable()
    {
        if (vacuum != null)
        {
            vacuum.OnCapturadoMicro.RemoveListener(OnPickedMicro);
            vacuum.OnCapturadoNormal.RemoveListener(OnPickedNormal);
        }
    }

    private void OnPickedMicro(int _totalMicroSoFar)
    {
        carriedCount += 1;
    }

    private void OnPickedNormal(GameObject _storedGO)
    {
        carriedCount += 1;
    }

    /// <summary>Entrega todo lo que llevas y devuelve cuántas unidades depositaste.</summary>
    public int DepositAll()
    {
        int n = carriedCount;
        carriedCount = 0;
        return n;
    }
}
