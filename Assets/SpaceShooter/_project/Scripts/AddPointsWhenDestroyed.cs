using UnityEngine;

// Puntos al destruir este objeto en combate (asteroide fracturado o nave enemiga derribada).
// Antes sumaba en OnDisable/OnDestroy, así que al descargar la escena (recargar tras perder la nave)
// los miles de asteroides daban puntos e inflaban el récord. Ahora lo llaman Asteroid y ShipController.
public class AddPointsWhenDestroyed : MonoBehaviour
{
    [SerializeField] int _points = 20;

    bool _scored;

    public void AddScore()
    {
        if (_scored) return;
        _scored = true;
        if (ScoreManager.Instance) ScoreManager.Instance.AddPoints(_points);
    }
}
