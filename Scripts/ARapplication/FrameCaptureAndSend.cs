using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine;
using Unity.Collections;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System;

public class FrameCaptureAndSend : MonoBehaviour
{
    [HideInInspector]
    public bool raysActive = false;
    [HideInInspector]
    public bool anchorsActive = false;
    public Component planeManager;
    public Component featurePointsVisualizer;

    public GameObject arSessionObj;
    private ARCameraManager cameraManager;
    private ARRaycastManager arRaycastManager;
    private ARAnchorManager arAnchorManager;
    private ARPlaneManager arPlaneManager;
    public Button recogButton;
    public Button resetButton;
    public LineRenderer lineRenderer;
    public Canvas canvas;
    public Camera arCamera;
    private string url = "PREDICTION_ENDPOINT";
    private string predictionKey = "PREDICTION_KEY";

    Stopwatch stopwatch = new Stopwatch();
    private List<long> requestTimes = new List<long>();
    private bool initialTimes = true;

    private List<GameObject> markers = new List<GameObject>();
    private List<ARAnchor> anchors = new List<ARAnchor>();
    private List<LineRenderer> rayLines = new List<LineRenderer>();
    List<Image> boundingBoxes = new List<Image>();
    public Material lineMaterial;


    private void Start()
    {
        cameraManager = FindFirstObjectByType<ARCameraManager>();
        arRaycastManager = FindFirstObjectByType<ARRaycastManager>();
        arAnchorManager = FindFirstObjectByType<ARAnchorManager>();
        arPlaneManager = FindFirstObjectByType<ARPlaneManager>();

        if (cameraManager == null)
        {
            UnityEngine.Debug.LogError("ARCameraManager component not found on any camera");
        }

        recogButton.onClick.AddListener(CaptureAndSendImage);
        resetButton.onClick.AddListener(DeleteMarkers);
    }



    void CaptureAndSendImage()
    {
        UnityEngine.Debug.Log("Button has triggered method!");
        stopwatch = new Stopwatch();
        stopwatch.Start();

        XRCpuImage image;
        if (cameraManager.TryAcquireLatestCpuImage(out image))
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                //inputRect = new RectInt(image.width / 4, image.height / 4, 320, 320),
                //outputDimensions = new Vector2Int(320, 320),
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(image.width, image.height),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.None
            };

