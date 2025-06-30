using System.Collections.Generic;
using System;
using UnityEngine;
public static class SamplePattern
{
    public static Vector3[] Hemisphere(Vector3 direction, float radius, int numberOfPoints)
    {
        List<Vector3> points = new List<Vector3>();
        direction = Vector3.Normalize(direction);

        float phi = MathF.PI * (3.0f - MathF.Sqrt(5.0f)); // Golden angle in radians

        for (int i = 0; i < numberOfPoints; i++)
        {
            // y goes from 1 (pole) to 0 (equator)
            float y = 1.0f - (float)i / (numberOfPoints - 1);
            float radiusAtY = MathF.Sqrt(radius - y * y);

            float theta = phi * i; // angle

            float x = MathF.Cos(theta) * radius;
            float z = MathF.Sin(theta) * radius;

            Vector3 point = new Vector3(x, y, z);
            points.Add(Vector3.Normalize(point));
        }

        return points.ToArray();
    }

    public static Vector3[] Circle(float radius, int numberOfPoints)
    {
        var samplePoints = new Vector3[numberOfPoints];
        float angleStep = 360f / numberOfPoints;

        for (int i = 0; i < numberOfPoints; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;


            samplePoints[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
        return samplePoints;
    }
}