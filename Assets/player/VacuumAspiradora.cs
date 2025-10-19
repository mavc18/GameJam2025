using UnityEngine;

public class VacuumAspiradora : MonoBehaviour
{
    [Header("Referencias")]
    public PlayerControllerFPS_DualJoystick player; // arrastra tu Player
    public Transform boquilla;

    [Header("Geometría de succión")]
    public float rangoMax = 3f;
    [Range(1f, 89f)] public float anguloCono = 30f;
    public float radioCaptura = 0.25f;

    [Header("Física de succión")]
    public float fuerzaBase = 30f;
    public float masaMaxAspirable = 3f;
    public LayerMask capasAspirables;
    public LayerMask capasBloqueo;
    public bool requiereLineaDeVision = true;
    public bool destruirAlCapturar = true;
    public int maxObjetosPorFrame = 8;

    [Header("Debug")]
    public bool aspirandoActivo; // <- visible en inspector

    void Awake()
    {
        if (boquilla == null) boquilla = transform;
    }

    void FixedUpdate()
    {
        // sincroniza con el Player
        aspirandoActivo = (player != null && player.aspirando);

        if (!aspirandoActivo) return;

        Collider[] cols = Physics.OverlapSphere(boquilla.position, rangoMax, capasAspirables, QueryTriggerInteraction.Ignore);
        if (cols.Length == 0) return;

        int procesados = 0;
        Vector3 origen = boquilla.position;
        Vector3 forward = boquilla.forward;

        foreach (var col in cols)
        {
            if (procesados >= maxObjetosPorFrame) break;
            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb.mass > masaMaxAspirable) continue;

            Vector3 haciaBoquilla = origen - rb.worldCenterOfMass;
            float dist = haciaBoquilla.magnitude;
            if (dist < Mathf.Epsilon) continue;

            Vector3 dir = haciaBoquilla / dist;
            float ang = Vector3.Angle(forward, dir);
            if (ang > anguloCono) continue;

            if (requiereLineaDeVision)
            {
                if (Physics.Raycast(origen, (rb.worldCenterOfMass - origen).normalized, out RaycastHit hit, dist, capasBloqueo))
                    continue;
            }

            if (dist <= radioCaptura)
            {
                if (destruirAlCapturar)
                    Destroy(rb.gameObject);
                else
                {
                    rb.isKinematic = true;
                    rb.detectCollisions = false;
                    rb.gameObject.SetActive(false);
                }
                procesados++;
                continue;
            }

            float t = Mathf.Clamp01(dist / rangoMax);
            float falloff = 1f - (t * t);
            falloff = Mathf.Max(falloff, 0.05f);
            Vector3 fuerza = dir * (fuerzaBase * falloff);
            rb.AddForce(fuerza, ForceMode.Acceleration);
            procesados++;
        }

        // DEBUG opcional:
        // Debug.Log($"Aspirando {procesados} objetos");
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Transform b = boquilla != null ? boquilla : transform;
        Vector3 o = b.position;
        Vector3 f = b.forward;

        Gizmos.color = new Color(0f, 0.6f, 1f, 0.25f);
        int seg = 16;
        float rad = Mathf.Tan(anguloCono * Mathf.Deg2Rad) * rangoMax;
        Vector3 right = Vector3.Cross(f, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.0001f) right = Vector3.Cross(f, Vector3.right).normalized;

        for (int i = 0; i < seg; i++)
        {
            float a0 = (i / (float)seg) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)seg) * Mathf.PI * 2f;
            Vector3 r0 = (Quaternion.AngleAxis(Mathf.Rad2Deg * a0, f) * right) * rad;
            Vector3 r1 = (Quaternion.AngleAxis(Mathf.Rad2Deg * a1, f) * right) * rad;
            Vector3 p0 = o + f * rangoMax + r0;
            Vector3 p1 = o + f * rangoMax + r1;
            Gizmos.DrawLine(o, p0);
            Gizmos.DrawLine(p0, p1);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(o, radioCaptura);
    }
#endif
}
