using UnityEngine;

/// <summary>
/// Utility for drawing a transparent icosphere mesh in the Scene view, similar to Debug.DrawWireSphere.
/// </summary>
public static class DebugGizmos
{
    private static Color defaultColor = new(0.7f, 0.7f, 0.7f, 0.5f); // Default color with 50% transparency 

    /// <summary>
    /// Draws a transparent icosphere mesh at the given position and radius.
    /// Only visible in the Scene view (Gizmos).
    /// </summary>
    /// <param name="position">Center of the sphere</param>
    /// <param name="radius">Radius of the sphere</param>
    /// <param name="color">Color of the sphere (alpha controls transparency)</param>
    /// <param name="subdivisions">Number of icosphere subdivisions (0 = icosahedron, 1 = finer, etc.)</param>
    public static void Draw(Vector3 position, float radius, Color color, int subdivisions = 1)
    {
#if UNITY_EDITOR
        Mesh mesh = GenerateIcoSphere(radius, subdivisions);
        if (mesh == null) return;
        color.a = Mathf.Clamp01(color.a);
        Gizmos.color = color;
        Gizmos.DrawMesh(mesh, position);
#endif
    }

    /// <summary>
    /// Draws a transparent icosphere mesh at the given position and radius.
    /// Only visible in the Scene view (Gizmos).
    /// </summary>
    /// <param name="position">Center of the sphere</param>
    /// <param name="radius">Radius of the sphere</param>
    /// <param name="subdivisions">Number of icosphere subdivisions (0 = icosahedron, 1 = finer, etc.)</param>
    public static void Draw(Vector3 position, float radius, int subdivisions = 1)
    {
#if UNITY_EDITOR
        Draw(position, radius, defaultColor, subdivisions);
#endif
    }

#if UNITY_EDITOR
    // Generates an icosphere mesh with the given radius and subdivisions
    private static Mesh GenerateIcoSphere(float radius, int subdivisions)
    {
        // Golden ratio
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var vertices = new System.Collections.Generic.List<Vector3>
        {
            new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
            new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
            new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1)
        };
        for (int i = 0; i < vertices.Count; i++)
            vertices[i] = vertices[i].normalized * radius;

        var faces = new System.Collections.Generic.List<int[]>
        {
            new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
            new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
            new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1}
        };

        // Subdivide faces
        var midCache = new System.Collections.Generic.Dictionary<long, int>();
        for (int s = 0; s < subdivisions; s++)
        {
            var newFaces = new System.Collections.Generic.List<int[]>();
            foreach (var tri in faces)
            {
                int a = tri[0], b = tri[1], c = tri[2];
                int ab = GetMidpoint(vertices, midCache, a, b, radius);
                int bc = GetMidpoint(vertices, midCache, b, c, radius);
                int ca = GetMidpoint(vertices, midCache, c, a, radius);
                newFaces.Add(new[] { a, ab, ca });
                newFaces.Add(new[] { b, bc, ab });
                newFaces.Add(new[] { c, ca, bc });
                newFaces.Add(new[] { ab, bc, ca });
            }
            faces = newFaces;
        }

        // Build mesh
        var mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        mesh.SetVertices(vertices);
        var triangles = new System.Collections.Generic.List<int>();
        foreach (var tri in faces)
        {
            triangles.Add(tri[0]);
            triangles.Add(tri[1]);
            triangles.Add(tri[2]);
        }
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    // Helper for midpoint vertex caching
    private static int GetMidpoint(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.Dictionary<long, int> cache, int i0, int i1, float radius)
    {
        long key = ((long)Mathf.Min(i0, i1) << 32) + Mathf.Max(i0, i1);
        if (cache.TryGetValue(key, out int idx)) return idx;
        Vector3 v = ((verts[i0] + verts[i1]) * 0.5f).normalized * radius;
        verts.Add(v);
        idx = verts.Count - 1;
        cache[key] = idx;
        return idx;
    }
#endif
}
