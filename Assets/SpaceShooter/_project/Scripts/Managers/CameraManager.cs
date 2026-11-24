using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

public class CameraManager : MonoBehaviour
{
    enum VirtualCameras
    {
        NoSelection = -1,
        CockpitCamera = 0,
        FollowCamera = 1,
        EnemyFollowCamera = 2,
    }

    [SerializeField]
    List<CinemachineVirtualCameraBase> _virtualCameras;

    // Teclas de cada cámara (cabina, seguimiento, enemigo). El jugador 2 usa 8 / 9 / 0 en cooperativo.
    public KeyCode[] Keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

    public Transform ActiveCamera { get; private set; }
    public UnityEvent ActiveCameraChanged;
    
    VirtualCameras CameraKeyPressed
    {
        get
        {
            for (int i = 0; i < _virtualCameras.Count && i < Keys.Length; ++i)
            {
                if (Input.GetKeyDown(Keys[i])) return (VirtualCameras)i;
            }

            return VirtualCameras.NoSelection;
        }
    }

    void Awake()
    {
        ActiveCameraChanged = new UnityEvent();
    }

    void Start()
    {
        SetActiveCamera(VirtualCameras.CockpitCamera);
    }


    void Update()
    {
        SetActiveCamera(CameraKeyPressed);
    }
    
    void SetActiveCamera(VirtualCameras selectedCamera)
    {
        if (selectedCamera == VirtualCameras.NoSelection)
        {
            return;
        }

        VirtualCameras camIndex = VirtualCameras.CockpitCamera;
        foreach (var cam in _virtualCameras)
        {
            if (camIndex++ == selectedCamera)
            {
                cam.gameObject.SetActive(true);
                ActiveCamera = cam.transform;
                ActiveCameraChanged.Invoke();
            }
            else
            {
                cam.gameObject.SetActive(false);
            }
        }
    }

}
