using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
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

    public class MeshData
    {
        public List<Vector3> Vertices = new List<Vector3>();
        public List<Vector3> Normals = new List<Vector3>();
        public List<Vector2> Uvs = new List<Vector2>();
        public List<int> Triangles = new List<int>();

        public int CurrentIndex = 0;
        public float CurrentUvOffset = 0;
    }

    public Material Material;
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
        var meshFilter = GetComponent<MeshFilter>();
        var meshRender = GetComponent<MeshRenderer>();

        var roundedControlPoints = GenerateCableControlPoints(ControlPoints, SmoothnessLevel);

        var meshData = GenerateMeshData(roundedControlPoints);
        var mesh = new Mesh {
            vertices = meshData.Vertices.ToArray(),
            normals = meshData.Normals.ToArray(),
            uv = meshData.Uvs.ToArray(),
            triangles = meshData.Triangles.ToArray()
        };

        meshFilter.mesh = mesh;
        meshRender.material = Material;
    }

    private List<SplineControlPoint> GenerateCableControlPoints(List<SplineControlPoint> controlPoints, int steps)
    {
        // Just add the first point. It will still be first
        var result = new List<SplineControlPoint> {
            controlPoints[0]
        };

        // Can't have a mesh with a single point
        if (controlPoints.Count < 2) {
            return result;
        }

        foreach (var pair in GetControlPointPairs(controlPoints)) {

            var pairHalfDistance = (pair.Second.Position - pair.First.Position).magnitude / 2;
            var pairStepDistance = 1f / (steps + 1);


            var firstPoint = pair.First.Position;
            var lastPoint = pair.Second.Position;

            // Calculate extra points
            var extraPosition01 = pair.First.Position + pair.First.Direction * Vector3.forward * pairHalfDistance;
            var extraPosition02 = pair.Second.Position + pair.Second.Direction * Vector3.back * pairHalfDistance;

            // Insert N extra control points along the curve.
            for (int i = 0; i < steps; i++) {
                var distanceFactor = (i + 1) * pairStepDistance;
                var position = BezierCurve.CubicCurve(firstPoint, extraPosition01, extraPosition02, lastPoint, distanceFactor);

                var firstRotation = Quaternion.Euler(0, 0, pair.First.Direction.eulerAngles.z);
                var secondRotation = Quaternion.Euler(0, 0, pair.Second.Direction.eulerAngles.z);
                var rotation = Quaternion.Lerp(firstRotation, secondRotation, steps * pairStepDistance);
                var tangent = BezierCurve.CubicCurveDerivative(firstPoint, extraPosition01, extraPosition02, lastPoint, distanceFactor).normalized;

                result.Add(new SplineControlPoint { Position = position, Direction = Quaternion.LookRotation(tangent) * rotation });
            }

            result.Add(pair.Second);
        }

        return result;
    }

    private MeshData GenerateMeshData(List<SplineControlPoint> controlPoints)
    {
        var result = new MeshData();
        AddControlPointToMesh(controlPoints[0], null, result);
        for (int i = 1; i < controlPoints.Count; i++) {
            AddControlPointToMesh(controlPoints[i], controlPoints[i - 1], result);
        }

        return result;
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

    private float GetUvOffset(SplineControlPoint controlPoint, SplineControlPoint lastControlPoint)
    {
        if (lastControlPoint == null) {
            return 0;
        }

        return (lastControlPoint.Position - controlPoint.Position).magnitude;
    }

    private Vector3 GetVectorPosition(Vector3 position, Quaternion rotation, Vector3 direction, float width)
    {
        return position + rotation * direction * width;
    }

    private void AddControlPointToMesh(SplineControlPoint controlPoint, SplineControlPoint lastControlPoint, MeshData meshData)
    {
        // Precalculate shared data
        var radius = Diameter / 2;
        var radiansSteps = (Mathf.PI * 2) / RoundSegments;
        var uvSteps = 1f / RoundSegments;

        meshData.CurrentUvOffset += GetUvOffset(controlPoint, lastControlPoint);

        // Generate data for each vertex
        for (int i = 0; i <= RoundSegments; i++) {
            var localDirection = new Vector3(Mathf.Sin(radiansSteps * i), Mathf.Cos(radiansSteps * i), 0);
            meshData.Vertices.Add(GetVectorPosition(controlPoint.Position, controlPoint.Direction, localDirection, radius));

            // Normals always points straight out
            meshData.Normals.Add(Vector3.up);

            // Wraps perfectly X wise and uses the length of the segment as a base for the Y value.
            meshData.Uvs.Add(new Vector2(uvSteps * i, meshData.CurrentUvOffset * 2));
        }

        // Add 1 to account for it to wrap back on its own start
        var extendedRoundSegments = RoundSegments + 1;
        if (lastControlPoint != null) {
            // Create the triangle list. Each quad has six entries in the list
            for (int i = 0; i < extendedRoundSegments; i++) {
                meshData.Triangles.AddRange(new int[] {
                    meshData.CurrentIndex - extendedRoundSegments + i,
                    meshData.CurrentIndex + i,
                    meshData.CurrentIndex + (i + 1) % extendedRoundSegments,
                    meshData.CurrentIndex + (i + 1) % extendedRoundSegments,
                    meshData.CurrentIndex - extendedRoundSegments + (i + 1) % extendedRoundSegments,
                    meshData.CurrentIndex - extendedRoundSegments + i,
                });
            }
        }

        meshData.CurrentIndex += (RoundSegments + 1);
    }
}
  