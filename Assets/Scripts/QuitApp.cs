using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class QuitApp : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("Quiting the application");
            Application.Quit();
        }
    }
}
