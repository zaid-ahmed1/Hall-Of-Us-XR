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
    
    // NEW METHOD: Prioritized matching for live demos
    public void TriggerPrioritizedMatching(List<string> priorityPhotoIds)
    {
        if (photoManager == null || photoManager.photos == null || photoManager.photos.Count == 0)
        {
            Debug.LogWarning("No photos available for prioritized matching!");
            return;
        }
        
        Debug.Log($"Starting prioritized photo-anchor matching with {priorityPhotoIds.Count} priority photos...");
        MatchPhotosToAnchorsWithPriority(priorityPhotoIds);
    }
    
    private void MatchPhotosToAnchorsWithPriority(List<string> priorityPhotoIds)
    {
        // Sort photos: priority photos first, then the rest
        var priorityPhotos = photoManager.photos
            .Where(p => priorityPhotoIds.Contains(p.id))
            .ToList();
            
        var regularPhotos = photoManager.photos
            .Where(p => !priorityPhotoIds.Contains(p.id))
            .ToList();
        
        // Combine with priority photos first
        var sortedPhotos = priorityPhotos.Concat(regularPhotos).ToList();
        
        Debug.Log($"Prioritized matching order: {priorityPhotos.Count} priority photos, then {regularPhotos.Count} regular photos");
        
        int matchedCount = 0;
        HashSet<GameObject> usedAnchors = new HashSet<GameObject>(); // Track used anchors
        
        foreach (Photo photo in sortedPhotos)
        {
            string firstTag = GetFirstTag(photo.tags);
            
            if (string.IsNullOrEmpty(firstTag))
            {
                Debug.LogWarning($"Photo {photo.filename} has no valid tags, skipping...");
                continue;
            }
            
            GameObject anchorObject = FindUnusedAnchorByTagAndOrientation(firstTag, photo.is_vertical, usedAnchors);
            
            if (anchorObject != null)
            {
                bool textSuccess = UpdateAnchorText(anchorObject, firstTag);
                bool imageSuccess = UpdateAnchorImage(anchorObject, photo);
                bool plaqueSuccess = UpdateAnchorPlaque(anchorObject, photo);
                
                if (textSuccess || imageSuccess || plaqueSuccess)
                {
                    usedAnchors.Add(anchorObject); // Mark this anchor as used
                    matchedCount++;
                    
                    string priorityIndicator = priorityPhotoIds.Contains(photo.id) ? " [PRIORITY]" : "";
                    Debug.Log($"Matched photo '{photo.filename}'{priorityIndicator} with tag '{firstTag}' to anchor '{anchorObject.name}' (Text: {textSuccess}, Image: {imageSuccess}, Plaque: {plaqueSuccess})");
                }
            }
            else
            {
                string priorityIndicator = priorityPhotoIds.Contains(photo.id) ? " [PRIORITY]" : "";
                Debug.LogWarning($"No available anchor found for tag '{firstTag}'{priorityIndicator} and orientation '{(photo.is_vertical ? "vertical" : "horizontal")}' from photo '{photo.filename}' (may be all used)");
            }
        }
        
        Debug.Log($"Prioritized matching complete! Successfully matched {matchedCount} photos to anchors.");
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
            
            GameObject anchorObject = FindUnusedAnchorByTagAndOrientation(firstTag, photo.is_vertical, usedAnchors);
            
            if (anchorObject != null)
            {
                bool textSuccess = UpdateAnchorText(anchorObject, firstTag);
                bool imageSuccess = UpdateAnchorImage(anchorObject, photo);
                bool plaqueSuccess = UpdateAnchorPlaque(anchorObject, photo);
                
                if (textSuccess || imageSuccess || plaqueSuccess)
                {
                    usedAnchors.Add(anchorObject); // Mark this anchor as used
                    matchedCount++;
                    Debug.Log($"Matched photo '{photo.filename}' with tag '{firstTag}' to anchor '{anchorObject.name}' (Text: {textSuccess}, Image: {imageSuccess}, Plaque: {plaqueSuccess})");
                }
            }
            else
            {
                Debug.LogWarning($"No available anchor found for tag '{firstTag}' and orientation '{(photo.is_vertical ? "vertical" : "horizontal")}' from photo '{photo.filename}' (may be all used)");
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
    
    private GameObject FindUnusedAnchorByTagAndOrientation(string tag, bool is_vertical, HashSet<GameObject> usedAnchors)
    {
        // Find all GameObjects in the scene that match the tag
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        
        List<GameObject> matchingAnchors = new List<GameObject>();
        string orientationKeyword = is_vertical ? "vertical" : "horizontal";
        
        foreach (GameObject obj in allObjects)
        {
            // Check if the object name contains the tag (case insensitive)
            if (obj.name.ToLower().Contains(tag.ToLower()))
            {
                // Also check if it contains the correct orientation keyword
                if (obj.name.ToLower().Contains(orientationKeyword))
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
        }
        
        // Return the first unused matching anchor
        if (matchingAnchors.Count > 0)
        {
            Debug.Log($"Found {matchingAnchors.Count} unused {orientationKeyword} anchors for tag '{tag}', using: {matchingAnchors[0].name}");
            return matchingAnchors[0];
        }
        
        Debug.LogWarning($"No unused {orientationKeyword} anchors found for tag '{tag}'. Available anchors may not match orientation requirement.");
        return null;
    }
    
    // NEW METHOD: Try to match a specific anchor with available photos
    public bool TryMatchSpecificAnchor(GameObject anchorObject)
    {
        if (photoManager == null || photoManager.photos == null || photoManager.photos.Count == 0)
        {
            Debug.LogWarning("No photos available to match with new anchor!");
            return false;
        }

        // Extract tag from anchor name
        string anchorTag = spatialAnchorManager.GetTagFromAnchorName(anchorObject.name);
        
        if (string.IsNullOrEmpty(anchorTag))
        {
            Debug.LogWarning($"Could not extract tag from anchor name: {anchorObject.name}");
            return false;
        }

        // Determine anchor orientation from name
        bool isVerticalAnchor = anchorObject.name.ToLower().Contains("vertical");
        
        Debug.Log($"Trying to match anchor '{anchorObject.name}' (tag: '{anchorTag}', vertical: {isVerticalAnchor})");

        // Get list of photo IDs already assigned to existing anchors
        HashSet<string> assignedPhotoIds = GetAssignedPhotoIds();

        // Find photos that match this anchor's tag and orientation, excluding already assigned ones
        var matchingPhotos = photoManager.photos
            .Where(p => {
                string firstTag = GetFirstTag(p.tags);
                return !string.IsNullOrEmpty(firstTag) && 
                       firstTag.Equals(anchorTag, System.StringComparison.OrdinalIgnoreCase) &&
                       p.is_vertical == isVerticalAnchor &&
                       !assignedPhotoIds.Contains(p.id); // Exclude already assigned photos
            })
            .OrderBy(p => photoManager.IsNewThisSession(p.id) ? 0 : 1) // New photos first
            .ToList();

        if (matchingPhotos.Count == 0)
        {
            Debug.LogWarning($"No unassigned photos found matching tag '{anchorTag}' and orientation '{(isVerticalAnchor ? "vertical" : "horizontal")}'. Total assigned photos: {assignedPhotoIds.Count}");
            return false;
        }

        // Try to match with the first available unassigned photo
        Photo photoToMatch = matchingPhotos[0];
        
        bool textSuccess = UpdateAnchorText(anchorObject, anchorTag);
        bool imageSuccess = UpdateAnchorImage(anchorObject, photoToMatch);
        bool plaqueSuccess = UpdateAnchorPlaque(anchorObject, photoToMatch);

        if (textSuccess || imageSuccess || plaqueSuccess)
        {
            string newSessionIndicator = photoManager.IsNewThisSession(photoToMatch.id) ? " [NEW THIS SESSION]" : "";
            Debug.Log($"Successfully matched new anchor '{anchorObject.name}' with photo '{photoToMatch.filename}'{newSessionIndicator} (Text: {textSuccess}, Image: {imageSuccess}, Plaque: {plaqueSuccess}). {matchingPhotos.Count - 1} other matching photos available.");
            return true;
        }
        else
        {
            Debug.LogWarning($"Failed to update anchor '{anchorObject.name}' with photo '{photoToMatch.filename}'");
            return false;
        }
    }

    // Helper method to get all photo IDs currently assigned to anchors in the scene
    private HashSet<string> GetAssignedPhotoIds()
    {
        HashSet<string> assignedIds = new HashSet<string>();
        
        // Find all spatial anchors in the scene
        OVRSpatialAnchor[] allAnchors = FindObjectsOfType<OVRSpatialAnchor>();
        
        foreach (var anchor in allAnchors)
        {
            // Skip preview objects (they have parents)
            if (anchor.transform.parent != null) continue;
            
            // Try to extract photo ID from the anchor's assigned content
            string photoId = GetPhotoIdFromAnchor(anchor.gameObject);
            if (!string.IsNullOrEmpty(photoId))
            {
                assignedIds.Add(photoId);
            }
        }
        
        Debug.Log($"Found {assignedIds.Count} photos currently assigned to anchors: {string.Join(", ", assignedIds)}");
        return assignedIds;
    }
    
    // Helper method to extract photo ID from an anchor's assigned content
    private string GetPhotoIdFromAnchor(GameObject anchorObject)
    {
        // Look for PictureRender child to get the texture
        Transform pictureRenderTransform = FindChildByName(anchorObject.transform, "PictureRender");
        
        if (pictureRenderTransform != null)
        {
            Renderer renderer = pictureRenderTransform.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                Texture texture = null;
                
                // Try to get texture from material
                if (renderer.material.HasProperty("_BaseMap"))
                {
                    texture = renderer.material.GetTexture("_BaseMap");
                }
                else if (renderer.material.HasProperty("_MainTex"))
                {
                    texture = renderer.material.GetTexture("_MainTex");
                }
                
                if (texture != null)
                {
                    // Find matching photo by comparing against S3 file paths
                    foreach (var photo in photoManager.photos)
                    {
                        string localPath = s3Manager.GetLocalPathForPhoto(photo);
                        if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                        {
                            // Check if this texture was loaded from this photo's file
                            // (This is a simplified check - in a more robust system you'd store metadata)
                            // For now, we'll use texture name if it contains the photo ID
                            if (texture.name.Contains(photo.id) || localPath.Contains(texture.name))
                            {
                                return photo.id;
                            }
                        }
                    }
                }
            }
        }
        
        return null; // No photo ID found
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
        GameObject anchorObject = FindUnusedAnchorByTagAndOrientation(firstTag, photo.is_vertical, new HashSet<GameObject>());
        
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
    
    private bool UpdateAnchorPlaque(GameObject anchorObject, Photo photo)
    {
        // Find the Canvas child, then the Image child within it
        Transform canvasTransform = FindChildByName(anchorObject.transform, "Canvas");
        
        if (canvasTransform == null)
        {
            Debug.LogWarning($"No 'Canvas' child found on anchor '{anchorObject.name}'");
            return false;
        }
        
        Transform imageTransform = FindChildByName(canvasTransform, "Image");
        
        if (imageTransform == null)
        {
            Debug.LogWarning($"No 'Image' child found in Canvas of anchor '{anchorObject.name}'");
            return false;
        }
        
        // Get the UI Image component
        UnityEngine.UI.Image imageComponent = imageTransform.GetComponent<UnityEngine.UI.Image>();
        if (imageComponent == null)
        {
            Debug.LogWarning($"No Image component found on 'Image' child of anchor '{anchorObject.name}'");
            return false;
        }
        
        // Check if photo has a plaque_id
        if (string.IsNullOrEmpty(photo.plaque_id))
        {
            Debug.LogWarning($"Photo '{photo.filename}' has no plaque_id, skipping plaque update");
            return false;
        }
        
        // Get the local file path for the plaque using plaque_id
        string localPlaquePath = s3Manager.GetLocalPathForPlaqueId(photo.plaque_id);
        if (string.IsNullOrEmpty(localPlaquePath) || !File.Exists(localPlaquePath))
        {
            Debug.LogWarning($"Plaque file not found for plaque_id '{photo.plaque_id}' at path: {localPlaquePath}");
            return false;
        }
        
        // Load and apply the texture as a sprite to the UI Image
        return LoadAndApplyUISprite(imageComponent, localPlaquePath, photo.plaque_id);
    }
    
    private bool LoadAndApplyUISprite(UnityEngine.UI.Image imageComponent, string imagePath, string filename)
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
                // Create a sprite from the texture for UI use
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                
                // Apply the sprite to the UI Image component
                imageComponent.sprite = sprite;
                
                Debug.Log($"Applied sprite '{filename}' to UI Image component");
                return true;
            }
            else
            {
                Debug.LogError($"Failed to load image data for '{filename}'");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading sprite for '{filename}': {e.Message}");
            return false;
        }
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
                
                // Check if this is a plaque material
                bool isPlaqueMaterial = material.name.ToLower().Contains("plaque");
                
                // Try URP/HDRP base map first
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", null); // Clear existing texture first
                    material.SetTexture("_BaseMap", texture); // Then apply new one
                    material.SetTextureScale("_BaseMap", Vector2.one);
                    material.SetTextureOffset("_BaseMap", Vector2.zero);
                    
                    if (isPlaqueMaterial)
                    {
                        // Plaque images are 800x400 (2:1 ratio) - adjust tiling to fit properly
                        material.SetTextureScale("_BaseMap", new Vector2(1f, 0.5f)); // Scale Y to fit 2:1 ratio
                        material.SetTextureOffset("_BaseMap", new Vector2(0f, 0.25f)); // Center vertically
                        Debug.Log($"Applied texture '{filename}' to _BaseMap property (plaque material with 2:1 ratio)");
                    }
                    else
                    {
                        // For regular photos, reset tiling and offset to show full image
                        material.SetTextureScale("_BaseMap", Vector2.one); // Show full image
                        material.SetTextureOffset("_BaseMap", Vector2.zero); // No offset
                        Debug.Log($"Applied texture '{filename}' to _BaseMap property (photo - full image display)");
                    }
                    return true;
                }
                // Try standard/builtin pipeline main texture
                else if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", texture);
                    
                    if (isPlaqueMaterial)
                    {
                        // Plaque images are 800x400 (2:1 ratio) - adjust tiling to fit properly
                        material.SetTextureScale("_MainTex", new Vector2(1f, 0.5f)); // Scale Y to fit 2:1 ratio
                        material.SetTextureOffset("_MainTex", new Vector2(0f, 0.25f)); // Center vertically
                        Debug.Log($"Applied texture '{filename}' to _MainTex property (plaque material with 2:1 ratio)");
                    }
                    else
                    {
                        // For regular photos, reset tiling and offset to show full image
                        material.SetTextureScale("_MainTex", Vector2.one); // Show full image
                        material.SetTextureOffset("_MainTex", Vector2.zero); // No offset
                        Debug.Log($"Applied texture '{filename}' to _MainTex property (photo - full image display)");
                    }
                    return true;
                }
                else
                {
                    Debug.LogWarning($"Material on {(isPlaqueMaterial ? "PlaqueRender" : "PictureRender")} doesn't have _BaseMap or _MainTex property.");
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