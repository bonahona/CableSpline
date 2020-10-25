using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// https://en.wikipedia.org/wiki/B%C3%A9zier_curve
public class BezierCurve
{
    public static Vector3 CubicCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * oneMinusT * p0 + 3f * oneMinusT * oneMinusT * t * p1 + 3f * oneMinusT * t * t * p2 + t * t * t * p3;
    }

    public static Vector3 CubicCurveDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float oneMinusT = 1f - t;
        return 3f * oneMinusT * oneMinusT * (p1 - p0) + 6f * oneMinusT * t * (p2 - p1) + 3f * t * t * (p3 - p2);
    }

    public Vector3 Source;
    public Vector3 SourceTangent;
    public Vector3 End;
    public Vector3 EndTangent;

    public float EstimateBezierCurveLength(int segmentCount)
    {
        var result = 0f;

        var increment = 1f / segmentCount;
        for (int i = 0; i < segmentCount; i++) {
            result += Vector3.Distance(GetBezierCurvePosition(increment * i), GetBezierCurvePosition(increment * (i + 1)));
        }

        return result;
    }

    public Vector3 GetBezierCurvePosition(float value)
    {
        return CubicCurve(Source, End, SourceTangent, EndTangent, value);
    }

    public Vector3 GetCubicCurveDerivative(float value)
    {
        return CubicCurveDerivative(Source, SourceTangent, EndTangent, End, value);
    }
}
