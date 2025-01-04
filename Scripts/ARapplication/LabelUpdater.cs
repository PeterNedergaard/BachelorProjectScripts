using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LabelUpdater : MonoBehaviour
{
    // Label is Public so directly accessible from outside
    public TextMeshProUGUI label;

    private void Update()
    {
        Camera camera = Camera.main;

        // Calculate the direction from the object to the camera
        Vector3 direction = camera.transform.position - transform.position;

        // Set the direction's y component to 0 to keep the object upright
        direction.y = 0;

        // Create a new rotation from the direction pointing to the camera
        Quaternion rotation = Quaternion.LookRotation(-direction);

        // Set the object's rotation
        transform.rotation = rotation;
    }

}
