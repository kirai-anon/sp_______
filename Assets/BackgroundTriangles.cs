using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FlatLandscape : MonoBehaviour
{
    // audio

    [HideInInspector] [System.NonSerialized] public int sampleSize = 1024;
    [HideInInspector] [System.NonSerialized] public float[] spectrumData;

    private float boost = 0;

    // mesh

    public int width = 10;
    public int height = 10;
    public float spacing = 1f;
    public float baseJitter = 0.5f;

    [Header("Movement")]
    public float jitterAmount = 0.4f;
    public float driftSpeed = 0.5f;

    [Header("Color Shifting")]
    public float colorChangeSpeed = 1.5f;

    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;
    private Color[] targetColors;

    // Store original grid center points to drift around
    private Vector3[,] baseGrid;

    void Start()
    {
        // mesh

        mesh = new Mesh();
        mesh.MarkDynamic(); // Optimizes mesh for frequent updates
        GetComponent<MeshFilter>().mesh = mesh;

        baseGrid = new Vector3[width + 1, height + 1];
        float hW = (width * spacing) / 2f;
        float hH = (height * spacing) / 2f;

        for (int y = 0; y <= height; y++)
            for (int x = 0; x <= width; x++)
                baseGrid[x, y] = new Vector3(
                    x * spacing - hW + Random.Range(-baseJitter, baseJitter),
                    y * spacing - hH + Random.Range(-baseJitter, baseJitter),
                    10
                );

        int triCount = width * height * 2;
        vertices = new Vector3[triCount * 3];
        colors = new Color[triCount * 3];
        targetColors = new Color[triCount * 3];
        int[] triangles = new int[triCount * 3];

        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = i;
            if (i % 3 == 0) targetColors[i] = GetBlueColor(); // Set initial target
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
    }

    void Update()
    {
        // audio

        if (spectrumData == null || spectrumData.Length != sampleSize)
        {
            spectrumData = new float[sampleSize];
        }
        
        AudioListener.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

        // Sum the low frequencies (Bass). 
        // Typically, the first 10-20 bins contain the most "energy" for visual pulse.
        int bassBoundary = 20;
        for (int e = 0; e < bassBoundary; e++)
        {
            boost += spectrumData[e];
        }

        boost *= 0.5f;

        // mesh

        int v = 0;
        float time = Time.time * driftSpeed;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 1. Calculate drifting corners using Perlin Noise for smoothness
                Vector3 bl = GetDriftedPos(x, y, time);
                Vector3 tl = GetDriftedPos(x, y + 1, time);
                Vector3 tr = GetDriftedPos(x + 1, y + 1, time);
                Vector3 br = GetDriftedPos(x + 1, y, time);

                // 2. Update Colors & Vertices for Triangle 1
                UpdateTriangle(v, bl, tl, tr);
                v += 3;

                // 3. Update Colors & Vertices for Triangle 2
                UpdateTriangle(v, bl, tr, br);
                v += 3;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
    }

    void UpdateTriangle(int index, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        if (Random.value < 0.003f) targetColors[index] = GetBlueColor();

        // Apply boost to the Lerp result so the flash is immediate
        Color nextColor = Color.Lerp(colors[index], targetColors[index], Time.deltaTime * colorChangeSpeed);

        // Add the boost to the color channels (adding to Blue/Green for a glow effect)
        Color boostedColor = nextColor; //+ new Color(boost * 0.01f, boost * 0.01f, boost * 0.04f);

        for (int i = 0; i < 3; i++) colors[index + i] = boostedColor;

        vertices[index] = p1;
        vertices[index + 1] = p2;
        vertices[index + 2] = p3;
    }

    Vector3 GetDriftedPos(int x, int y, float t)
    {
        Vector3 basePos = baseGrid[x, y];
        // Use PerlinNoise so neighboring vertices move somewhat together
        float offsetX = (Mathf.PerlinNoise(x * 0.5f + t, y * 0.5f) - 0.5f) * jitterAmount;
        float offsetY = (Mathf.PerlinNoise(x * 0.5f, y * 0.5f + t) - 0.5f) * jitterAmount;
        return basePos + new Vector3(offsetX, offsetY, 0);
    }

    Color GetBlueColor()
    {
        float s = Random.Range(0.00f, 0.04f);
        return new Color(s, s, s + s);
    }
}
