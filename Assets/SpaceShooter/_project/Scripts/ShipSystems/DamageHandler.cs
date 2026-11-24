using UnityEngine;
using UnityEngine.Events;

public class DamageHandler : MonoBehaviour, IDamageable
{
    [SerializeField] GameObject _explosionPrefab;
    UnityEvent _healthChangedEvent;
    UnityEvent _objectDestroyedEvent;
    public int MaxHealth { get; private set; }
    public int Health { get; private set; }
    // Multiplicador del daño recibido (la misión final lo baja en Fácil/Intermedio para la nave del jugador).
    public float DamageMultiplier { get; set; } = 1f;
    // Hasta este instante no recibe daño (cooperativo: protección al reaparecer).
    public float InvulnerableUntil { get; set; }

    public UnityEvent HealthChanged => _healthChangedEvent ??= new UnityEvent();
    public UnityEvent ObjectDestroyed => _objectDestroyedEvent ??= new UnityEvent();

    public void Init(int maxHealth)
    {
        Health = MaxHealth = maxHealth;
        HealthChanged.Invoke();
    }

    public void TakeDamage(int damage, Vector3 hitPosition)
    {
        // Ya destruido: evita contar dos veces la misma nave (p. ej. láser y misil en el mismo frame).
        if (Health <= 0) return;
        if (Time.time < InvulnerableUntil) return;
        if (!Mathf.Approximately(DamageMultiplier, 1f) && damage > 0)
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * DamageMultiplier));
        Health -= damage;
        HealthChanged.Invoke();
        if (Health > 0) return;
        if (_explosionPrefab)
        {
            Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
        }
        ObjectDestroyed.Invoke();
    }
}
