using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class SettingsToggleHandler : MonoBehaviour
{
    public GameObject objWithFrameCaptureAndSend;
    private FrameCaptureAndSend frameCaptureAndSend;
    private FeaturePointsVisualizer featurePointsVisualizer;
    private ARPlaneManager arPlaneManager;
    public Toggle rayToggle;
    public Toggle featurePointsToggle;
    public Toggle anchorToggle;
    public Toggle planeToggle;


    void Start()
    {
        rayToggle.onValueChanged.AddListener(delegate { ToggleRays(rayToggle.isOn); });
        featurePointsToggle.onValueChanged.AddListener(delegate { ToggleFeaturePoints(featurePointsToggle.isOn); });
        anchorToggle.onValueChanged.AddListener(delegate { ToggleAnchors(anchorToggle.isOn); });
        planeToggle.onValueChanged.AddListener(delegate { TogglePlanes(planeToggle.isOn); });

        frameCaptureAndSend = objWithFrameCaptureAndSend.GetComponent<FrameCaptureAndSend>();
        featurePointsVisualizer = objWithFrameCaptureAndSend.GetComponent<FeaturePointsVisualizer>();
        arPlaneManager = objWithFrameCaptureAndSend.GetComponent<ARPlaneManager>();
    }

    public void ToggleRays(bool isOn)
    {
        frameCaptureAndSend.raysActive = isOn;
    }

    public void ToggleFeaturePoints(bool isOn)
    {
        featurePointsVisualizer.ClearFeaturePoints();

        featurePointsVisualizer.enabled = isOn;
    }

    public void ToggleAnchors(bool isOn)
    {
        frameCaptureAndSend.anchorsActive = isOn;
    }

    public void TogglePlanes(bool isOn)
    {
        arPlaneManager.enabled = isOn;
        foreach (var plane in arPlaneManager.trackables)
        {
            plane.gameObject.SetActive(isOn);
        }
    }

}
