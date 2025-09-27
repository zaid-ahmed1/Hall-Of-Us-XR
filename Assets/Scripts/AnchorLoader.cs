using System;
using TMPro;
using UnityEngine;

public class AnchorLoader : MonoBehaviour
{
    private SpatialAnchorManager spatialAnchorManager;

    Action<OVRSpatialAnchor.UnboundAnchor, bool> _onLoadAnchor;

    private void Awake()
    {
        spatialAnchorManager = GetComponent<SpatialAnchorManager>();
        _onLoadAnchor = OnLocalized;
    }

    public void LoadAnchorsByUuid()
    {
        if (!PlayerPrefs.HasKey(SpatialAnchorManager.NumUuidsPlayerPref)) return;

        int count = PlayerPrefs.GetInt(SpatialAnchorManager.NumUuidsPlayerPref);
        if (count == 0) return;

        var uuids = new Guid[count];
        for (int i = 0; i < count; ++i)
        {
            string json = PlayerPrefs.GetString("anchor" + i);
            var data = JsonUtility.FromJson<AnchorData>(json);
            uuids[i] = new Guid(data.uuid);
        }

        Load(new OVRSpatialAnchor.LoadOptions
        {
            Timeout = 0,
            StorageLocation = OVRSpace.StorageLocation.Local,
            Uuids = uuids
        });
    }

    private void Load(OVRSpatialAnchor.LoadOptions options)
    {
        OVRSpatialAnchor.LoadUnboundAnchors(options, anchors =>
        {
            if (anchors == null) return;

            foreach (var anchor in anchors)
            {
                if (anchor.Localized)
                    _onLoadAnchor(anchor, true);
                else if (!anchor.Localizing)
                    anchor.Localize(_onLoadAnchor);
            }
        });
    }

    private void OnLocalized(OVRSpatialAnchor.UnboundAnchor unboundAnchor, bool success)
    {
        if (!success) return;

        // Find prefab index from PlayerPrefs
        int prefabIndex = 0;
        int playerNumUuids = PlayerPrefs.GetInt(SpatialAnchorManager.NumUuidsPlayerPref);
        for (int i = 0; i < playerNumUuids; i++)
        {
            string json = PlayerPrefs.GetString("anchor" + i, "");
            if (string.IsNullOrEmpty(json)) continue;

            var data = JsonUtility.FromJson<AnchorData>(json);
            if (new Guid(data.uuid) == unboundAnchor.Uuid)
            {
                prefabIndex = Mathf.Clamp(data.prefabIndex, 0, spatialAnchorManager.anchorPrefabs.Length - 1);
                break;
            }
        }

        // Instantiate the correct prefab
        var prefab = spatialAnchorManager.anchorPrefabs[prefabIndex];
        var spatialAnchor = Instantiate(prefab, unboundAnchor.Pose.position, unboundAnchor.Pose.rotation);
        unboundAnchor.BindTo(spatialAnchor);

        // Update UI
        var texts = spatialAnchor.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 1)
        {
            texts[0].text = "UUID: " + unboundAnchor.Uuid;
            texts[1].text = "Loaded from Device";
        }
    }
}
