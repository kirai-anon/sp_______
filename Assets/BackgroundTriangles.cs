using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FlatLandscape : MonoBehaviour
{
    [SerializeField] public AudioSource targetMusicSource;

    public int width = 10;
    public int height = 10;
    public float spacing = 1f;
    public float baseJitter = 0.5f;

    [Header("Movement")]
    public float jitterAmount = 0.4f;
    public float driftSpeed = 0.5f;

    [Header("Color Shifting")]
    public float colorChangeSpeed = 1.5f;
    public Color pulseGlowColor = new Color(0.2f, 0.6f, 1f);

    [Header("Audio Settings")]
    public float pulseIntensity = 2f;
    public float pulseSensitivity = 15f;
    [Range(1f, 30f)] public float fallSpeed = 12f; // Smooths out jittery frames

    private float[] spectrum = new float[512];
    private float[] smoothedSpectrum = new float[64];
    private int[] triangleFrequencyMap;

    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;
    private Color[] targetColors;
    private Vector3[,] baseGrid;

    public void SetOffset(Vector2 offset)
    {
        transform.position = new Vector3(offset.x, offset.y, transform.position.z);
    }

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
                    10f
                );

        int triCount = width * height * 2;
        vertices = new Vector3[triCount * 3];
        colors = new Color[triCount * 3];
        targetColors = new Color[triCount * 3];
        triangleFrequencyMap = new int[triCount];

        int[] triangles = new int[triCount * 3];

        for (int i = 0; i < triCount; i++)
        {
            triangleFrequencyMap[i] = Random.Range(0, 64);

            int baseIdx = i * 3;
            triangles[baseIdx] = baseIdx;
            triangles[baseIdx + 1] = baseIdx + 1;
            triangles[baseIdx + 2] = baseIdx + 2;

            Color startCol = GetBlueColor();
            targetColors[baseIdx] = startCol;
            colors[baseIdx] = startCol;
            colors[baseIdx + 1] = startCol;
            colors[baseIdx + 2] = startCol;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
    }

    void Update()
    {
        if (targetMusicSource != null && targetMusicSource.isPlaying)
        {
            targetMusicSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

            // Process and smooth the first 64 frequency bins
            for (int i = 0; i < 64; i++)
            {
                // Boost higher frequencies so trebles/mids are as visible as bass
                float frequencyBoost = 1f + (i * 0.25f);
                // Sqrt compresses extreme bass spikes and lifts quiet beats
                float rawValue = Mathf.Sqrt(spectrum[i] * frequencyBoost) * pulseSensitivity;

                // Snap up instantly on beats, decay smoothly when quiet
                if (rawValue > smoothedSpectrum[i])
                    smoothedSpectrum[i] = rawValue;
                else
                    smoothedSpectrum[i] = Mathf.Lerp(smoothedSpectrum[i], rawValue, Time.deltaTime * fallSpeed);
            }
        }

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

                float pulse1 = smoothedSpectrum[triangleFrequencyMap[triIndex]];
                UpdateTriangle(v, bl, tl, tr, pulse1);
                v += 3;
                triIndex++;

                float pulse2 = smoothedSpectrum[triangleFrequencyMap[triIndex]];
                UpdateTriangle(v, bl, tr, br, pulse2);
                v += 3;
                triIndex++;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.RecalculateBounds(); // Prevents camera frustum culling when vertices move
    }

    void UpdateTriangle(int index, Vector3 p1, Vector3 p2, Vector3 p3, float pulse)
    {
        if (Random.value < 0.001f) targetColors[index] = GetBlueColor();

        // Smoothly drift the base ambient color
        Color baseAmbient = Color.Lerp(colors[index], targetColors[index], Time.deltaTime * colorChangeSpeed);

        // Add the audio glow on top of the base color
        Color finalColor = baseAmbient + (pulseGlowColor * pulse);
        for (int i = 0; i < 3; i++) colors[index + i] = finalColor;

        // Push forward on Z-axis
        Vector3 pulseOffset = Vector3.back * (pulse * pulseIntensity);
        vertices[index] = p1 + pulseOffset;
        vertices[index + 1] = p2 + pulseOffset;
        vertices[index + 2] = p3 + pulseOffset;
    }

    Vector3 GetDriftedPos(int x, int y, float t)
    {
        Vector3 basePos = baseGrid[x, y];
        float offsetX = (Mathf.PerlinNoise(x * 0.5f + t, y * 0.5f) - 0.5f) * jitterAmount;
        float offsetY = (Mathf.PerlinNoise(x * 0.5f, y * 0.5f + t) - 0.5f) * jitterAmount;
        return basePos + new Vector3(offsetX, offsetY, 0);
    }

    Color GetBlueColor()
    {
        float s = Random.Range(0.01f, 0.04f);
        return new Color(s, s, s * 2.5f);
    }
}
