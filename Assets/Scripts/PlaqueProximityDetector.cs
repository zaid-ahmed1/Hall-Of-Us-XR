using UnityEngine;

public class PlaqueProximityDetector : MonoBehaviour
{
    [Header("Settings")]
    private UnityEngine.UI.Image plaqueImage;
    private bool isPlayerNear = false;
    
    void Start()
    {
        // Find the plaque image component
        Transform canvas = transform.Find("Canvas");
        if (canvas != null)
        {
            Transform imageTransform = canvas.Find("Image");
            if (imageTransform != null)
            {
                plaqueImage = imageTransform.GetComponent<UnityEngine.UI.Image>();
                if (plaqueImage != null)
                {
                    plaqueImage.gameObject.SetActive(false); // Start hidden
                }
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.name.Contains("CenterEyeAnchor"))
        {
            ShowPlaque();
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HidePlaque();
        }
    }
    
    void ShowPlaque()
    {
        if (plaqueImage != null && !isPlayerNear)
        {
            plaqueImage.gameObject.SetActive(true);
            isPlayerNear = true;
            Debug.Log("Player entered plaque area - showing plaque");
        }
    }
    
    void HidePlaque()
    {
        if (plaqueImage != null && isPlayerNear)
        {
            plaqueImage.gameObject.SetActive(false);
            isPlayerNear = false;
            Debug.Log("Player left plaque area - hiding plaque");
        }
    }
}
