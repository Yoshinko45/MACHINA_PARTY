using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tanks.Complete;

public class GameCameraSetup : MonoBehaviour
{
    [SerializeField]
    CameraControl cameraControl;

    Coroutine setupCoroutine;

    bool gameCameraSetup;

    void OnEnable()
    {
        gameCameraSetup = false;

        setupCoroutine =
            StartCoroutine(
                SetupWhenGameStarts());
    }

    void OnDisable()
    {
        if (setupCoroutine != null)
        {
            StopCoroutine(
                setupCoroutine);

            setupCoroutine = null;
        }
    }

    IEnumerator SetupWhenGameStarts()
    {
        while (true)
        {
            yield return null;

            if (RoomState.Instance == null)
                continue;

            if (!RoomState.Instance.GameStarted)
            {
                gameCameraSetup = false;
                continue;
            }

            if (gameCameraSetup)
                continue;

            if (TrySetup())
            {
                gameCameraSetup = true;

                Debug.Log(
                    "[GameCameraSetup] " +
                    "Game camera setup complete.");
            }
        }
    }

    bool TrySetup()
    {
        if (cameraControl == null)
            return false;

        List<Transform> targets =
            new List<Transform>();

        foreach (var networkPlayer in
            FindObjectsByType<NetworkPlayer>(
                FindObjectsSortMode.None))
        {
            if (networkPlayer.VehicleObject ==
                null)
            {
                continue;
            }

            targets.Add(
                networkPlayer.VehicleObject
                    .transform);
        }

        if (targets.Count == 0)
            return false;

        cameraControl.m_Targets =
            targets.ToArray();

        cameraControl.SetStartPositionAndSize();

        Debug.Log(
            $"[GameCameraSetup] " +
            $"Camera targets={targets.Count}");

        return true;
    }
}