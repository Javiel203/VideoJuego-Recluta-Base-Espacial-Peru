using System;
using System.Collections;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    public event Action<int> ScoreChanged = delegate(int i) {  };
    public event Action<int> HighScoreChanged = delegate(int i) {  };
    
    public int Score { get; private set; }
    public int HighScore { get; private set; }

    const string HighScoreKey = "Defensa_HighScore";

    public void ResetScore()
    {
        Score = 0;
        ScoreChanged(Score);
    }

    public void AddPoints(int points)
    {
        Score += points;
        ScoreChanged(Score);
        if (Score <= HighScore) return;
        HighScore = Score;
        PlayerPrefs.SetInt(HighScoreKey, HighScore);
        HighScoreChanged(HighScore);
    }
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    // Muestra el récord guardado cuando la UI ya está suscrita.
    IEnumerator Start()
    {
        yield return null;
        HighScoreChanged(HighScore);
    }

    void OnDestroy()
    {
        PlayerPrefs.Save();
    }
}
