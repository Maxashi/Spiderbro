using System.Collections.Generic;
using System;
using UnityEngine;
public static class SamplePattern
{

    public static Vector3[] Hemisphere(Vector3 direction, float radius, int numberOfPoints, float maxAngleDegrees = 180f)
    {
        List<Vector3> points = new List<Vector3>();
        direction = Vector3.Normalize(direction);

        var phi = MathF.PI * (3.0f - MathF.Sqrt(5.0f)); // Golden angle in radians
        var maxAngleRad = maxAngleDegrees * Mathf.Deg2Rad;

        for (int i = 0; i < numberOfPoints; i++)
        {
            // y goes from 1 (pole) to cos(maxAngle) (limited spread)
            var y = 1.0f - (float)i / (numberOfPoints - 1) * (1.0f - MathF.Cos(maxAngleRad));
            var radiusAtY = MathF.Sqrt(1.0f - y * y);

            var theta = phi * i; // angle            

            var x = MathF.Cos(theta) * radiusAtY;
            var z = MathF.Sin(theta) * radiusAtY;

            var point = new Vector3(x, y, z);
            
            // Rotate the point to align with the given direction
            var rotation = Quaternion.FromToRotation(Vector3.up, direction);
            point = rotation * point;
            
            points.Add(point * radius);
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