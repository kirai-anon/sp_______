using UnityEngine;

public class BoundaryDrawer : MonoBehaviour
{

    [SerializeField] private int Size = 7;
    void Start()
    {
        LineRenderer line = GetComponent<LineRenderer>();
        
        Vector3[] points = new Vector3[4];
        points[0] = new Vector3(-Size, Size, 1);
        points[1] = new Vector3(-Size, -Size, 1);
        points[2] = new Vector3(Size, -Size, 1);
        points[3] = new Vector3(Size, Size, 1);

        line.positionCount = points.Length;
        line.SetPositions(points);

        // Make it look nice
        line.startWidth = 0.2f;
        line.endWidth = 0.2f;
        line.useWorldSpace = true;
    }
}