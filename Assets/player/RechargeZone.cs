using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RechargeZone : MonoBehaviour
{
    [Tooltip("Cuánta energía por segundo entrega esta zona. Si es 0, usa la recarga base del VacuumController.")]
    public float recargaPorSegundo = 20f;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true; // asegúrate de que sea Trigger
    }
}
