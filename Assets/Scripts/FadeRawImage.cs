using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeRawImage : MonoBehaviour
{
    [Tooltip("Reference to the DaydreamAPIManager to check streaming status")]
    public DaydreamAPIManager apiManager;

    void Start()
    {
        // Auto-find DaydreamAPIManager if not assigned
        if (apiManager == null)
        {
            apiManager = FindObjectOfType<DaydreamAPIManager>();
            if (apiManager == null)
            {
                Debug.LogWarning("[FadeRawImage] DaydreamAPIManager not found in scene.");
            }
        }

        StartCoroutine(WaitForStreamingRoutine());
    }

    IEnumerator WaitForStreamingRoutine()
    {
        if (apiManager == null)
        {
            Debug.LogWarning("[FadeRawImage] Cannot wait for streaming without DaydreamAPIManager reference.");
            yield break;
        }

        // Wait until streaming starts
        while (!apiManager.IsStreaming)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // Wait an extra second after connection is established
        yield return new WaitForSeconds(1f);

        Debug.Log("[FadeRawImage] Streaming started, deactivating GameObject.");
        this.gameObject.SetActive(false);
    }
}
