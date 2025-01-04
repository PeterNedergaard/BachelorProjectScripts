using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.Collections;

public class FeaturePointsVisualizer : MonoBehaviour
{
    private ARPointCloudManager pointCloudManager;

    public int maxMarkers;
    private List<GameObject> markers = new List<GameObject>();

    void Awake()
    {
        pointCloudManager = GetComponent<ARPointCloudManager>();
    }

    void OnEnable()
    {
        pointCloudManager.trackablesChanged.AddListener(OnTrackablesChanged);
    }

    void OnDisable()
    {
        pointCloudManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
    }

    void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPointCloud> eventArgs)
    {
        foreach (var pointCloud in eventArgs.updated)
        {
            if (pointCloud.positions.HasValue)
            {
                var positions = pointCloud.positions.Value;
                foreach (var point in positions)
                {
                    if (markers.Count >= maxMarkers)
                    {
                        GameObject oldestMarker = markers[0];
                        markers.RemoveAt(0);
                        Destroy(oldestMarker);
                    }

                    GameObject newMarker = Instantiate(pointCloudManager.pointCloudPrefab, point, Quaternion.identity);
                    markers.Add(newMarker);
                }
            }
        }
    }

    public void ClearFeaturePoints()
    {
        foreach (var marker in markers)
        {
            Destroy(marker);
        }

        markers.Clear();
    }

}
