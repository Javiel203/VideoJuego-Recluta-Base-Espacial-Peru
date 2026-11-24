using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyShipManager : MonoBehaviour
{
    [SerializeField] GameObject[] _enemyShipPrefabs;
    [SerializeField] int _maxEnemies = 1;
    [Tooltip("Máximo de enemigos simultáneos al que se llega derribando naves (cada derribo suma uno).")]
    [SerializeField] int _maxEnemiesCap = 3;
    [SerializeField] float _firstSpawnInterval = 10f, _spawnInterval = 30f;
    [SerializeField] float _minSpawnRange = 500f, _maxSpawnRange = 2000f;

    List<GameObject> _enemyShips;
    float _spawnDelay;
    Transform _transform;
    int MaxEnemies { get; set; }
    float SpawnInterval { get; set; }
    int ActiveEnemies => _enemyShips.Count(e => e && e.activeSelf);

    bool CanSpawnEnemyShip
    {
        get
        {
            _spawnDelay -= Time.deltaTime;
            return _spawnDelay <= 0f && ActiveEnemies < MaxEnemies;
        }
    }

    GameObject RandomPrefab => _enemyShipPrefabs[Random.Range(0, _enemyShipPrefabs.Length)];

    void Awake()
    {
        _transform = transform;
        MaxEnemies = _maxEnemies;
        SpawnInterval = _spawnInterval;
    }

    void OnEnable()
    {
        _enemyShips = new List<GameObject>();
        _spawnDelay = _firstSpawnInterval;
    }

    void Update()
    {
        if (CanSpawnEnemyShip)
        {
            SpawnEnemyShip();
        }
    }

    void SpawnEnemyShip()
    {
        var spawnPosition = Random.insideUnitSphere * Random.Range(_minSpawnRange, _maxSpawnRange);
        var enemy = Instantiate(RandomPrefab, _transform);
        enemy.GetComponent<EnemyShipController>().ShipDestroyed.AddListener(OnShipDestroyed);
        _enemyShips.Add(enemy);
        enemy.transform.position = spawnPosition;
        _spawnDelay = SpawnInterval;
    }

    void OnShipDestroyed(int id)
    {
        for (var i = 0; i < _enemyShips.Count; ++i)
        {
            var ship = _enemyShips[i];
            if (!ship || ship.GetInstanceID() != id) continue;
            _enemyShips.RemoveAt(i);
            ship.GetComponent<EnemyShipController>().ShipDestroyed.RemoveListener(OnShipDestroyed);
            Destroy(ship);
            // Dificultad progresiva: cada nave derribada permite un enemigo simultáneo más (con tope).
            MaxEnemies = Mathf.Min(MaxEnemies + 1, Mathf.Max(_maxEnemies, _maxEnemiesCap));
            return;
        }
    }
}
