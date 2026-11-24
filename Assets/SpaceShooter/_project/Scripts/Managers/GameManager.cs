using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    public event Action<GameState> GameStateChanged = delegate(GameState state) {  };

    public GameState GameState { get; private set; }
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
    }

    void SetGameState(GameState gameState)
    {
        if (gameState == GameState) return;
        GameState = gameState;
        GameStateChanged(gameState);
    }

    void OnEnable()
    {
        SetGameState(GameState.Patrol);
        MusicManager.Instance.PlayPatrolMusic();
    }

    void Update()
    {
        // Esc (volver a elegir prueba) lo gestiona DefensaMission.
        // F1 congelaba el juego sin forma de reanudar: se quitó.
        if (Input.GetKeyDown(KeyCode.C))
        {
            Cursor.visible = !Cursor.visible;
            Cursor.lockState = Cursor.visible ? CursorLockMode.None : CursorLockMode.Confined;
        }
    }

    public void InCombat(bool inCombat)
    {
        if (GameState == GameState.GameOver) return;
        if (inCombat)
        {
            if (GameState == GameState.Combat) return;
            MusicManager.Instance.PlayCombatMusic();
            SetGameState(GameState.Combat);
            return;
        }

        // Fin del combate: vuelve a la música de patrulla.
        if (GameState == GameState.Patrol) return;
        MusicManager.Instance.PlayPatrolMusic();
        SetGameState(GameState.Patrol);
    }

    public void PlayerWon()
    {
        MusicManager.Instance.PlayGameOverMusic();
        SetGameState(GameState.GameOver);    
    }
}