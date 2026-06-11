#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PhotonSettingsFinder
{
    [MenuItem("Tools/Photon/Find PhotonServerSettings in Project")]
    public static void Find()
    {
        string[] guids = AssetDatabase.FindAssets("t:PhotonServerSettings");
        if (guids.Length == 0)
        {
            Debug.LogWarning("PhotonServerSettings not found in project.");
            return;
        }

        Debug.Log($"Found {guids.Length} PhotonServerSettings asset(s):");
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            Debug.Log($" - {path}");
        }
    }
}
#endif