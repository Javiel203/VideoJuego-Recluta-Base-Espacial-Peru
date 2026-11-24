using UnityEngine;

public class Managers : MonoBehaviour
{
    static Managers _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            // Antes usaba DontDestroyOnLoad: ahora la escena es un minijuego dentro de RECLUTA,
            // así que los managers deben desaparecer al volver a los menús.
            _instance = this;
        }
    }
}
