using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(CableSpline))]
public class CableSplineEditor: Editor {

    [MenuItem("GameObject/3D Object/Cable Spline")]
    public static void CreateCableSpline() {
        var position = GetMiddleOfViewPort();
        var connectionSystem = CreateCableSplineObject(position);

        connectionSystem.IsEditable = true;
        Selection.activeGameObject = connectionSystem.gameObject;

    }

    private static Vector3 GetMiddleOfViewPort() {
        var middleOfViewRay = SceneView.lastActiveSceneView.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 1));
        if (Physics.Raycast(middleOfViewRay, out RaycastHit rayCasthit)) {
            return rayCasthit.point;
        } else {
            return new Vector3(0, 0, 0);
        }
    }

    private static CableSpline CreateCableSplineObject(Vector3 position) {
        var connectionGameObject = new GameObject("CableSpline");
        var connectionSystem = connectionGameObject.AddComponent<CableSpline>();
        connectionSystem.transform.position = position;

        return connectionSystem;
    }

    public class InScenePosition
    {
        public Vector3 Position;
        public bool IsInWorld;
        public bool IsInCable;
        public CableSpline.ControlPointPair ControlPoints;
    }

    private readonly RaycastHit[] RaycastHits = new RaycastHit[128];
    private GUIStyle EditorTextStyle;

    public override void OnInspectorGUI() {
        var cableSpline = target as CableSpline;
        EditorGUI.BeginChangeCheck();
        cableSpline.SmoothnessLevel = Mathf.Clamp(EditorGUILayout.IntField("Smoothness Level", cableSpline.SmoothnessLevel), 0, 10);
        cableSpline.RoundSegments = Mathf.Clamp(EditorGUILayout.IntField("Roundness", cableSpline.RoundSegments), 3, 30);
        cableSpline.Diameter = Mathf.Clamp(EditorGUILayout.FloatField("Diameter", cableSpline.Diameter), 0.01f, 10);
        cableSpline.Material = EditorGUILayout.ObjectField("Material", cableSpline.Material, typeof(Material), false) as Material;

        EditorGUILayout.Space();

        cableSpline.IsEditable = GUILayout.Toggle(cableSpline.IsEditable, "Edit path", "Button", GUILayout.Height(24));

        if (EditorGUI.EndChangeCheck()) {
            cableSpline.UpdateMesh();
        }
    }

    public void OnEnable() {
        SceneView.duringSceneGui += OnSceneView;
    }

    public void OnDisable() {
        SceneView.duringSceneGui -= OnSceneView;

        var connection = target as CableSpline;
        if (connection == null) {
            return;
        }

        connection.IsEditable = Selection.activeGameObject == connection.gameObject;
    }

    public void OnSceneView(SceneView sceneView) {
        if(EditorTextStyle == null) {
            EditorTextStyle = CreateInSceneTextStyle();
        }

        var cable = target as CableSpline;
        if (!cable.IsEditable) {
            return;
        }

        // Get lone rights to capture the input
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        ShowControlPoints(cable);
        DrawCurvedLine(cable);
        HandleEvent(Event.current, cable);

        sceneView.Repaint();
    }

    private void HandleEvent(Event currentEvent, CableSpline cable){
        var inScenePosition = GetInScenePoint(Event.current.mousePosition, cable);

        if (currentEvent.control) {
            AddContolPoint(currentEvent, cable, inScenePosition);
        } else if (currentEvent.shift) {
            RemoveControlPoint(currentEvent, cable, inScenePosition);
        } else {
            Handles.Label(inScenePosition.Position + Vector3.down * 2, "Hold control to place point\nHold shift to remove a point\nPress space to release", EditorTextStyle);
        }

        // Space releases the editing of this cable
        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Space) {
            cable.IsEditable = false;
            currentEvent.Use();
        }
    }

    private void AddContolPoint(Event currentEvent, CableSpline cable, InScenePosition inScenePosition){
        var lastPoint = cable.ControlPoints.Last();
        var lastPointPosition = cable.transform.position + lastPoint.Position; 

        if (inScenePosition.IsInCable) {
            if (currentEvent.type == EventType.MouseDown) {
                Undo.RecordObject(cable, "Inserted control point");
                cable.InsertControlPoint(inScenePosition.ControlPoints, inScenePosition.Position);
                currentEvent.Use();
            }
        } else {
            Handles.DrawLine(lastPointPosition, inScenePosition.Position);

            if (currentEvent.type == EventType.MouseDown) {
                Undo.RecordObject(cable, "Added additional control point");
                cable.AddControlPoint(inScenePosition.Position);
                currentEvent.Use();
            }
        }
    }

    private InScenePosition GetInScenePoint(Vector2 position, CableSpline cable)
    {
        if (Physics.Raycast(HandleUtility.GUIPointToWorldRay(position), out RaycastHit raycastHit)) {
            if (raycastHit.collider.gameObject == cable.gameObject) {
                return new InScenePosition { Position = raycastHit.point, IsInWorld = true, IsInCable = true, ControlPoints = GetSelectedControlPoint(raycastHit, cable) };
            } else {
                return new InScenePosition { Position = raycastHit.point, IsInWorld = true, IsInCable = false };
            }
        } else {
            return new InScenePosition { Position = Vector3.zero, IsInWorld = false };
        }
    }

    private CableSpline.ControlPointPair GetSelectedControlPoint(RaycastHit raycastHit, CableSpline connection)
    {
        // Each segment consist of a |RoundSegments| quad, for a total of RoundSegments * 2 tris or RoundSegments * 6 vertices.
        var segmentIndex = raycastHit.triangleIndex / (2 * connection.RoundSegments);
        var placedSegmentIndex = Mathf.Clamp((segmentIndex + 1) / (connection.SmoothnessLevel + 1), 0, int.MaxValue);
        var previousSegmentIndex = Mathf.Clamp(segmentIndex / (connection.SmoothnessLevel + 1), 0, int.MaxValue);

        if (previousSegmentIndex == placedSegmentIndex) {
            previousSegmentIndex = placedSegmentIndex + 1;
        }

        return new CableSpline.ControlPointPair(connection.ControlPoints[placedSegmentIndex], connection.ControlPoints[previousSegmentIndex]);
    }


    private void RemoveControlPoint(Event currentEvent, CableSpline cable, InScenePosition inScenePosition){
        if (currentEvent.type == EventType.MouseDown) {
            Undo.RecordObject(cable, "Removed control point");
            cable.RemoveControlPoint(inScenePosition.ControlPoints.First);
            currentEvent.Use();
        }
    }

    private GUIStyle CreateInSceneTextStyle(){
        return new GUIStyle() {
            normal = new GUIStyleState {
                textColor = Color.white,
            },
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };
    }

    private void ShowControlPoints(CableSpline cable) {
        foreach (var point in cable.ControlPoints) {
            ShowControlPoint(point, cable);
        }
    }

    private void ShowControlPoint(CableSpline.SplineControlPoint controlPoint, CableSpline cable){
        var position = cable.transform.position + controlPoint.Position;
        float size = HandleUtility.GetHandleSize(position) * 1f;

        EditorGUI.BeginChangeCheck();
        controlPoint.Position = Handles.DoPositionHandle(position, controlPoint.Direction) - cable.transform.position;
        controlPoint.Direction = Handles.DoRotationHandle(controlPoint.Direction, position);
        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(cable, "Edited Cable control point");
            cable.UpdateMesh();
            EditorUtility.SetDirty(cable);
        }
    }

    private void DrawCurvedLine(CableSpline cable){
        var leftRotation = Quaternion.LookRotation(Vector3.left);
        var rightotation = Quaternion.LookRotation(Vector3.right);

        foreach (var pair in cable.GetControlPointPairs(cable.ControlPoints)) {
            var distance = (pair.Second.Position - pair.First.Position).magnitude / 3;
            var firstPoint = pair.First.Position + cable.transform.position;
            var lastPoint = pair.Second.Position + cable.transform.position;
            var extraPosition01 = pair.First.Position + pair.First.Direction * Vector3.forward * distance + cable.transform.position;
            var extraPosition02 = pair.Second.Position + pair.Second.Direction * Vector3.back * distance + cable.transform.position;

            Handles.DrawBezier(firstPoint, lastPoint, extraPosition01, extraPosition02, Color.green, null, 2);
        }
    }
}
