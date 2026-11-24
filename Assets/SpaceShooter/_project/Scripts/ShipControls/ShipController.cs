using System;
using System.Collections.Generic;
using UnityEngine;

public class ShipController : MonoBehaviour
{
    // Se lanza cuando una nave (jugador o enemiga) es destruida en combate.
    public static event Action<ShipController> ShipKilled;

    // Naves de jugador activas (1 en individual, 2 en cooperativo). Las usa la IA enemiga para elegir objetivo.
    public static readonly List<ShipController> Players = new List<ShipController>();

    [SerializeField] Shield _shield;
    [SerializeField]
    protected MovementControlsBase _movementControls;

    [SerializeField]
    protected WeaponControlsBase _weaponControls;    

    [SerializeField]
    ShipDataSo _shipData;
    
    [SerializeField]
    List<ShipEngine> _engines;

    [SerializeField]
    List<Blaster> _blasters;

    [SerializeField] protected List<MissileLauncher> _missileLaunchers;
    
    [SerializeField]
    private AnimateCockpitControls _cockpitAnimationControls;
    
    Rigidbody _rigidBody;
    [Range(-1f, 1f)]
    float _pitchAmount, _rollAmount, _yawAmount = 0f;

    protected DamageHandler _damageHandler;

    IMovementControls MovementInput => _movementControls;
    IWeaponControls WeaponInput => _weaponControls;

    void Awake()
    {
        _rigidBody = GetComponent<Rigidbody>();
        _damageHandler = GetComponent<DamageHandler>();
    }
    void Start()
    {
        foreach (ShipEngine engine in _engines)
        {
            engine.Init(MovementInput, _rigidBody, _shipData.ThrustForce / _engines.Count);
        }

        foreach (Blaster blaster in _blasters)
        {
            blaster.Init(WeaponInput, _shipData.BlasterCooldown, _shipData.BlasterLaunchForce, _shipData.BlasterProjectileDuration, _shipData.BlasterDamage, _rigidBody);
        }

        foreach (MissileLauncher launcher in _missileLaunchers)
        {
            launcher.Init(WeaponInput);
        }

        if (_cockpitAnimationControls != null)
        {
            _cockpitAnimationControls.Init(MovementInput);
        }

        if (_shield)
        {
            _shield.Init(_shipData.ShieldStrength);
        }
    }

    public virtual void OnEnable()
    {
        if (CompareTag("Player") && !Players.Contains(this)) Players.Add(this);
        if (_damageHandler == null) return;
        _damageHandler.Init(_shipData.MaxHealth);
        _damageHandler.HealthChanged.AddListener(OnHealthChanged);
        _damageHandler.ObjectDestroyed.AddListener(DestroyShip);
    }

    protected virtual void OnDisable()
    {
        Players.Remove(this);
        if (_damageHandler == null) return;
        _damageHandler.HealthChanged.RemoveListener(OnHealthChanged);
        _damageHandler.ObjectDestroyed.RemoveListener(DestroyShip);
    }

    // Cooperativo: la nave del jugador 2 usa sus propios controles. Llamar antes de que la nave se active (Start).
    public void SetControls(MovementControlsBase movement, WeaponControlsBase weapons)
    {
        _movementControls = movement;
        _weaponControls = weapons;
    }

    public virtual void Update()
    {
        _rollAmount = MovementInput.RollAmount;
        _yawAmount = MovementInput.YawAmount;
        _pitchAmount = MovementInput.PitchAmount;
    }

    void FixedUpdate()
    {
        if (!Mathf.Approximately(0f, _pitchAmount))
        {
            _rigidBody.AddTorque(transform.right * (_shipData.PitchForce * _pitchAmount * Time.fixedDeltaTime));
        }

        if (!Mathf.Approximately(0f, _rollAmount))
        {
            _rigidBody.AddTorque(transform.forward * (_shipData.RollForce * _rollAmount * Time.fixedDeltaTime));
        }

        if (!Mathf.Approximately(0f, _yawAmount))
        {
            _rigidBody.AddTorque(transform.up * (_yawAmount * _shipData.YawForce * Time.fixedDeltaTime));
        }
    }
    
    void DestroyShip()
    {
        ShipKilled?.Invoke(this);
        if (TryGetComponent(out AddPointsWhenDestroyed points)) points.AddScore();
        gameObject.SetActive(false);
    }

    void OnHealthChanged()
    {
    }


}
