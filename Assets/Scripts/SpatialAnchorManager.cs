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

    private void Update()
    {
    // Cycle through prefabs using the X button
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            selectedPrefabIndex = (selectedPrefabIndex + 1) % anchorPrefabs.Length;
            Debug.Log("Selected prefab: " + selectedPrefabIndex);

            Destroy(previewObject);
            SpawnPreview();
        }


        // Place anchor
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            PlaceAnchor();
        }


        // Delete last anchor
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            DeleteLastAnchor();
        }

        // Clear all anchors
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
        {
            ClearAllAnchors();
        }

        // Load anchors
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))
        {
            LoadSavedAnchors();
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
            Destroy(anchor.gameObject); // 🔹 also remove from scene
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

    private void LoadSavedAnchors()
    {
        anchorLoader.LoadAnchorsByUuid();
    }
}
