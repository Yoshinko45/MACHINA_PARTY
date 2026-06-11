#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class FindPhotonFusionAssets
{
    [MenuItem("Tools/Photon/Find Fusion/Photon Assets")]
    public static void Find()
    {
        string[] keywords = new[] { "Photon", "Fusion", "PhotonAppSettings", "PhotonServerSettings", "FusionAppSettings" };
        var guids = AssetDatabase.FindAssets(string.Join(" OR ", keywords));
        if (guids.Length == 0)
        {
            Debug.LogWarning("No Photon/Fusion related assets found by name search. Scanning all assets for 'Photon'/'Fusion' in path...");
            guids = AssetDatabase.FindAssets("");
        }

        int count = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (path.IndexOf("Photon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Fusion", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.Log($"[FindPhotonFusionAssets] {path}");
                count++;
            }
        }

        if (count == 0)
            Debug.LogWarning("[FindPhotonFusionAssets] No assets with 'Photon' or 'Fusion' found in path.");
        else
            Debug.Log($"[FindPhotonFusionAssets] Found {count} asset(s) with 'Photon' or 'Fusion' in path.");
    }
}
#endif