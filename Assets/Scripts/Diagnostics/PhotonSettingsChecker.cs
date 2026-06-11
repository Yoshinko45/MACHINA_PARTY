using UnityEngine;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

public class PhotonSettingsChecker : MonoBehaviour
{
    [Header("Debug: ã≠êßÉäÅ[ÉWÉáÉì (ãÛï∂éöÇ≈ñ≥å¯)")]
    public string forceRegion = "jp";

    void Start()
    {
#if PHOTON_UNITY_NETWORKING
        if (PhotonNetwork.PhotonServerSettings != null)
        {
            var app = PhotonNetwork.PhotonServerSettings.AppSettings;
            Debug.Log($"[PhotonSettingsChecker] AppIdRealtime: {app.AppIdRealtime}");
            Debug.Log($"[PhotonSettingsChecker] FixedRegion (before): '{app.FixedRegion}'");

            if (!string.IsNullOrEmpty(forceRegion))
            {
                app.FixedRegion = forceRegion;
                Debug.Log($"[PhotonSettingsChecker] FixedRegion forced to '{forceRegion}'");
            }

            Debug.Log($"[PhotonSettingsChecker] FixedRegion (after): '{app.FixedRegion}'");
        }
        else
        {
            Debug.LogWarning("[PhotonSettingsChecker] PhotonServerSettings is null (asset not found in Resources?)");
        }
#else
        Debug.LogWarning("[PhotonSettingsChecker] PHOTON_UNITY_NETWORKING define is not present. Cannot access PhotonServerSettings.");
#endif
    }
}