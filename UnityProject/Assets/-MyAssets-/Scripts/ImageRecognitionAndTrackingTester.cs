#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TestAlgorithm {
	public string Name;
	public Func<Texture2D, Pose?> TrackObject;
	public Func<Texture2D, Pose?> EstimatePose;
}

public class AlgorithmTester : MonoBehaviour {

	public List<TestAlgorithm> AlgorithmsToTest_Recognition;
	public List<TestAlgorithm> AlgorithmsToTest_Tracking;
	public Texture2D TestImage; // Placeholder for input images
	public List<Pose> GroundTruthPoses; // Ground truth for accuracy tests
	public int TestFrames = 10; // Number of frames to test image tracking

	private void Start() {
		// Get the custom image recognition module and the custom image tracking module
		ImageTracker imageTracker = FindObjectOfType<ImageTracker>();
		CustomImageRecognitionModule imageRecognitionModule = FindObjectOfType<CustomImageRecognitionModule>();
		CustomImageTrackingModule imageTrackingModule = FindObjectOfType<CustomImageTrackingModule>();
		// Get the list of algorithms to test
		AlgorithmsToTest_Recognition = new List<TestAlgorithm>() {
			new TestAlgorithm {
				Name = "ARFoundationNative",
				EstimatePose = (Texture2D image) => {
					Transform objectTransform = imageTracker.GetARObjects().First().Value.transform;
					return new Pose(objectTransform.position / objectTransform.localScale.x, objectTransform.rotation);
				},
				TrackObject = null
			},
			new TestAlgorithm {
				Name = "ORB",
				EstimatePose = null,
				TrackObject = (Texture2D image) => {
					return imageRecognitionModule.GetTrackImagePose(image, CustomImageRecognitionModule.FeatureAlgorithm.ORB);
				}
			},
			new TestAlgorithm {
				Name = "SIFT",
				EstimatePose = null,
				TrackObject = (Texture2D image) => {
					return imageRecognitionModule.GetTrackImagePose(image, CustomImageRecognitionModule.FeatureAlgorithm.SIFT);
				}
			},
			new TestAlgorithm {
				Name = "SURF",
				EstimatePose = null,
				TrackObject = (Texture2D image) => {
					return imageRecognitionModule.GetTrackImagePose(image, CustomImageRecognitionModule.FeatureAlgorithm.SURF);
				}
			},
			new TestAlgorithm {
				Name = "AKAZE",
				EstimatePose = null,
				TrackObject = (Texture2D image) => {
					return imageRecognitionModule.GetTrackImagePose(image, CustomImageRecognitionModule.FeatureAlgorithm.AKAZE);
				}
			}
		};
		AlgorithmsToTest_Tracking = new List<TestAlgorithm>() {
			new TestAlgorithm {
				Name = "ARFoundationNative",
				EstimatePose = null,
				TrackObject = (Texture2D image) => {
					Transform objectTransform = imageTracker.GetARObjects().First().Value.transform;
					return new Pose(objectTransform.position / objectTransform.localScale.x, objectTransform.rotation);
				}
			},
			new TestAlgorithm {
				Name = "ImageTracking",
				EstimatePose = null,
				TrackObject = (Texture2D image) => {
					return imageTrackingModule.GetTrackImagePose(image, CustomImageTrackingModule.TrackingMode.ImageTracking);
				}
			},
			new TestAlgorithm {
				Name = "SensorTracking",
				EstimatePose = null,
				TrackObject = (Texture2D image) => {
					return imageTrackingModule.GetTrackImagePose(image, CustomImageTrackingModule.TrackingMode.SensorTracking);
				}
			},
			new TestAlgorithm {
				Name = "SimpleFusionTracking",
				EstimatePose = null,
				TrackObject = (Texture2D image) => {
					return imageTrackingModule.GetTrackImagePose(image, CustomImageTrackingModule.TrackingMode.SimpleFusionTracking);
				}
			},
			new TestAlgorithm {
				Name = "KalmanFusionTracking",
				EstimatePose = null,
				TrackObject = (Texture2D image) => {
					return imageTrackingModule.GetTrackImagePose(image, CustomImageTrackingModule.TrackingMode.KalmanFusionTracking);
				}
			}
		};
		// Get the names of the assets of the ground truth coordinates file and the marker data file for the given sample's number
		string[] GetSampleNames(int sampleNumber) {
			string paddedSampleNumber = sampleNumber.ToString().PadLeft(2, '0');
			return new string[] {
				$"test_video_{paddedSampleNumber}_coordinates.json",
				$"test_marker_{paddedSampleNumber}_data.json"
			};
		}
		// Get the ground truth poses for all the samples
		int totalSamples = 5;
		for (int i = 1; i <= totalSamples; i++) {
			string[] assetsNames = GetSampleNames(i);
			string coordinatesFilePath = assetsNames[0];
			string imageDataFilePath = assetsNames[1];
			GroundTruthPoses.AddRange(GetGroundTruthPoses(assetsNames[0], assetsNames[1]));
		}
		// Run the tests for the image recognition and pose estimation modules
		StartCoroutine(TestImageRecognitionAndPoseEstimation());
		// Run the tests for the image tracking module
		StartCoroutine(TestImageTracking());
	}

