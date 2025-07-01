using System.Collections.Generic;
using System;
using UnityEngine;
public static class SamplePattern
{
    public static Vector3[] Hemisphere(Vector3 direction, float radius, int numberOfPoints)
    {
        List<Vector3> points = new List<Vector3>();
        direction = Vector3.Normalize(direction);

        var phi = MathF.PI * (3.0f - MathF.Sqrt(5.0f)); // Golden angle in radians

        for (int i = 0; i < numberOfPoints; i++)
        {
            // y goes from 1 (pole) to 0 (equator)
            var y = 1.0f - (float)i / (numberOfPoints - 1);
            var radiusAtY = MathF.Sqrt(radius - y * y);

            var theta = phi * i; // angle            

            var x = MathF.Cos(theta) * radius;
            var z = MathF.Sin(theta) * radius;

            var point = new Vector3(x, y, z);
            points.Add(Vector3.Normalize(point));
        }

        return points.ToArray();
    }

    public static Vector3[] Circle(float radius, int numberOfPoints)
    {
        var samplePoints = new Vector3[numberOfPoints];
        var angleStep = 360f / numberOfPoints;

        for (int i = 0; i < numberOfPoints; i++)
        {
            var angle = i * angleStep * Mathf.Deg2Rad;
            samplePoints[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
        return samplePoints;
    }
}