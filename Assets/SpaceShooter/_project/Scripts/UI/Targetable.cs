using System.Collections.Generic;
using UnityEngine;

// Objetivo que aparece en el HUD (indicador). En cooperativo se avisa a los HUD de los dos jugadores.
public class Targetable : MonoBehaviour
{
    public static readonly HashSet<Targetable> Active = new HashSet<Targetable>();

    void OnEnable()
    {
        Active.Add(this);
        foreach (UIManager ui in UIManager.All)
            if (ui) ui.AddTarget(transform);
    }

    void OnDisable()
    {
        RemoveTarget();
    }

    void OnDestroy()
    {
        RemoveTarget();
    }

    void RemoveTarget()
    {
        Active.Remove(this);
        foreach (UIManager ui in UIManager.All)
            if (ui) ui.RemoveTarget(transform);
    }
}
