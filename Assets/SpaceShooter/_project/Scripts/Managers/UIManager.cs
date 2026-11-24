using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

// HUD del shooter (indicadores de objetivo y puntuación).
// En cooperativo hay uno por jugador: cada uno dibuja los indicadores con su cámara y su nave (Configure).
// Instance es siempre el del jugador 1.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public static readonly List<UIManager> All = new List<UIManager>();

    [SerializeField] TargetIndicator _targetIndicatorPrefab;
    [SerializeField] Canvas _mainCanvas;
    [SerializeField] TMP_Text _scoreText, _highScoreText;

    List<TargetIndicator> _targetIndicators;

    // Nave y cámara de este HUD (null = la nave con tag Player y Camera.main, como en individual).
    public Transform Owner { get; private set; }
    public Camera OwnerCamera { get; private set; }
    public Canvas MainCanvas => _mainCanvas;

    void Awake()
    {
        if (!Instance) Instance = this;
        if (!All.Contains(this)) All.Add(this);
        _targetIndicators ??= new List<TargetIndicator>();
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (Instance == this) Instance = All.Count > 0 ? All[0] : null;
    }

    void OnEnable()
    {
        SubscribeToEvents();
    }

    void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    void Start()
    {
        SubscribeToEvents();
        // Objetivos que ya existían antes de este HUD (el del jugador 2 se crea con la partida empezada).
        foreach (Targetable t in Targetable.Active)
            if (t) AddTarget(t.transform);
    }

    // Cooperativo: nave y cámara de este HUD. Los indicadores ya creados se actualizan.
    public void Configure(Transform owner, Camera ownerCamera)
    {
        Owner = owner;
        OwnerCamera = ownerCamera;
        if (_targetIndicators == null) return;
        foreach (TargetIndicator indicator in _targetIndicators)
            if (indicator) indicator.SetView(owner, ownerCamera);
    }

    public void AddTarget(Transform target)
    {
        _targetIndicators ??= new List<TargetIndicator>();
        var key = target.GetInstanceID();
        if (_targetIndicators.Any(i => i && i.Key == key)) return;
        var targetIndicator = Instantiate(_targetIndicatorPrefab, _mainCanvas.transform);
        targetIndicator.Init(target, _mainCanvas, Owner, OwnerCamera);
        _targetIndicators.Add(targetIndicator);
    }

    public void RemoveTarget(Transform target)
    {
        if (_targetIndicators == null) return;
        var key = target.GetInstanceID();
        var indicator = _targetIndicators.FirstOrDefault(i => i && i.Key == key);
        if (indicator)
        {
            _targetIndicators.Remove(indicator);
            Destroy(indicator.gameObject);
        }
    }

    public void UpdateTargetIndicators(List<Transform> targets, int lockedOnTarget)
    {
        foreach (var targetIndicator in _targetIndicators)
        {
            if (!targetIndicator) continue;
            targetIndicator.gameObject.SetActive(targets.Any(target => target.GetInstanceID() == targetIndicator.Key));
            targetIndicator.LockedOn = targetIndicator.Key == lockedOnTarget;
        }
    }

    void SubscribeToEvents()
    {
        SubscribeToScoreManagerEvents();
    }

    void UnsubscribeFromEvents()
    {
        UnsubscribeFromScoreManagerEvents();
    }

    void SubscribeToScoreManagerEvents()
    {
        if (!ScoreManager.Instance) return;
        UnsubscribeFromScoreManagerEvents();
        ScoreManager.Instance.ScoreChanged += OnScoreChanged;
        ScoreManager.Instance.HighScoreChanged += OnHighScoreChanged;
    }

    void UnsubscribeFromScoreManagerEvents()
    {
        if (!ScoreManager.Instance) return;
        ScoreManager.Instance.ScoreChanged -= OnScoreChanged;
        ScoreManager.Instance.HighScoreChanged -= OnHighScoreChanged;
    }

    void OnScoreChanged(int score)
    {
        _scoreText.text = score.ToString();
    }

    void OnHighScoreChanged(int highScore)
    {
        _highScoreText.text = highScore.ToString();
    }
}
