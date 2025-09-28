using System;
using UnityEngine;

public class PlaqueProximityDetector : MonoBehaviour
{
    [Header("Settings")]
    private UnityEngine.UI.Image plaqueImage;
    private bool isPlayerNear = false;
    
    [Header("Debug Visualization")]
    public bool showTriggerZone = false;
    public Material wireframeMaterial;
    
    private GameObject visualZone;
    private BoxCollider triggerCollider;
    
    void Start()
    {
        triggerCollider = GetComponent<BoxCollider>();
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
        CreateWireframeVisualization();
    }

    private void Update()
    {
        showTriggerZone = !showTriggerZone;
        if (visualZone != null)
            visualZone.SetActive(showTriggerZone);
    }

    void CreateWireframeVisualization()
    {
        // Create a wireframe cube
        visualZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualZone.name = "TriggerZone_Visual";
        visualZone.transform.SetParent(transform);
        
        // Match the trigger collider size and position
        visualZone.transform.localPosition = triggerCollider.center;
        visualZone.transform.localScale = triggerCollider.size;
        
        // Remove the collider (we just want visual)
        Destroy(visualZone.GetComponent<BoxCollider>());
        
        // Make it wireframe
        Renderer renderer = visualZone.GetComponent<Renderer>();
        if (wireframeMaterial != null)
        {
            renderer.material = wireframeMaterial;
        }
        else
        {
            // Create simple transparent material
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(0, 1, 0, 0.3f); // Transparent green
            renderer.material = mat;
        }
        
        visualZone.SetActive(showTriggerZone);
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