            // Create a NativeArray to hold the converted image
            int dataSize = image.GetConvertedDataSize(conversionParams);
            if (dataSize > 0)
            {
                var imageData = new NativeArray<byte>(dataSize, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                // Convert the image
                image.Convert(conversionParams, imageData);

                // Proceed with conversion
                Texture2D texture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, conversionParams.outputFormat, false);
                texture.LoadRawTextureData(imageData);
                texture.Apply();

                // Rotate the texture 90 degrees clockwise
                Texture2D rotatedTexture = RotateTextureLeft(texture);
                Texture2D flippedTexture = FlipTexture(rotatedTexture);

                //byte[] jpegBytes = flippedTexture.EncodeToJPG();

                byte[] jpegBytes = flippedTexture.EncodeToJPG();

                // Send the data to Azure Custom Vision
                StartCoroutine(SendImageData(jpegBytes));

                // Clean up
                Destroy(texture);
                imageData.Dispose();
                image.Dispose();
            }
            else
            {
                UnityEngine.Debug.LogError("Data size for conversion is zero.");
            }
        }
        else
        {
            UnityEngine.Debug.Log("Image aquire failed!");
        }
    }


    IEnumerator SendImageData(byte[] imageData)
    {
        var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(imageData);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Prediction-Key", predictionKey);
        request.SetRequestHeader("Content-Type", "application/octet-stream");

        yield return request.SendWebRequest();

        var text = request.downloadHandler.text;

        // Check for errors
        if (request.result != UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError("Error: " + request.error);
            // Additional error details from the response body
            if (!string.IsNullOrEmpty(request.downloadHandler.text))
            {
                UnityEngine.Debug.LogError("Error Details: " + text);
            }
        }
        else
        {
            PredictionResponse response = JsonUtility.FromJson<PredictionResponse>(text);

            // Clear list of Bounding Boxes
            foreach (var bbox in boundingBoxes)
            {
                Destroy(bbox);
            }
            boundingBoxes.Clear();

            // Clear list of Raycast lines
            foreach (var rayLine in rayLines)
            {
                Destroy(rayLine);
            }
            rayLines.Clear();

            foreach (var prediction in response.predictions)
            {
                if (prediction.probability > 0.99f)
                {
                    float normalizedLeft = prediction.boundingBox.left;
                    float normalizedTop = prediction.boundingBox.top;
                    float normalizedWidth = prediction.boundingBox.width;
                    float normalizedHeight = prediction.boundingBox.height;

                    float left = normalizedLeft * Screen.width;
                    float top = normalizedTop * Screen.height;
                    float width = normalizedWidth * Screen.width;
                    float height = normalizedHeight * Screen.height;

                    UnityEngine.Debug.Log($"Tag: {prediction.tagName}, Probability: {prediction.probability}, Bounding Box: Left {normalizedLeft}, Top {normalizedTop}, Width {normalizedWidth}, Height {normalizedHeight}");

                    // Calculate center of the bounding box in normalized coordinates
                    float centerX = left + width / 2;
                    float centerY = top + height / 2;

                    // Convert to screen coordinates
                    int screenCenterX = (int)centerX;
                    int screenCenterY = (int)centerY;

                    if (screenCenterX > Screen.width / 2)
                    {
                        if (screenCenterX < (Screen.width / 2) + width)
                        {
                            screenCenterX += (int)(((Screen.width / 2) + width) - screenCenterX);
                        }
                        else
                        {
                            screenCenterX += (int)width;
                        }
                    }
                    else if (screenCenterX < Screen.width / 2)
                    {
                        if (screenCenterX > (Screen.width / 2) - width)
                        {
                            screenCenterX += (int)(((Screen.width / 2) - width) - screenCenterX);
                        }
                        else
                        {
                            screenCenterX -= (int)width;
                        }
                    }

                    // Bounding box coords have zero in top-left, Unity coords have zero in bottom-left, convert to Unity coords
                    screenCenterY = Screen.height - screenCenterY;

                    Vector2 screenPosition = new Vector2(screenCenterX, screenCenterY);

                    var pred3DPos = Get3DPos(screenPosition, width);

                    if (pred3DPos.Equals(Vector3.zero))
                    {
                        break;
                    }

                    // Place marker
                    GameObject marker = Instantiate(arRaycastManager.raycastPrefab, pred3DPos, Quaternion.identity);

                    var labelUpdater = marker.GetComponent<LabelUpdater>();
                    labelUpdater.label.SetText(prediction.tagName);

                    markers.Add(marker);

                    if (anchorsActive)
                    {
                        var anchor = arAnchorManager.TryAddAnchorAsync(new Pose(pred3DPos, Quaternion.identity)).GetAwaiter().GetResult().value;

                        anchors.Add(anchor);
                    }
                }

            }

            stopwatch.Stop();

            if (requestTimes.Count > 2 && initialTimes)
            {
                requestTimes = new List<long>();
                initialTimes = false;
            }

            requestTimes.Add(stopwatch.ElapsedMilliseconds);

            UnityEngine.Debug.Log($"Time taken: {stopwatch.ElapsedMilliseconds} ms");
            UnityEngine.Debug.Log($"Average Request Time: {Math.Round(CalculateAverageTime())} ms");
        }
    }


    Vector3 Get3DPos(Vector2 screenPosition, float width)
    {

        Vector2[] points = new Vector2[]
        {
            new Vector2(screenPosition.x, screenPosition.y), // Center
            new Vector2(screenPosition.x - width / 4, screenPosition.y),
            new Vector2(screenPosition.x + width / 4, screenPosition.y)
        };

        var positions = new List<Vector3>();
        List<ARRaycastHit> hits = new List<ARRaycastHit>();

        foreach (var point in points)
        {
            if (raysActive)
            {
                CreateLineRenderer(point);
            }

            if (arRaycastManager.Raycast(point, hits, TrackableType.FeaturePoint))
            {
                // Take the first hit, closest to the user
                Pose hitPose = hits[0].pose;

                positions.Add(hitPose.position);
            }
        }

        var sum = Vector3.zero;

        foreach (var pose in positions)
        {
            sum += pose;
        }

        if (positions.Count > 0)
        {
            sum = sum / positions.Count;
        }
        
        return sum;
    }


    void CreateLineRenderer(Vector2 pos)
    {
        // Create a new GameObject
        GameObject lineObject = new GameObject("Raycast Line");
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

        rayLines.Add(lineRenderer);

        // Set the LineRenderer properties
        lineRenderer.material = lineMaterial;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.positionCount = 2;

        // Set positions
        lineRenderer.SetPosition(0, arCamera.ScreenToWorldPoint(new Vector3(pos.x, pos.y, arCamera.nearClipPlane + 0.1f)));
        lineRenderer.SetPosition(1, arCamera.ScreenToWorldPoint(new Vector3(pos.x, pos.y, 0.5f)));
    }


    Texture2D FlipTexture(Texture2D original)
    {
        Texture2D flipped = new Texture2D(original.width, original.height, original.format, false);

        int width = original.width;
        int height = original.height;

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                flipped.SetPixel(width - j - 1, i, original.GetPixel(j, i));
            }
        }

        flipped.Apply(); // Apply all SetPixel changes
        return flipped;
    }


    Texture2D RotateTextureLeft(Texture2D originalTexture)
    {
        Texture2D rotatedTexture = new Texture2D(originalTexture.height, originalTexture.width, originalTexture.format, false);

        int originalWidth = originalTexture.width;
        int originalHeight = originalTexture.height;

        for (int i = 0; i < originalHeight; i++)
        {
            for (int j = 0; j < originalWidth; j++)
            {
                // Get pixel from original texture and set it into the rotated texture
                Color pixel = originalTexture.GetPixel(j, i);
                rotatedTexture.SetPixel(i, originalWidth - j - 1, pixel);
            }
        }
        rotatedTexture.Apply();
        return rotatedTexture;
    }


    private double CalculateAverageTime()
    {
        if (requestTimes.Count == 0)
            return 0.0;

        long total = 0;
        foreach (var time in requestTimes)
        {
            total += time;
        }
        return total / (double)requestTimes.Count;
    }


    private void DeleteMarkers()
    {
        foreach (var marker in markers)
        {
            Destroy(marker);
        }
        markers.Clear();

        foreach (var anchor in anchors)
        {
            arAnchorManager.TryRemoveAnchor(anchor);
            Destroy(anchor);
        }
        anchors.Clear();
    }



    [System.Serializable]
    public class PredictionResponse
    {
        public string id;
        public string project;
        public string iteration;
        public string created;
        public List<Prediction> predictions;
    }

    [System.Serializable]
    public class Prediction
    {
        public float probability;
        public string tagId;
        public string tagName;
        public BoundingBox boundingBox;
    }

    [System.Serializable]
    public class BoundingBox
    {
        public float left;
        public float top;
        public float width;
        public float height;
    }


}
