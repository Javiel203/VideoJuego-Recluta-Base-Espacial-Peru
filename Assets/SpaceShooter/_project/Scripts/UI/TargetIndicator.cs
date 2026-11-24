using System;
using UnityEngine;
using UnityEngine.UI;

public class TargetIndicator : MonoBehaviour
{
    [SerializeField] Image _targetBox, _lockOnImage, _offScreenImage;
    [SerializeField] GameObject _targetLeadIndicatorPrefab;
    [SerializeField] float _offScreenMargin = 45f, _projectileSpeed = 800f;

    Transform _target;
    Canvas _mainCanvas;
    Camera _mainCamera;
    RectTransform _rectTransform, _canvasRect;
    
    Rigidbody _targetRigidbody;
    Transform _player;
    GameObject _targetLeadIndicator;

    public int Key { get; private set; }
    public bool LockedOn { get; set; }

    // owner / ownerCamera: nave y cámara del jugador de este HUD (null = nave con tag Player y Camera.main).
    public void Init(Transform target, Canvas mainCanvas, Transform owner = null, Camera ownerCamera = null)
    {
        _target = target;
        Key = _target.GetInstanceID();
        _mainCanvas = mainCanvas;
        _canvasRect = _mainCanvas.GetComponent<RectTransform>();
        _targetLeadIndicator = Instantiate(_targetLeadIndicatorPrefab, _mainCanvas.transform);
        _targetRigidbody = _target.GetComponent<Rigidbody>();
        SetView(owner, ownerCamera);
    }

    public void SetView(Transform owner, Camera ownerCamera)
    {
        _player = owner;
        if (!_player)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go) _player = go.transform;
        }
        _mainCamera = ownerCamera ? ownerCamera : Camera.main;
    }

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    void OnDestroy()
    {
        if (!_targetLeadIndicator) return;
        _targetLeadIndicator.SetActive(false);
        GameObject.Destroy(_targetLeadIndicator);
    }

    // Punto del visor de la cámara (0..1) → posición local en el canvas (centro = 0,0).
    // Funciona con el canvas a pantalla completa (individual) y con media pantalla (cooperativo).
    Vector3 ViewportToCanvas(Vector3 viewport)
    {
        Rect rect = _canvasRect.rect;
        return new Vector3((viewport.x - 0.5f) * rect.width, (viewport.y - 0.5f) * rect.height, 0f);
    }

    void LateUpdate()
    {
        if (!_target || !_mainCamera) return;
        // Get normalized position of target
        Vector3 targetViewportPos = _mainCamera.WorldToViewportPoint(_target.position);
        
        // Display target reticle on target
        if (TargetIsVisible(targetViewportPos))
        {
            DisplayOnScreenReticle(targetViewportPos);
            UpdateTargetLeadingIndicator();
            return;
        }

        _targetLeadIndicator.SetActive(false);
        DisplayOffScreenReticle(targetViewportPos);
    }

    void UpdateTargetLeadingIndicator()
    {
        var predictedPosition = CalculatePredictedPosition();
        DisplayTargetLeadIndicator(predictedPosition);
    }

    Vector3 CalculatePredictedPosition()
    {
        var interceptPosition = _target.position;

        // Relative position and velocity
        if (!_player || !_targetRigidbody) return interceptPosition;
        var relativePosition = interceptPosition - _player.position;
        var relativeVelocity = _targetRigidbody.linearVelocity;

        // Coefficients for the quadratic equation
        var a = Vector3.Dot(relativeVelocity, relativeVelocity) - _projectileSpeed * _projectileSpeed;
        var b = 2 * Vector3.Dot(relativeVelocity, relativePosition);
        var c = Vector3.Dot(relativePosition, relativePosition);

        // Check for near-zero 'a' to avoid numerical instability
        if (Mathf.Abs(a) < Mathf.Epsilon)
        {
            return interceptPosition; // Return current position if 'a' is effectively zero
        }

        // Calculate the discriminant of the quadratic equation
        var discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            return interceptPosition; // No real solution, return current position
        }

        // Calculate the two possible times to impact
        var sqrtDiscriminant = Mathf.Sqrt(discriminant);
        var time1 = (-b - sqrtDiscriminant) / (2 * a);
        var time2 = (-b + sqrtDiscriminant) / (2 * a);

        // We want the positive time to impact (the future)
        var impactTime = Mathf.Max(time1, time2);
        if (impactTime < Mathf.Epsilon)
        {
            return interceptPosition; // No valid future impact time, return current position
        }

        // Calculate the intercept position
        interceptPosition += _targetRigidbody.linearVelocity * impactTime;

        return interceptPosition;    
    }

    void DisplayTargetLeadIndicator(Vector3 targetPosition)
    {
        var targetViewportPos = _mainCamera.WorldToViewportPoint(targetPosition);
        _targetLeadIndicator.SetActive(TargetIsVisible(targetViewportPos));
        if (!_targetLeadIndicator.activeSelf) return;
        _targetLeadIndicator.transform.localPosition = ViewportToCanvas(targetViewportPos);
    }

    bool TargetIsVisible(Vector3 position)
    {
        return (position.x is >= 0 and <= 1 && position.y is >= 0 and <= 1 && position.z >= 0);
    }    
  
    void DisplayOnScreenReticle(Vector3 targetViewportPos)
    {
        _rectTransform.localPosition = ViewportToCanvas(targetViewportPos);
        _rectTransform.localEulerAngles = Vector3.zero;
        _targetBox.enabled = !LockedOn;
        _lockOnImage.enabled = LockedOn;
        _offScreenImage.enabled = false;
    }
    
    void DisplayOffScreenReticle(Vector3 targetViewportPos)
    {
        _targetBox.enabled = false;
        _lockOnImage.enabled = false;

        // Todo en coordenadas locales del canvas, con el centro de la vista en (0,0).
        Rect rect = _canvasRect.rect;
        float halfWidth = rect.width * 0.5f - _offScreenMargin;
        float halfHeight = rect.height * 0.5f - _offScreenMargin;
        Vector3 indicatorPosition = ViewportToCanvas(targetViewportPos) * Mathf.Sign(targetViewportPos.z);

        float x = halfWidth / Mathf.Max(Mathf.Abs(indicatorPosition.x), 0.0001f);
        float y = halfHeight / Mathf.Max(Mathf.Abs(indicatorPosition.y), 0.0001f);

        if (x < y)
        {
            float angle = Vector3.SignedAngle(Vector3.right, indicatorPosition, Vector3.forward);
            indicatorPosition.x = Mathf.Sign(indicatorPosition.x) * halfWidth;
            indicatorPosition.y = Mathf.Tan(Mathf.Deg2Rad * angle) * indicatorPosition.x;
        }
        else
        {
            float angle = Vector3.SignedAngle(Vector3.up, indicatorPosition, Vector3.forward);
            indicatorPosition.y = Mathf.Sign(indicatorPosition.y) * halfHeight;
            indicatorPosition.x = -Mathf.Tan(Mathf.Deg2Rad * angle) * indicatorPosition.y;
        }

        _rectTransform.localPosition = indicatorPosition;
        _rectTransform.localEulerAngles = new Vector3(0f, 0f, Vector3.SignedAngle(Vector3.up, indicatorPosition, Vector3.forward));
        _offScreenImage.enabled = true;
    }
}
