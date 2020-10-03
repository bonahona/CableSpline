using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CableSpline : MonoBehaviour
{
    public const float DefaultDiameter = 0.25f;

    [System.Serializable]
    public class SplineControlPoint {
        public Vector3 Position;
        public Quaternion Direction = Quaternion.identity;
    }

    [System.Serializable]
    public class ControlPointPair {
        public SplineControlPoint First;
        public SplineControlPoint Second;

        public ControlPointPair(SplineControlPoint first, SplineControlPoint second) {
            First = first;
            Second = second;
        }
    }

    public int SmoothnessLevel = 5;         // Additional segments inserted between the placed control points
    public int RoundSegments = 10;
    public float Diameter = DefaultDiameter;
    public bool IsEditable = false;         // If true, the edit path button is pressed down in the editor

    public List<SplineControlPoint> ControlPoints;

    public void Reset()
    {
        ControlPoints = new List<SplineControlPoint> {
            new SplineControlPoint {
                Position = new Vector3(0f, 0f, 0f), Direction = Quaternion.LookRotation(Vector3.right)
            }
        };
    }

    public void AddControlPoint(Vector3 position)
    {
        var lastControlPoint = ControlPoints.Last();
        var directionOffset = position - (transform.position + lastControlPoint.Position);
        directionOffset.y = 0;
        var direction = Quaternion.LookRotation(directionOffset);

        var targetPosition = position - transform.position;
        targetPosition.y = lastControlPoint.Position.y;

        var controlPoint = new SplineControlPoint { Position = targetPosition, Direction = direction };
        ControlPoints.Add(controlPoint);
        UpdateMesh();
    }

    public void InsertControlPoint(ControlPointPair controlPoints, Vector3 position)
    {

        var targetPosition = position - transform.position;
        var direction = Quaternion.Slerp(controlPoints.First.Direction, controlPoints.Second.Direction, 0.5f);

        var controlPoint = new SplineControlPoint { Position = targetPosition, Direction = direction };

        var insertIndex = Mathf.Max(ControlPoints.IndexOf(controlPoints.First), ControlPoints.IndexOf(controlPoints.Second));
        ControlPoints.Insert(insertIndex, controlPoint);
        UpdateMesh();
    }

    public void RemoveControlPoint(SplineControlPoint controlPoint)
    {
        ControlPoints.Remove(controlPoint);
        UpdateMesh();
    }

    public void UpdateMesh()
    {
        // TODO: Implement me
    }

    public List<ControlPointPair> GetControlPointPairs(List<SplineControlPoint> controlPoints)
    {
        var result = new List<ControlPointPair>();
        if (controlPoints.Count < 2) {
            return result;
        }

        for (int i = 0; i < controlPoints.Count - 1; i++) {
            result.Add(new ControlPointPair(controlPoints[i], controlPoints[i + 1]));
        }

        return result;
    }
}
  