	// Class for storing the ground truth information for the test images containing a list of coordinates for the 4 corners of the anchor image
	public class GroundTruthImagePose {
		public string imageName;
		public List<List<float>> controlPoints;
	}

	// Loads the JSON file with the ground truth poses stored as a list of dictionaries containing the coordinates of the 4 corners of an anchor image
	public List<Pose> GetGroundTruthPoses(string jsonFilePathCoordinates, string jsonFilePathImageData) {
		string jsonCoordinatesString = System.IO.File.ReadAllText(jsonFilePathCoordinates);
		List<GroundTruthImagePose> jsonCoordinatesList = JsonUtility.FromJson<List<GroundTruthImagePose>>(jsonCoordinatesString);
		string jsonImageDataString = System.IO.File.ReadAllText(jsonFilePathImageData);
		Dictionary<string, float> jsonImageDataList = JsonUtility.FromJson<Dictionary<string, float>>(jsonImageDataString);
		List<Pose> groundTruthPoses = new List<Pose>();
		foreach (var jsonPose in jsonCoordinatesList) {
			Vector2[] corners = new Vector2[4];
			for (int i = 0; i < 4; i++) {
				corners[i] = new Vector2(jsonPose.controlPoints[i][0], jsonPose.controlPoints[i][1]);
			}
			float[] sizes = new float[2];
			sizes[0] = jsonImageDataList["width"];
			sizes[1] = jsonImageDataList["height"];
			Vector2 size = new Vector2(sizes[0], sizes[1]);
			groundTruthPoses.Add(TranslateCornersPositionsToPose(corners, size));
		}
		return groundTruthPoses;
	}

	// Converts an array of 4 corners (top left, top right, bottom left, bottom right) to a pose given the width and height of the anchor image
	public Pose TranslateCornersPositionsToPose(Vector2[] corners, Vector2 size) {
		if (corners == null || corners.Length != 4) {
			throw new System.ArgumentException("Corners array must contain exactly 4 elements.");
		}
		
		// Camera resolution and field of view in degrees
		Vector2 cameraResolution = new Vector2(1920, 1080);
		float cameraFOV = 60.0f;

		// Convert corners coordinates from [0, 1080] and [0,1920] to [-1, 1]
		for (int i = 0; i < corners.Length; i++) {
			corners[i].x = (corners[i].x / cameraResolution.x) * 2 - 1;
			corners[i].y = (corners[i].y / cameraResolution.y) * 2 - 1;
		}

		// Calculate the center of the image from the corners
		Vector2 imageCenter2D = (corners[0] + corners[1] + corners[2] + corners[3]) / 4.0f;

		// Calculate width and height in world units based on the corners
		float width = Vector2.Distance(corners[0], corners[1]);
		float height = Vector2.Distance(corners[0], corners[2]);

		// Calculate the scale factor (assume the provided size is in some world space units)
		float scaleX = size.x / width;
		float scaleY = size.y / height;

		// Adjust the image center to reflect scaling
		Vector3 scaledCenter = new Vector3(imageCenter2D.x * scaleX, imageCenter2D.y * scaleY, 0);

		// Define the image's 3D position assuming it's facing the camera
		float cameraFOVRadians = Mathf.Deg2Rad * cameraFOV;
		float fixedDistance = (size.y / 2.0f) / Mathf.Tan(Mathf.Deg2Rad * cameraFOVRadians / 2.0f);

		// The image center in 3D space
		Vector3 imageCenter3D = new Vector3(scaledCenter.x, scaledCenter.y, fixedDistance);

		// Calculate the orientation of the image
		Vector3 right = (corners[1] - corners[0]).normalized;
		Vector3 up = (corners[2] - corners[0]).normalized;
		Vector3 forward = Vector3.Cross(right, up).normalized;

		Quaternion orientation = Quaternion.LookRotation(forward, up);

		// Return the Pose containing the position and rotation
		return new Pose(imageCenter3D, orientation);
	}

