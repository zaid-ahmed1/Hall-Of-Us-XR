using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;

[Serializable]
public class AnchorData
{
    public string uuid;
    public int prefabIndex;
}

public class SpatialAnchorManager : MonoBehaviour
{
    [Header("Anchor Prefabs")]
    public OVRSpatialAnchor[] anchorPrefabs; // Different prefabs you can place
    public const string NumUuidsPlayerPref = "numUuids";
    private float moveSpeed = 0.5f; // units per second
    private float deadzone = 0.1f;
    private GameObject previewObject; // live preview
    private OVRCameraRig cameraRig;

    private List<OVRSpatialAnchor> anchors = new List<OVRSpatialAnchor>();
    private AnchorLoader anchorLoader;
    private int selectedPrefabIndex = 0;

    [Header("Auto-Match References")]
    public PhotoManager photoManager;
    public S3 s3Manager;
    public PhotoAnchorMatcher photoAnchorMatcher;
    public bool autoMatchNewAnchors = true;

    private void Awake()
    {
        anchorLoader = GetComponent<AnchorLoader>();
        cameraRig = FindAnyObjectByType<OVRCameraRig>();
        SpawnPreview(); // create initial preview
    }

    private void OnDestroy()
    {
        Debug.Log("Anchors:\n" + anchors);
    }

    void Start()
    {
        LoadSavedAnchors();
    }

    private bool inAnchorMode = false;

    private void Update()
    {
        // Get thumbstick Y axis value
        float thumbstickY = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).y;

        // Enter anchor mode if stick is held downward
        if (thumbstickY < -0.7f)
        {
            if (!inAnchorMode)
            {
                inAnchorMode = true;
                if (previewObject == null) SpawnPreview();
                previewObject.SetActive(true); // Show preview
                UpdateTagDisplayForPreview();                
                Debug.Log("Entered Anchor Mode (stick down)");
            }

            // Only allow anchor interactions when in anchor mode
            // Cycle through prefabs (X button)
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            {
                selectedPrefabIndex = (selectedPrefabIndex + 1) % anchorPrefabs.Length;
                Debug.Log("Selected prefab: " + selectedPrefabIndex);

                Destroy(previewObject);
                SpawnPreview();
                UpdateTagDisplayForPreview();
            }

            // Place anchor (Trigger)
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                PlaceAnchor();
            }

            // Delete last anchor (B / Button.Two)
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
            {
                DeleteLastAnchor();
            }

