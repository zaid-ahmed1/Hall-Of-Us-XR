using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

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
        
        foreach (Photo photo in photoManager.photos)
        {
            string firstTag = GetFirstTag(photo.tags);
            
            if (string.IsNullOrEmpty(firstTag))
            {
                Debug.LogWarning($"Photo {photo.filename} has no valid tags, skipping...");
                continue;
            }
            
            GameObject anchorObject = FindAnchorByTag(firstTag);
            
            if (anchorObject != null)
            {
                bool success = UpdateAnchorText(anchorObject, firstTag);
                if (success)
                {
                    matchedCount++;
                    Debug.Log($"✅ Matched photo '{photo.filename}' with tag '{firstTag}' to anchor '{anchorObject.name}'");
                }
            }
            else
            {
                Debug.LogWarning($"❌ No anchor found for tag '{firstTag}' from photo '{photo.filename}'");
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
    
    private GameObject FindAnchorByTag(string tag)
    {
        // Find all GameObjects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        foreach (GameObject obj in allObjects)
        {
            // Check if the object name contains the tag (case insensitive)
            if (obj.name.ToLower().Contains(tag.ToLower()))
            {
                // Verify it's actually a spatial anchor
                if (obj.GetComponent<OVRSpatialAnchor>() != null)
                {
                    return obj;
                }
            }
        }
        
        return null;
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
            
        GameObject anchorObject = FindAnchorByTag(firstTag);
        
        if (anchorObject != null)
        {
            return UpdateAnchorText(anchorObject, firstTag);
        }
        
        return false;
    }
}