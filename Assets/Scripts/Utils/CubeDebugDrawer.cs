using UnityEngine;

public static class CubeDebugDrawer
{
    public static Vector3 cubeSize = Vector3.one; // Customize the cube size

    public static void DrawCube(Vector3 position)
    {
        Vector3 center = position;
        Vector3 halfSize = cubeSize * 0.5f;

        // Define cube vertices
        Vector3[] vertices = new Vector3[]
        {
            center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
            center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
            center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
            center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
            center + new Vector3(halfSize.x, halfSize.y, halfSize.z),
            center + new Vector3(-halfSize.x, halfSize.y, halfSize.z)
        };

        // Draw cube edges
        for (int i = 0; i < 4; i++)
        {
            Debug.DrawLine(vertices[i], vertices[(i + 1) % 4], Color.red);
            Debug.DrawLine(vertices[i + 4], vertices[(i + 1) % 4 + 4], Color.red);
            Debug.DrawLine(vertices[i], vertices[i + 4], Color.red);
        }
    }
}