	// Converts a pose to an array of 4 corners (top-left, top-right, bottom-left, bottom-right coordinates) given the width and height of the anchor image 
	public Vector2[] TranslatePoseToCornersPositions(Pose pose, Vector2 size) {
		if (pose == null) {
			throw new System.ArgumentException("Pose cannot be null.");
		}

		// Camera resolution and field of view in degrees
		Vector2 cameraResolution = new Vector2(1920, 1080);
		float cameraFOV = 60.0f;

		// Extract the position and rotation from the pose
		Vector3 position = pose.position;
		Quaternion rotation = pose.rotation;

		// Calculate the distance to the image in world space based on FOV and the vertical resolution
		float cameraFOVRadians = Mathf.Deg2Rad * cameraFOV;
		float distance = (size.y / 2.0f) / Mathf.Tan(cameraFOVRadians / 2.0f);

		// Calculate the aspect ratio of the camera and the scaling factors for width and height
		float aspectRatio = cameraResolution.x / cameraResolution.y;
		float scaledWidth = size.x / aspectRatio;
		float scaledHeight = size.y;

		// Calculate the half-width and half-height of the image in world units
		float halfWidth = scaledWidth / 2.0f;
		float halfHeight = scaledHeight / 2.0f;

		// Calculate the local corners of the image in its local space
		Vector3 topLeft = new Vector3(-halfWidth, halfHeight, distance);
		Vector3 topRight = new Vector3(halfWidth, halfHeight, distance);
		Vector3 bottomLeft = new Vector3(-halfWidth, -halfHeight, distance);
		Vector3 bottomRight = new Vector3(halfWidth, -halfHeight, distance);

		// Transform the local corners to world space using the pose
		topLeft = position + rotation * topLeft;
		topRight = position + rotation * topRight;
		bottomLeft = position + rotation * bottomLeft;
		bottomRight = position + rotation * bottomRight;

		// Convert the world space positions back to 2D
		Vector2[] corners = new Vector2[4];
		corners[0] = new Vector2(topLeft.x, topLeft.y);
		corners[1] = new Vector2(topRight.x, topRight.y);
		corners[2] = new Vector2(bottomLeft.x, bottomLeft.y);
		corners[3] = new Vector2(bottomRight.x, bottomRight.y);

		// Convert coordinates from [-1, 1] to [0, 1080] and [0,1920] as pixel screens
		for (int i = 0; i < corners.Length; i++) {
			corners[i].x = (corners[i].x + 1) * cameraResolution.x / 2;
			corners[i].y = (corners[i].y + 1) * cameraResolution.y / 2;
		}

		return corners;
	}


	// Test image recognition and pose estimation modules
	private IEnumerator TestImageRecognitionAndPoseEstimation() {
		Debug.Log("Starting Image Recognition and Pose Estimation Tests...");
		foreach (var algorithm in AlgorithmsToTest_Recognition) {
			Debug.Log($"Testing Algorithm: {algorithm.Name}");

			// Measure time for pose estimation
			var startTime = Time.realtimeSinceStartup;
			var estimatedPose = algorithm.EstimatePose(TestImage);
			yield return null; // Wait for the next frame to simulate processing time
			var endTime = Time.realtimeSinceStartup;
			Debug.Log($"Time Taken: {(endTime - startTime) * 1000} ms");

			// Measure accuracy (compare to ground truth if available)
			if (GroundTruthPoses.Count > 0) {
				Pose groundTruthPose = GroundTruthPoses[0]; // Assuming a single pose ground truth for this example
				var position = groundTruthPose.position;
				var rotation = groundTruthPose.rotation;
				float positionError = Vector3.Distance(position, estimatedPose?.position ?? Vector3.zero);
				float rotationError = Quaternion.Angle(rotation, estimatedPose?.rotation ?? Quaternion.identity);
				Debug.Log($"Position Error: {positionError:F4}");
				Debug.Log($"Rotation Error: {rotationError:F4}");
			}
		}
	}

	// Test image tracking module
	private IEnumerator TestImageTracking() {

		Debug.Log("Starting Image Tracking Tests...");

		foreach (var algorithm in AlgorithmsToTest_Recognition) {
			Debug.Log($"Testing Algorithm: {algorithm.Name}");

			yield return null; // Wait for the next frame to simulate processing time

			// Measure FPS
			var frameStartTime = Time.realtimeSinceStartup;
			for (int i = 0; i < TestFrames; i++) {
				algorithm.TrackObject(TestImage);
				yield return null; // Wait for the next frame to simulate processing time
			}
			var frameEndTime = Time.realtimeSinceStartup;
			float averageFPS = TestFrames / (frameEndTime - frameStartTime);
			Debug.Log($"Average FPS: {averageFPS:F2}");

			// Measure accuracy
			if (GroundTruthPoses.Count > 0) {
				float cumulativePositionError = 0f;
				float cumulativeRotationError = 0f;
				for (int i = 0; i < Math.Min(TestFrames, GroundTruthPoses.Count); i++) {
					var detectedPose = algorithm.TrackObject(TestImage);
					var position = GroundTruthPoses[i].position;
					var rotation = GroundTruthPoses[i].rotation;
					cumulativePositionError += Vector3.Distance(position, detectedPose?.position ?? Vector3.zero);
					cumulativeRotationError += Quaternion.Angle(rotation, detectedPose?.rotation ?? Quaternion.identity);

				}
				float averagePositionError = cumulativePositionError / TestFrames;
				float averageRotationError = cumulativeRotationError / TestFrames;
				Debug.Log($"Average Accuracy Error: {averagePositionError:F4}");
			}
		}
	}

}
#endif