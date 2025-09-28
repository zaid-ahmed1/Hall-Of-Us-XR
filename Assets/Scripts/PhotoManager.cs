using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

// Classes to match your JSON structure
[System.Serializable]
public class Photo
{
    public string id;
    public string filename;
    public string url;
    public string tags;
    public string user_id;
    public int likes;
    public string plaque_id;
    public bool is_vertical;
}

[System.Serializable]
public class PhotosResponse
{
    public List<Photo> photos;
}

public class PhotoManager : MonoBehaviour
{
    private const string endpoint = "https://api.doubleehbatteries.com/photos";

    public List<Photo> photos = new List<Photo>();

    [Header("References")]
    public S3 s3Manager;
    public PhotoAnchorMatcher photoAnchorMatcher;
    
    [Header("Manual Update Controls")]
    public bool useManualUpdates = true;
    public float refreshInterval = 15f; // Only used if manual updates are disabled
    private HashSet<string> seenPhotoIds = new HashSet<string>();
    
    [Header("Live Demo Feature")]
    public List<string> newPhotosThisSession = new List<string>(); // Track photos added during this session
    
    void Start()
    {
        StartCoroutine(FetchPhotos());
        
        if (!useManualUpdates)
        {
            StartCoroutine(PeriodicPhotoCheck());
        }
    }
    
    void Update()
    {
        if (useManualUpdates)
        {
            // Existing manual update: Right stick up + right trigger
            float thumbstickY = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).y;
            bool triggerPressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            
            if (thumbstickY > 0.7f && triggerPressed)
            {
                Debug.Log("Manual update triggered!");
                StartCoroutine(ManualUpdateCheck());
            }
            
            // NEW LIVE DEMO FEATURE: Up stick + hand grip
            bool handGripPressed = OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
            
            if (thumbstickY > 0.7f && handGripPressed)
            {
                Debug.Log("Live demo update triggered!");
                StartCoroutine(LiveDemoUpdateCheck());
            }
        }
    }
    
    IEnumerator LiveDemoUpdateCheck()
    {
        Debug.Log("Checking for newest content (live demo mode)...");
        
        // First, fetch latest photos to see if there are any new ones
        yield return StartCoroutine(FetchPhotos());
        
        // Check for actually new photos
        var newPhotos = photos.Where(p => !seenPhotoIds.Contains(p.id)).ToList();
        if (newPhotos.Count > 0)
        {
            Debug.Log($"Found {newPhotos.Count} new photos in live demo mode!");
            
            // Add new IDs to tracking and session tracking
            foreach (var photo in newPhotos)
            {
                seenPhotoIds.Add(photo.id);
                newPhotosThisSession.Add(photo.id); // Track as new this session
            }
            
            // Download only the newest file from S3
            if (s3Manager != null)
            {
                yield return StartCoroutine(s3Manager.DownloadNewestFileOnly());
            }
            
            // Trigger prioritized matching (newest first)
            if (photoAnchorMatcher != null)
            {
                photoAnchorMatcher.TriggerPrioritizedMatching(newPhotosThisSession);
            }
        }
        else
        {
            Debug.Log("No new photos found in live demo mode.");
        }
    }
    
    IEnumerator ManualUpdateCheck()
    {
        Debug.Log("Checking for new photos (manual trigger)...");
        yield return StartCoroutine(FetchPhotos());
        
        // Check for actually new photos
        var newPhotos = photos.Where(p => !seenPhotoIds.Contains(p.id)).ToList();
        if (newPhotos.Count > 0)
        {
            Debug.Log($"Found {newPhotos.Count} new photos!");
            
            // Add new IDs to tracking
            foreach (var photo in newPhotos)
                seenPhotoIds.Add(photo.id);
            
            // Trigger new file check and matching
            if (s3Manager != null)
                _ = s3Manager.CheckForNewFiles();
            
            if (photoAnchorMatcher != null)
                photoAnchorMatcher.TriggerMatching();
        }
        else
        {
            Debug.Log("No new photos found.");
        }
    }

    IEnumerator PeriodicPhotoCheck()
    {
        yield return new WaitForSeconds(10f); // Initial delay
    
        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);
        
            Debug.Log("Checking for new photos...");
            yield return StartCoroutine(FetchPhotos());
        
            // Check for actually new photos (not just count change)
            var newPhotos = photos.Where(p => !seenPhotoIds.Contains(p.id)).ToList();
            if (newPhotos.Count > 0)
            {
                Debug.Log($"Found {newPhotos.Count} new photos!");
                
                // Add new IDs to tracking
                foreach (var photo in newPhotos)
                    seenPhotoIds.Add(photo.id);
                
                // Trigger new file check and matching
                if (s3Manager != null)
                    _ = s3Manager.CheckForNewFiles();
                
                if (photoAnchorMatcher != null)
                    photoAnchorMatcher.TriggerMatching();
            }
        }
    }
    
    // Helper method to check if a photo is new this session
    public bool IsNewThisSession(string photoId)
    {
        return newPhotosThisSession.Contains(photoId);
    }
    
    // NEW METHOD: Fetch photos specifically for new anchor matching
    public IEnumerator FetchPhotosForNewAnchor()
    {
        Debug.Log("Fetching photos for new anchor matching...");
        yield return StartCoroutine(FetchPhotos());
        
        // Check if any new photos were found and add them to session tracking
        var newPhotos = photos.Where(p => !seenPhotoIds.Contains(p.id)).ToList();
        if (newPhotos.Count > 0)
        {
            Debug.Log($"Found {newPhotos.Count} new photos during anchor creation!");
            foreach (var photo in newPhotos)
            {
                seenPhotoIds.Add(photo.id);
                newPhotosThisSession.Add(photo.id);
            }
        }
    }
    
    IEnumerator FetchPhotos()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError("Error fetching photos: " + request.error);
            }
            else
            {
                string json = request.downloadHandler.text;
                Debug.Log("Received JSON: " + json);

                // Deserialize into our wrapper class
                PhotosResponse response = JsonUtility.FromJson<PhotosResponse>(json);
                if (response != null && response.photos != null)
                {
                    photos = response.photos;
                    Debug.Log("Loaded " + photos.Count + " photos.");
                    
                    Debug.Log("=== PHOTO API DATA ===");
                    foreach (var photo in photos)
                    {
                        Debug.Log($"Photo ID: '{photo.id}' | Filename: '{photo.filename}' | Vertical: {photo.is_vertical}");
                    }
                    Debug.Log("=== END PHOTO DATA ===");
                    
                    // Initialize seen photos on first load
                    if (seenPhotoIds.Count == 0)
                    {
                        foreach (var photo in photos)
                            seenPhotoIds.Add(photo.id);
                    }
                }
                else
                {
                    Debug.LogWarning("No photos found in response.");
                }
            }
        }
    }
}