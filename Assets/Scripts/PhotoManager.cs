using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

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
}

[System.Serializable]
public class PhotosResponse
{
    public List<Photo> photos;
}

public class PhotoManager : MonoBehaviour
{
    private const string endpoint = "http://lawn-128-61-74-223.lawn.gatech.edu:8000/photos";

    public List<Photo> photos = new List<Photo>();

    void Start()
    {
        StartCoroutine(FetchPhotos());
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
                }
                else
                {
                    Debug.LogWarning("No photos found in response.");
                }
            }
        }
    }
}
