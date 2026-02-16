using UnityEngine;

public class DiscoLightController : MonoBehaviour
{
    public Material discoMaterial;
    public Color[] lightColors;
    public float colorChangeInterval = 2f;

    private float timer;
    private int currentColorIndex;

    void Start()
    {
        if (lightColors == null || lightColors.Length == 0)
        {
            lightColors = new Color[]
            {
                new Color(1f, 0.2f, 0.2f), // Red
                new Color(0.2f, 1f, 0.2f), // Green
                new Color(0.2f, 0.2f, 1f), // Blue
                new Color(1f, 1f, 0.2f),   // Yellow
                new Color(1f, 0.2f, 1f)    // Magenta
            };
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= colorChangeInterval)
        {
            timer = 0;
            currentColorIndex = (currentColorIndex + 1) % lightColors.Length;
            discoMaterial.SetColor("_LightColor", lightColors[currentColorIndex]);
        }

        // Optional: Randomize parameters for more dynamic effect
        if (Random.value < 0.1f)
        {
            discoMaterial.SetFloat("_LightIntensity", Random.Range(0.8f, 1.5f));
        }
    }
}