            // Clear all anchors (Grip)
            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
            {
                ClearAllAnchors();
            }
        }
        else
        {
            // Exit anchor mode if stick is released
            if (inAnchorMode)
            {
                inAnchorMode = false;
                if (previewObject != null) 
                    previewObject.SetActive(false); // Hide preview
                Debug.Log("Exited Anchor Mode");
            }
        }
    }


    private void CreateAndSaveSpatialAnchor(int prefabIndex)
    {
        var prefab = anchorPrefabs[prefabIndex];
        var position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
        var rotation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

        OVRSpatialAnchor newAnchor = Instantiate(prefab, position, rotation);

        // Start coroutine to wait until it's created before saving
        StartCoroutine(AnchorCreatedAndSaved(newAnchor, prefabIndex));
    }

    private IEnumerator AnchorCreatedAndSaved(OVRSpatialAnchor anchor, int prefabIndex)
    {
        while (!anchor.Created && !anchor.Localized)
            yield return new WaitForEndOfFrame();

        // Save automatically
        anchor.Save((savedAnchor, success) =>
        {
            if (success)
                Debug.Log("Anchor saved successfully");
        });

        anchors.Add(anchor);
        SaveAnchorDataToPlayerPrefs(anchor.Uuid, prefabIndex);

        // Update UI if present
        var canvas = anchor.GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            var texts = canvas.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 1)
            {
                texts[0].text = "UUID: " + anchor.Uuid;
                texts[1].text = "Saved";
            }
        }

        // NEW: Try to auto-match this new anchor with available content
        if (autoMatchNewAnchors)
        {
            StartCoroutine(TryAutoMatchNewAnchor(anchor.gameObject));
        }
    }

    private IEnumerator TryAutoMatchNewAnchor(GameObject newAnchor)
    {
        Debug.Log($"Attempting to auto-match new anchor: {newAnchor.name}");

        // First, refresh photo data
        if (photoManager != null)
        {
            Debug.Log("Refreshing photo data for new anchor...");
            yield return StartCoroutine(photoManager.FetchPhotosForNewAnchor());
        }

        // Then check for new S3 files
        if (s3Manager != null)
        {
            Debug.Log("Checking S3 for new files...");
            yield return StartCoroutine(s3Manager.CheckForNewFilesForNewAnchor());
        }

        // Finally, try to match this specific anchor
        if (photoAnchorMatcher != null)
        {
            Debug.Log("Attempting to match new anchor with available photos...");
            bool matchSuccess = photoAnchorMatcher.TryMatchSpecificAnchor(newAnchor);
            
            if (matchSuccess)
            {
                Debug.Log($"Successfully matched new anchor {newAnchor.name} with content!");
            }
            else
            {
                Debug.Log($"No suitable content found for new anchor {newAnchor.name}");
            }
        }
    }

    private void SaveAnchorDataToPlayerPrefs(Guid uuid, int prefabIndex)
    {
        if (!PlayerPrefs.HasKey(NumUuidsPlayerPref))
            PlayerPrefs.SetInt(NumUuidsPlayerPref, 0);

        int index = PlayerPrefs.GetInt(NumUuidsPlayerPref);

        AnchorData data = new AnchorData
        {
            uuid = uuid.ToString(),
            prefabIndex = prefabIndex
        };

        PlayerPrefs.SetString("anchor" + index, JsonUtility.ToJson(data));
        PlayerPrefs.SetInt(NumUuidsPlayerPref, ++index);
        PlayerPrefs.Save();
    }
    private void DeleteLastAnchor()
    {
        if (anchors.Count == 0) return;

        var lastAnchor = anchors[anchors.Count - 1];

        // Remove from list first so we don't keep stale refs
        anchors.RemoveAt(anchors.Count - 1);

        if (lastAnchor != null)
        {
            lastAnchor.Erase((erasedAnchor, success) => { });
            Destroy(lastAnchor.gameObject);
        }

        RemoveLastAnchorFromPrefs();
    }



    private void RemoveLastAnchorFromPrefs()
    {
        if (!PlayerPrefs.HasKey(NumUuidsPlayerPref)) return;

        int count = PlayerPrefs.GetInt(NumUuidsPlayerPref);
        if (count > 0)
        {
            PlayerPrefs.DeleteKey("anchor" + (count - 1));
            PlayerPrefs.SetInt(NumUuidsPlayerPref, count - 1);
            PlayerPrefs.Save();
        }
    }

    private void ClearAllAnchors()
    {
        foreach (var anchor in anchors)
        {
            anchor.Erase((erasedAnchor, success) => { });
            Destroy(anchor.gameObject); // also remove from scene
        }

        anchors.Clear();

        if (PlayerPrefs.HasKey(NumUuidsPlayerPref))
        {
            int count = PlayerPrefs.GetInt(NumUuidsPlayerPref);
            for (int i = 0; i < count; i++)
                PlayerPrefs.DeleteKey("anchor" + i);

            PlayerPrefs.DeleteKey(NumUuidsPlayerPref);
            PlayerPrefs.Save();
        }
    }

    private void SpawnPreview()
    {
        var prefab = anchorPrefabs[selectedPrefabIndex].gameObject;
        previewObject = Instantiate(prefab, Vector3.zero, Quaternion.identity);

        // Disable anchor logic while previewing
        var anchor = previewObject.GetComponent<OVRSpatialAnchor>();
        if (anchor != null) anchor.enabled = false;

        // Attach to controller so it follows hand
        previewObject.transform.SetParent(cameraRig.rightControllerAnchor, false);
    }

    private void PlaceAnchor()
    {
        if (previewObject == null) return;

        // Detach and enable spatial anchor
        previewObject.transform.SetParent(null);
        var spatialAnchor = previewObject.GetComponent<OVRSpatialAnchor>();
        if (spatialAnchor != null)
        {
            spatialAnchor.enabled = true;
            StartCoroutine(AnchorCreatedAndSaved(spatialAnchor, selectedPrefabIndex));
            anchors.Add(spatialAnchor);
        }

        // Spawn a new preview for the next placement
        SpawnPreview();
    }

    [Header("Tag Display")]
    public TextMeshProUGUI tagDisplayText; // Assign this in inspector

// Method to extract tag name from anchor name
    public string GetTagFromAnchorName(string anchorName)
    {
        if (string.IsNullOrEmpty(anchorName))
            return "";
    
        // Find the first underscre and return everything before it
        int asteriskIndex = anchorName.IndexOf('_');
        if (asteriskIndex > 0)
        {
            return anchorName.Substring(0, asteriskIndex);
        }
    
        // If no underscore found, return the whole name
        return anchorName;
    }

    // Method to update the text display with current anchor's tag
    public void UpdateTagDisplay(OVRSpatialAnchor anchor)
    {
        if (anchor != null && tagDisplayText != null)
        {
            string tag = GetTagFromAnchorName(anchor.name);
            tagDisplayText.text = tag;
            Debug.Log($"Displaying tag: {tag} for anchor: {anchor.name}");
        }
    }

    // Example usage - call this when you want to show a tag
    public void ShowTagForAnchor(GameObject anchorObject)
    {
        OVRSpatialAnchor anchor = anchorObject.GetComponent<OVRSpatialAnchor>();
        if (anchor != null)
        {
            UpdateTagDisplay(anchor);
        }
    }
    
    // Method to update tag display for the current preview object
    private void UpdateTagDisplayForPreview()
    {
        if (previewObject != null && tagDisplayText != null)
        {
            string tag = GetTagFromAnchorName(previewObject.name);
            tagDisplayText.text = tag;
            Debug.Log($"Displaying preview tag: {tag} for preview: {previewObject.name}");
        }
    }
    
    private void LoadSavedAnchors()
    {
        anchorLoader.LoadAnchorsByUuid();
    }
}