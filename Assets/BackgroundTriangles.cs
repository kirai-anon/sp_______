using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FlatLandscape : MonoBehaviour
{
    public int width = 10;
    public int height = 10;
    public float spacing = 1f;
    public float baseJitter = 0.5f;

    [Header("Movement")]
    public float jitterAmount = 0.4f;
    public float driftSpeed = 0.5f;

    [Header("Color Shifting")]
    public float colorChangeSpeed = 1.5f;

    [Header("Audio Settings")]
    public float pulseIntensity = 1f;
    public float pulseSensitivity = 1f;
    // 512 is a standard buffer size for spectrum analysis
    private float[] spectrum = new float[512];
    private int[] triangleFrequencyMap;

    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;
    private Color[] targetColors;
    private Vector3[,] baseGrid;

    void Start()
    {
        mesh = new Mesh();
        mesh.MarkDynamic();
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

        // Map each triangle to a random frequency bin (0-511)
        triangleFrequencyMap = new int[triCount];

        int[] triangles = new int[triCount * 3];

        for (int i = 0; i < triCount; i++)
        {
            // We favor lower indices (0-64) because music energy is usually in the bass/mids
            triangleFrequencyMap[i] = Random.Range(0, 64);

            int baseIdx = i * 3;
            triangles[baseIdx] = baseIdx;
            triangles[baseIdx + 1] = baseIdx + 1;
            triangles[baseIdx + 2] = baseIdx + 2;
            targetColors[baseIdx] = GetBlueColor();
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
    }

    void Update()
    {
        // 1. Get the latest audio data
        AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        int v = 0;
        int triIndex = 0;
        float time = Time.time * driftSpeed;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 bl = GetDriftedPos(x, y, time);
                Vector3 tl = GetDriftedPos(x, y + 1, time);
                Vector3 tr = GetDriftedPos(x + 1, y + 1, time);
                Vector3 br = GetDriftedPos(x + 1, y, time);

                // Triangle 1 pulse
                float pulse1 = spectrum[triangleFrequencyMap[triIndex]] * pulseSensitivity * (1 + triIndex * 0.05f);
                UpdateTriangle(v, bl, tl, tr, pulse1);
                v += 3;
                triIndex++;

                // Triangle 2 pulse
                float pulse2 = spectrum[triangleFrequencyMap[triIndex]] * pulseSensitivity * (1 + triIndex * 0.05f);
                UpdateTriangle(v, bl, tr, br, pulse2);
                v += 3;
                triIndex++;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
    }

    void UpdateTriangle(int index, Vector3 p1, Vector3 p2, Vector3 p3, float pulse)
    {
        if (Random.value < 0.003f) targetColors[index] = GetBlueColor();

        // Add the pulse to the color to make it "glow"
        Color nextColor = Color.Lerp(colors[index], targetColors[index], Time.deltaTime * colorChangeSpeed);
        Color pulsedColor = nextColor + (new Color(0.5f, 0.5f, 1f) * pulse * pulseIntensity);

        for (int i = 0; i < 3; i++) colors[index + i] = pulsedColor;

        // Optionally, push the vertices forward on the Z axis based on pulse
        Vector3 pulseOffset = Vector3.back * pulse * pulseIntensity;
        vertices[index] = p1 + pulseOffset;
        vertices[index + 1] = p2 + pulseOffset;
        vertices[index + 2] = p3 + pulseOffset;
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
