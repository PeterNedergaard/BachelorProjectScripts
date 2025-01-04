using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomiezdReflection : MonoBehaviour
{
    public ReflectionProbe probe;
    public float minIntensity = 0.5f;
    public float maxIntensity = 2f;

    void Start()
    {
        UpdateReflectionProbe();
    }

    private void Update()
    {
        UpdateReflectionProbe();
    }

    void UpdateReflectionProbe()
    {
        var files = Resources.LoadAll<Sprite>("Backgrounds");
        if (files.Length == 0)
        {
            Debug.LogError("No images found in the specified directory.");
            return;
        }

        var randomFile = files[Random.Range(0, files.Length)];
        Texture2D texture = randomFile.texture;

        probe.customBakedTexture = texture;
        probe.intensity = Random.Range(minIntensity, maxIntensity);

        // Re-bake the reflection probe to update the scene
        probe.RenderProbe();
    }
}
