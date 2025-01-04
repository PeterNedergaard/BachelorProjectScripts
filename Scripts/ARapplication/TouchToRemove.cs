using UnityEngine;

public class TouchToRemove : MonoBehaviour
{

    void Start()
    {

    }

    void Update()
    {
        // Check if there is a touch input
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // Proceed with actions on Touch began phase only
            if (touch.phase == TouchPhase.Began)
            {
                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit;

                // Perform the raycast
                if (Physics.Raycast(ray, out hit))
                {
                    // Check if the object hit has the tag you are interested in, or remove all objects hit
                    if (hit.collider.gameObject.Equals(gameObject))
                    {
                        Destroy(gameObject);  // Destroy the object
                    }
                }
            }
        }
    }
}
