using System;
using System.Linq;
using UnityEngine.Perception.Randomization.Parameters;
using UnityEngine.Perception.Randomization.Samplers;
using UnityEngine.Perception.Randomization.Utilities;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.Perception.Randomization.Randomizers
{
    [Serializable]
    [AddRandomizerMenu("Perception/Custom Background Randomizer")]
    [MovedFrom("UnityEngine.Perception.Internal")]
    public class CustomBackgroundRandomizerTag : Randomizer
    {
        public UI.Image image;

        private Sprite[] backgroundImages;

        public ReflectionProbe probe;
        public float minIntensity = 0.5f;
        public float maxIntensity = 1f;

        protected override void OnIterationStart()
        {
            backgroundImages = Resources.LoadAll<Sprite>("Backgrounds");
            if (backgroundImages.Length == 0)
            {
                Debug.LogError("No images found in the specified directory.");
                return;
            }

            int index = Random.Range(0, backgroundImages.Length);
            float brightness = Random.Range(0.25f, 3f);

            var textureSprite = backgroundImages[index];

            // Adjust sprite brightness
            image.sprite = textureSprite;
            image.color = new Color(brightness, brightness, brightness, 1);

            probe.customBakedTexture = textureSprite.texture;
            probe.intensity = Random.Range(minIntensity, maxIntensity);

            // Re-bake the reflection probe to update the scene
            probe.mode = Rendering.ReflectionProbeMode.Realtime;
            probe.RenderProbe();
            probe.mode = Rendering.ReflectionProbeMode.Custom;
        }
    }
}
