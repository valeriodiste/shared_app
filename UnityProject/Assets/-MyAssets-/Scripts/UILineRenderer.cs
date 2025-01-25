using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UILineRenderer : MaskableGraphic {

	public bool loop;
	public Vector2[] points;

	public bool renderCorners = true;

	public float thickness = 10f;
	public bool center = true;

	// Reference to the corner objects
	private List<RectTransform> cornerObjects;
	public Texture2D cornerTexture;

	protected override void OnPopulateMesh(VertexHelper vh) {

		vh.Clear();

		if (points.Length < 2) return;

		List<Vector2> pointsToConsider = new List<Vector2>(points);
		if (loop) {
			// Add the first point to the end of the list
			pointsToConsider.Add(points[0]);
			pointsToConsider.Add(points[1]);
		}

		for (int i = 0; i < pointsToConsider.Count - 1; i++) {
			// Create a line segment between the next two points
			CreateLineSegment(pointsToConsider[i], pointsToConsider[i + 1], vh);
			
			int index = i * 5;

			// Add the line segment to the triangles array
			vh.AddTriangle(index, index + 1, index + 3);
			vh.AddTriangle(index + 3, index + 2, index);

			// These two triangles create the beveled edges
			// between line segments using the end point of
			// the last line segment and the start points of this one
			if (i != 0) {
				vh.AddTriangle(index, index - 1, index - 3);
				vh.AddTriangle(index + 1, index - 1, index - 2);
			}

		}

	}

	/// <summary>
	/// Creates a rect from two points that acts as a line segment
	/// </summary>
	/// <param name="point1">The starting point of the segment</param>
	/// <param name="point2">The endint point of the segment</param>
	/// <param name="vh">The vertex helper that the segment is added to</param>
	private void CreateLineSegment(Vector3 point1, Vector3 point2, VertexHelper vh) {
		Vector3 offset = center ? (rectTransform.sizeDelta / 2) : Vector2.zero;

		// Create vertex template
		UIVertex vertex = UIVertex.simpleVert;
		vertex.color = color;

		// Create the start of the segment
		Quaternion point1Rotation = Quaternion.Euler(0, 0, RotatePointTowards(point1, point2) + 90);
		vertex.position = point1Rotation * new Vector3(-thickness / 2, 0);
		vertex.position += point1 - offset;
		vh.AddVert(vertex);
		vertex.position = point1Rotation * new Vector3(thickness / 2, 0);
		vertex.position += point1 - offset;
		vh.AddVert(vertex);

		// Create the end of the segment
		Quaternion point2Rotation = Quaternion.Euler(0, 0, RotatePointTowards(point2, point1) - 90);
		vertex.position = point2Rotation * new Vector3(-thickness / 2, 0);
		vertex.position += point2 - offset;
		vh.AddVert(vertex);
		vertex.position = point2Rotation * new Vector3(thickness / 2, 0);
		vertex.position += point2 - offset;
		vh.AddVert(vertex);

		// Also add the end point
		vertex.position = point2 - offset;
		vh.AddVert(vertex);
	}

	// Create the corners of the line as circles shown at the given points
	private void SetCorners(Vector2[] points) {
		// Function to create a single corner
		RectTransform CreateCorner(Vector2 point) {
			GameObject cornerObj = new GameObject("Corner");
			RectTransform corner = cornerObj.AddComponent<RectTransform>();
			corner.SetParent(rectTransform);
			corner.anchorMin = corner.anchorMax = new Vector2(0.5f, 0.5f);
			corner.sizeDelta = new Vector2(thickness * 3, thickness * 3);
			corner.anchoredPosition = point;
			RawImage cornerImage = corner.gameObject.AddComponent<RawImage>();
			cornerImage.texture = cornerTexture;
			cornerImage.color = color;
			cornerObj.transform.localScale = Vector3.one;
			return corner;
		}
		// Create the corner objects if needed
		if (cornerObjects == null) {
			cornerObjects = new List<RectTransform>();
		}
		int numCorners = points.Length > cornerObjects.Count ? points.Length : cornerObjects.Count;
		for (int i = 0; i < numCorners; i++) {
			if (i < points.Length) {
				if (i < cornerObjects.Count) {
					cornerObjects[i].anchoredPosition = points[i];
				} else {
					cornerObjects.Add(CreateCorner(points[i]));
				}
				cornerObjects[i].gameObject.SetActive(true);
			} else {
				if (i < cornerObjects.Count) {
					cornerObjects[i].gameObject.SetActive(false);
				}
			}
		}
	}

	/// <summary>
	/// Gets the angle that a vertex needs to rotate to face target vertex
	/// </summary>
	/// <param name="vertex">The vertex being rotated</param>
	/// <param name="target">The vertex to rotate towards</param>
	/// <returns>The angle required to rotate vertex towards target</returns>
	private float RotatePointTowards(Vector2 vertex, Vector2 target) {
		return (float) (Mathf.Atan2(target.y - vertex.y, target.x - vertex.x) * (180 / Mathf.PI));
	}

	/// <summary>
	/// Set the points of the line renderer
	/// </summary>
	/// <param name="points"></param>
	public void SetPoints(Vector2[] points, bool loop) {
		// Set the point
		this.points = points;
		this.loop = loop;
		// Create the corners
		if (Application.isPlaying && renderCorners) {
			SetCorners(points);
		}
		// Trigger a redraw
		SetAllDirty();
	}

	private float SqrDistance(Vector2 a, Vector2 b) {
		return (a - b).sqrMagnitude;
	}

	/// <summary>
	/// Function to translate a touchPosition on screen (from [0,0] as the lower-left corner to [Screen.width,Screen.height] as top-right corner) to a UI line renderer displayed position
	/// </summary>
	public Vector2 GetRelativePosition(Vector2 touchPosition) {
		Vector2 actualPosition = new Vector2(touchPosition.x / Screen.width, touchPosition.y / Screen.height);   // In coordinates from [0,0] to [1,1]
		actualPosition = new Vector2(actualPosition.x - 0.5f, actualPosition.y - 0.5f) * 2f;   // In coordinates from [-1,-1] to [1,1]
		Vector2 referencePosition = new Vector2(actualPosition.x * rectTransform.rect.width, actualPosition.y * rectTransform.rect.height);   // In coordinates from [-width/2,-height/2] to [width/2,height/2]
		return referencePosition / 2f;
	}

	public bool IsCornerPosition(Vector2 pos, out int cornerIndex) {
		float cornerRadius = thickness * 5f;
		foreach (Vector2 point in points) {
			if (SqrDistance(pos, point) < cornerRadius * cornerRadius) {
				cornerIndex = System.Array.IndexOf(points, point);
				return true;
			}
		}
		cornerIndex = -1;
		return false;
	}

	public bool IsEdgePosition(Vector2 pos, out int edgeIndex) {
		// Check if the position lies on the rectangle formed by each edge (extended by half the thickness)
		float edgeThickness = thickness * 2f;
		List<Vector2> pointsToConsider = new List<Vector2>(points);
		if (loop) {
			pointsToConsider.Add(points[0]);
		}
		bool IsPointInRectangle(Vector2 p, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4) {
			float d1 = (p.x - p1.x) * (p2.y - p1.y) - (p2.x - p1.x) * (p.y - p1.y);
			float d2 = (p.x - p2.x) * (p3.y - p2.y) - (p3.x - p2.x) * (p.y - p2.y);
			float d3 = (p.x - p3.x) * (p4.y - p3.y) - (p4.x - p3.x) * (p.y - p3.y);
			float d4 = (p.x - p4.x) * (p1.y - p4.y) - (p1.x - p4.x) * (p.y - p4.y);
			if (d1 > 0 && d2 > 0 && d3 > 0 && d4 > 0) {
				return true;
			}
			if (d1 < 0 && d2 < 0 && d3 < 0 && d4 < 0) {
				return true;
			}
			return false;
		}
		for (int i = 0; i < pointsToConsider.Count - 1; i++) {
			Vector2 point1 = pointsToConsider[i];
			Vector2 point2 = pointsToConsider[i + 1];
			Vector2 offset = center ? (rectTransform.sizeDelta / 2) : Vector2.zero;
			Vector2 point1Rotation = Quaternion.Euler(0, 0, RotatePointTowards(point1, point2) + 90) * new Vector3(-edgeThickness, 0);
			Vector2 point2Rotation = Quaternion.Euler(0, 0, RotatePointTowards(point2, point1) - 90) * new Vector3(-edgeThickness, 0);
			Vector2 point1Offset = point1 + point1Rotation - offset;
			Vector2 point2Offset = point2 + point2Rotation - offset;
			Vector2 point3Offset = point2 - point2Rotation - offset;
			Vector2 point4Offset = point1 - point1Rotation - offset;
			if (IsPointInRectangle(pos, point1Offset, point2Offset, point3Offset, point4Offset)) {
				edgeIndex = i;
				return true;
			}
		}
		edgeIndex = -1;
		return false;
	}
}
