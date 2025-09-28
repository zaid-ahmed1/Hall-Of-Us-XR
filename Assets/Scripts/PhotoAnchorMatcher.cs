using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PhotoAnchorMatcher : MonoBehaviour
{
    [Header("References")]
    public PhotoManager photoManager;
    public SpatialAnchorManager spatialAnchorManager;
    public S3 s3Manager;
    
    [Header("Settings")]
    public bool autoMatchWhenReady = true;
    
    void Start()
    {
        if (autoMatchWhenReady)
        {
            StartCoroutine(WaitForBothSystemsReady());
        }
    }
    
    private IEnumerator WaitForBothSystemsReady()
    {
        Debug.Log("Waiting for S3 downloads and photo data to complete...");
        
        // Wait until both systems are ready
        while (!IsS3Ready() || !IsPhotosReady())
        {
            yield return new WaitForSeconds(0.5f); // Check every half second
        }
        
        Debug.Log("Both S3 and Photos are ready! Starting photo-anchor matching...");
        MatchPhotosToAnchors();
    }
    
    private bool IsS3Ready()
    {
        return s3Manager != null && s3Manager.IsReady;
    }
    
    private bool IsPhotosReady()
    {
        return photoManager != null && 
               photoManager.photos != null && 
               photoManager.photos.Count > 0;
    }
    
    [ContextMenu("Match Photos to Anchors")]
    public void MatchPhotosToAnchors()
    {
        if (photoManager == null || photoManager.photos == null || photoManager.photos.Count == 0)
        {
            Debug.LogWarning("No photos available to match!");
            return;
        }
        
        Debug.Log($"Starting to match {photoManager.photos.Count} photos to spatial anchors...");
        
        int matchedCount = 0;
        HashSet<GameObject> usedAnchors = new HashSet<GameObject>(); // Track used anchors
        
        foreach (Photo photo in photoManager.photos)
        {
            string firstTag = GetFirstTag(photo.tags);
            
            if (string.IsNullOrEmpty(firstTag))
            {
                Debug.LogWarning($"Photo {photo.filename} has no valid tags, skipping...");
                continue;
            }
            
            GameObject anchorObject = FindUnusedAnchorByTag(firstTag, usedAnchors);
            
            if (anchorObject != null)
            {
                bool textSuccess = UpdateAnchorText(anchorObject, firstTag);
                bool imageSuccess = UpdateAnchorImage(anchorObject, photo);
                
                if (textSuccess || imageSuccess)
                {
                    usedAnchors.Add(anchorObject); // Mark this anchor as used
                    matchedCount++;
                    Debug.Log($"✅ Matched photo '{photo.filename}' with tag '{firstTag}' to anchor '{anchorObject.name}' (Text: {textSuccess}, Image: {imageSuccess})");
                }
            }
            else
            {
                Debug.LogWarning($"❌ No available anchor found for tag '{firstTag}' from photo '{photo.filename}' (may be all used)");
            }
        }
        
        Debug.Log($"Matching complete! Successfully matched {matchedCount} photos to anchors.");
    }
    
    private string GetFirstTag(string tags)
    {
        if (string.IsNullOrEmpty(tags))
            return null;
            
        // Split by common delimiters and get first non-empty tag
        string[] tagArray = tags.Split(new char[] { ',', ';', '|', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        return tagArray.Length > 0 ? tagArray[0].Trim() : null;
    }
    
    private GameObject FindUnusedAnchorByTag(string tag, HashSet<GameObject> usedAnchors)
    {
        // Find all GameObjects in the scene that match the tag
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        List<GameObject> matchingAnchors = new List<GameObject>();
        
        foreach (GameObject obj in allObjects)
        {
            // Check if the object name contains the tag (case insensitive)
            if (obj.name.ToLower().Contains(tag.ToLower()))
            {
                // Verify it's actually a spatial anchor and not already used
                if (obj.GetComponent<OVRSpatialAnchor>() != null && !usedAnchors.Contains(obj))
                {
                    // Skip preview objects (they have parents, real anchors don't)
                    if (obj.transform.parent == null)
                    {
                        matchingAnchors.Add(obj);
                    }
                    else
                    {
                        Debug.Log($"Skipping preview object: {obj.name} (has parent: {obj.transform.parent.name})");
                    }
                }
            }
        }
        
        // Return the first unused matching anchor
        if (matchingAnchors.Count > 0)
        {
            Debug.Log($"Found {matchingAnchors.Count} unused anchors for tag '{tag}', using: {matchingAnchors[0].name}");
            return matchingAnchors[0];
        }
        
        return null;
    }
    
    // Helper method to manually trigger matching from other scripts
    public void TriggerMatching()
    {
        MatchPhotosToAnchors();
    }
    
    // Method to match a specific photo to anchors
    public bool MatchSpecificPhoto(Photo photo)
    {
        string firstTag = GetFirstTag(photo.tags);
        
        if (string.IsNullOrEmpty(firstTag))
            return false;
            
        // For single photo matching, we don't track used anchors (use first match)
        GameObject anchorObject = FindUnusedAnchorByTag(firstTag, new HashSet<GameObject>());
        
        if (anchorObject != null)
        {
            bool textSuccess = UpdateAnchorText(anchorObject, firstTag);
            bool imageSuccess = UpdateAnchorImage(anchorObject, photo);
            return textSuccess || imageSuccess;
        }
        
        return false;
    }
    
    private bool UpdateAnchorText(GameObject anchorObject, string tagText)
    {
        // Look for TextMeshProUGUI components (UI Text)
        TextMeshProUGUI[] textComponents = anchorObject.GetComponentsInChildren<TextMeshProUGUI>();
        
        if (textComponents.Length > 0)
        {
            // Update the first text component found
            textComponents[0].text = tagText;
            Debug.Log($"Updated TextMeshProUGUI text to '{tagText}' on anchor '{anchorObject.name}'");
            return true;
        }
        
        // Look for TextMeshPro components (3D Text)
        TextMeshPro[] textMeshComponents = anchorObject.GetComponentsInChildren<TextMeshPro>();
        
        if (textMeshComponents.Length > 0)
        {
            // Update the first text component found
            textMeshComponents[0].text = tagText;
            Debug.Log($"Updated TextMeshPro text to '{tagText}' on anchor '{anchorObject.name}'");
            return true;
        }
        
        // Fallback to legacy Text component
        UnityEngine.UI.Text[] legacyTextComponents = anchorObject.GetComponentsInChildren<UnityEngine.UI.Text>();
        
        if (legacyTextComponents.Length > 0)
        {
            legacyTextComponents[0].text = tagText;
            Debug.Log($"Updated legacy Text to '{tagText}' on anchor '{anchorObject.name}'");
            return true;
        }
        
        Debug.LogWarning($"No text component found on anchor '{anchorObject.name}' or its children!");
        return false;
    }
    
    private bool UpdateAnchorImage(GameObject anchorObject, Photo photo)
    {
        // Find the PictureRender child object
        Transform pictureRenderTransform = FindChildByName(anchorObject.transform, "PictureRender");
        
        if (pictureRenderTransform == null)
        {
            Debug.LogWarning($"No 'PictureRender' child found on anchor '{anchorObject.name}'");
            return false;
        }
        
        // Get the renderer component
        Renderer renderer = pictureRenderTransform.GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning($"No Renderer component found on 'PictureRender' child of anchor '{anchorObject.name}'");
            return false;
        }
        
        // Get the local file path from S3 manager
        string localImagePath = s3Manager.GetLocalPathForPhoto(photo);
        if (string.IsNullOrEmpty(localImagePath) || !File.Exists(localImagePath))
        {
            Debug.LogWarning($"Image file not found for photo '{photo.filename}' at path: {localImagePath}");
            return false;
        }
        
        // Load and apply the texture
        return LoadAndApplyTexture(renderer, localImagePath, photo.filename);
    }
    
    private bool LoadAndApplyTexture(Renderer renderer, string imagePath, string filename)
    {
        try
        {
            // Read the image file as bytes
            byte[] imageData = File.ReadAllBytes(imagePath);
            
            // Create a new texture
            Texture2D texture = new Texture2D(2, 2); // Size will be overridden by LoadImage
            
            // Load the image data into the texture
            if (texture.LoadImage(imageData))
            {
                // Apply to material - try common base map property names
                Material material = renderer.material;
                
                // Try URP/HDRP base map first
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                    Debug.Log($"✅ Applied texture '{filename}' to _BaseMap property");
                    return true;
                }
                // Try standard/builtin pipeline main texture
                else if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", texture);
                    Debug.Log($"✅ Applied texture '{filename}' to _MainTex property");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"Material on PictureRender doesn't have _BaseMap or _MainTex property.");
                    #if UNITY_EDITOR
                    // Debug log available properties (Editor only)
                    var shader = material.shader;
                    for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
                    {
                        if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                        {
                            Debug.Log($"  Available texture property: {ShaderUtil.GetPropertyName(shader, i)}");
                        }
                    }
                    #endif
                    return false;
                }
            }
            else
            {
                Debug.LogError($"Failed to load image data for '{filename}'");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading texture for '{filename}': {e.Message}");
            return false;
        }
    }
    
    private Transform FindChildByName(Transform parent, string childName)
    {
        // Search all descendants recursively (breadth-first to find closest matches first)
        Queue<Transform> searchQueue = new Queue<Transform>();
        searchQueue.Enqueue(parent);
        
        while (searchQueue.Count > 0)
        {
            Transform current = searchQueue.Dequeue();
            
            // Check if this transform matches (skip the parent itself)
            if (current != parent && current.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"Found '{childName}' at path: {GetTransformPath(current)}");
                return current;
            }
            
            // Add all children to the search queue
            foreach (Transform child in current)
            {
                searchQueue.Enqueue(child);
            }
        }
        
        Debug.LogWarning($"Could not find child named '{childName}' in hierarchy of '{parent.name}'");
        return null;
    }
    
    // Helper method to show the full path for debugging
    private string GetTransformPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
}