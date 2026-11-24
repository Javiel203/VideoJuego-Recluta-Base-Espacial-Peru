using System;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

public class EnemyShipController : ShipController
{
    [SerializeField] float _patrolRange = 2000f, _attackRange = 1000f;
    [SerializeField] LayerMask _targetMask, _playerMask;

    enum EnemyShipState
    {
        None,
        Patrol,
        Attack,
        Reposition,
        Retreat
    };

    AIShipMovementControls _aiShipMovementControls;
    AIShipWeaponControls _aiShipWeaponControls;
    EnemyShipState _state = EnemyShipState.None;
    Transform _transform;

    // Jugador objetivo en caché: el más cercano de los vivos (en cooperativo hay dos).
    // Es null mientras no hay ninguna nave de jugador activa.
    const float RetargetInterval = 2f;
    Transform _player;
    float _nextRetarget;
    Transform PlayerShip
    {
        get
        {
            if (!_player || !_player.gameObject.activeInHierarchy || Time.time >= _nextRetarget)
            {
                _player = NearestPlayer();
                _nextRetarget = Time.time + RetargetInterval;
            }
            return _player && _player.gameObject.activeInHierarchy ? _player : null;
        }
    }

    Transform NearestPlayer()
    {
        Transform best = null;
        float bestDistance = float.MaxValue;
        Vector3 me = _transform ? _transform.position : transform.position;
        foreach (ShipController ship in Players)
        {
            if (!ship || !ship.gameObject.activeInHierarchy) continue;
            float d = (ship.transform.position - me).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = ship.transform;
            }
        }
        if (best) return best;
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        return go && go.activeInHierarchy ? go.transform : null;
    }

    Transform _target;
    // Destino creado por esta nave (patrulla, reposición, retirada); se destruye al cambiar de destino.
    GameObject _ownedTarget;

    public UnityEvent<int> ShipDestroyed = new UnityEvent<int>();

    #region Public data for debugging

    public string ShipState => _state.ToString();
    public string TargetName => _target ? _target.name : "none";

    public string DistanceToTarget
    {
        get
        {
            string distance = String.Empty;
            if (_target)
            {
                distance = $"{Vector3.Distance(_target.position, _transform.position):F2}";
            }
            return distance;
        }
    }

    public string HealthLevel => $"{_damageHandler.Health}/{_damageHandler.MaxHealth}";

    #endregion

    bool InAttackRange
    {
        get
        {
            Transform player = PlayerShip;
            return player && Vector3.Distance(player.position, _transform.position) <= _attackRange;
        }
    }

    bool ShouldRetreat => _damageHandler.Health < (_damageHandler.MaxHealth * 0.33f);
    bool ReachedPatrolTarget => !_target || Vector3.Distance(_target.position, _transform.position) < 0.15f;

    bool ShouldReposition => Physics.SphereCast(_transform.position, 3f, _transform.forward,
        out var hit, 100f, _playerMask);

    public float VectorDifference
    {
        get
        {
            Transform player = PlayerShip;
            return player ? (player.forward - _transform.forward).magnitude : 0f;
        }
    }

    public override void OnEnable()
    {
        _transform = transform;
        _aiShipMovementControls = (AIShipMovementControls)_movementControls;
        _aiShipWeaponControls = (AIShipWeaponControls)_weaponControls;
        SetState(EnemyShipState.Patrol);
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // Se envía el id del GameObject: es el que compara EnemyShipManager.
        ShipDestroyed.Invoke(gameObject.GetInstanceID());
    }

    void OnDestroy()
    {
        if (_ownedTarget) Destroy(_ownedTarget);
    }

    public override void Update()
    {
        EnemyShipState state = GetNextState();
        SetState(state);
        // Cooperativo: si el jugador más cercano cambió en pleno ataque, cambia de objetivo.
        if (_state == EnemyShipState.Attack && PlayerShip && _target != PlayerShip)
        {
            SetMovementTarget(PlayerShip);
            SetWeaponsTarget(_target, _attackRange, _targetMask);
        }
        base.Update();
    }

    EnemyShipState GetNextState()
    {
        EnemyShipState newState = _state switch
        {
            EnemyShipState.Patrol => Patrol(),
            EnemyShipState.Attack => Attack(),
            EnemyShipState.Reposition => Reposition(),
            EnemyShipState.Retreat => Retreat(),
            _ => EnemyShipState.None
        };
        return newState;
    }

    EnemyShipState Patrol()
    {
        if (ShouldRetreat) return EnemyShipState.Retreat;
        if (InAttackRange) return EnemyShipState.Attack;
        if (ReachedPatrolTarget && _target)
        {
            _target.position = Random.insideUnitSphere * _patrolRange;
        }

        return EnemyShipState.Patrol;
    }

    EnemyShipState Attack()
    {
        if (ShouldRetreat) return EnemyShipState.Retreat;
        // Jugador destruido: vuelve a patrullar.
        if (!PlayerShip) return EnemyShipState.Patrol;
        return ShouldReposition ? EnemyShipState.Reposition : EnemyShipState.Attack;
    }

    EnemyShipState Reposition()
    {
        if (ShouldRetreat) return EnemyShipState.Retreat;
        if (!_target) return EnemyShipState.Patrol;
        return Vector3.Distance(_target.position, _transform.position) < 100f
            ? EnemyShipState.Attack
            : EnemyShipState.Reposition;
    }

    EnemyShipState Retreat()
    {
        return EnemyShipState.Retreat;
    }

    void SetState(EnemyShipState state)
    {
        if (_state == state) return;
        // Sin jugador no se puede atacar.
        if (state == EnemyShipState.Attack && !PlayerShip) state = EnemyShipState.Patrol;
        if (_state == state) return;
        _state = state;
        switch (state)
        {
            case EnemyShipState.Patrol:
                SetMovementTarget(CreateTarget("Patrol Target", Random.insideUnitSphere * _patrolRange));
                SetWeaponsTarget(null, 0, 0);
                break;
            case EnemyShipState.Attack:
                SetMovementTarget(PlayerShip);
                SetWeaponsTarget(_target, _attackRange, _targetMask);
                break;
            case EnemyShipState.Reposition:
                SetMovementTarget(GetRepositionTarget());
                SetWeaponsTarget(null, 0, 0);
                break;
            case EnemyShipState.Retreat:
                SetMovementTarget(GetRetreatTarget());
                SetWeaponsTarget(null, 0, 0);
                break;
        }
    }

    // Cambia el destino del movimiento y destruye el destino temporal anterior.
    void SetMovementTarget(Transform target)
    {
        if (_ownedTarget && (!target || _ownedTarget != target.gameObject))
        {
            Destroy(_ownedTarget);
            _ownedTarget = null;
        }
        _target = target;
        _aiShipMovementControls.SetTarget(_target);
    }

    Transform CreateTarget(string targetName, Vector3 position)
    {
        var target = new GameObject(targetName).transform;
        target.position = position;
        if (_ownedTarget && _ownedTarget != target.gameObject) Destroy(_ownedTarget);
        _ownedTarget = target.gameObject;
        return target;
    }

    Transform GetRetreatTarget()
    {
        // Se aleja del jugador (o hacia atrás si el jugador no existe).
        Transform player = PlayerShip;
        Vector3 away = player
            ? (_transform.position - player.position).normalized
            : -_transform.forward;
        return CreateTarget("Retreat target", _transform.position + away * 5000f);
    }

    Transform GetRepositionTarget()
    {
        // Random.Range con enteros excluye el máximo: 1..4 (antes nunca salía "abajo").
        var rand = Random.Range(1, 5);
        var right = _transform.right;
        var up = _transform.up;
        var direction = rand switch
        {
            1 => right,
            2 => right * -1,
            3 => up,
            4 => up * -1,
            _ => _transform.forward * -1
        };
        return CreateTarget("Reposition Target", _transform.position + direction * 250f);
    }

    void SetWeaponsTarget(Transform target, float attackRange, int targetMask)
    {
        foreach (MissileLauncher launcher in _missileLaunchers)
        {
            launcher.SetTarget(target);
        }

        _aiShipWeaponControls.SetTarget(target, attackRange, targetMask);
    }
}
