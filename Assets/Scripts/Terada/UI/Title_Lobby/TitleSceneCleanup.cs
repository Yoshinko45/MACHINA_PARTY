using UnityEngine;

public class TitleSceneCleanup : MonoBehaviour
{
    [SerializeField] Camera titleCamera;
    [SerializeField] GameObject titleCanvas;

    void Start()
    {
        ConnectionManager.Instance.OnStateChanged += HandleStateChanged;
    }

    void OnDestroy()
    {
        if (ConnectionManager.Instance == null) return;
        ConnectionManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    void HandleStateChanged(ConnectionState state)
    {
        if (state != ConnectionState.InRoom) return;

        if (titleCamera != null)
            titleCamera.gameObject.SetActive(false);

        if (titleCanvas != null)
            titleCanvas.SetActive(false);
    }
}