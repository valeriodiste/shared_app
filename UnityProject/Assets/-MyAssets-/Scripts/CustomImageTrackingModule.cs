#if UNITY_EDITOR
using UnityEngine;

public class CustomImageTrackingModule : MonoBehaviour {

	// Reference to the ImageTracker class (implements the AR-Foundation library tracking, but also stores information about anchor images)
	private ImageTracker imageTracker;

	// Reference to the image recognition module
	private CustomImageRecognitionModule imageRecognitionModule;

	// Chosen image recognition algorithm
	private CustomImageRecognitionModule.FeatureAlgorithm imageRecognitionAlgorithm = CustomImageRecognitionModule.FeatureAlgorithm.AKAZE;

	// Enum for different tracking modes
	public enum TrackingMode { ImageTracking, SensorTracking, SimpleFusionTracking, KalmanFusionTracking };
	public TrackingMode currentTrackingMode = TrackingMode.KalmanFusionTracking;

	// Current marker image being tracked
	public Texture2D currentMarkerImage;

	// Marker pose and sensor data
	private Pose imagePose;
	private Vector3 sensorPosition;
	private Quaternion sensorOrientation;

	// Kalman Filter variables
	private KalmanFilter positionKalmanFilter;
	private KalmanQuaternionFilter orientationKalmanFilter;

	// Sensor fusion weight (for simple fusion and Kalman fusion)
	private float fusionWeight = 0.5f; 

	void Start() {
		// Initialize the image recognition module (if not already initialized)
		imageRecognitionModule = GetComponent<CustomImageRecognitionModule>();
		if (!imageRecognitionModule.IsInitialized()) {
			Debug.LogError($"Image recognition module not initialized, initializing it to use the ${imageRecognitionAlgorithm} algorithm.");
			Texture2D[] markerTextures = imageTracker.GetAnchorImages();
			imageRecognitionModule.InitializeMarkers(markerTextures, imageRecognitionAlgorithm);
		}
		// Initialize the Kalman filters for position and orientation
		positionKalmanFilter = new KalmanFilter();
		orientationKalmanFilter = new KalmanQuaternionFilter();
	}

	private void Update() {
		// Update the tracking
		Vector3 deviceAcceleration = Input.acceleration;	// Linear acceleration
		Quaternion deviceRotation = Input.gyro.attitude;   // Attitude (i.e. orientation in space)
		UpdateTracking(currentMarkerImage, deviceAcceleration, deviceRotation);
	}

	// Update the tracking
	public void UpdateTracking(Texture2D cameraTexture, Vector3 acceleration, Quaternion rotation) {

		// Detect marker pose (for image-based tracking)
		bool detectMarkerPose =
				currentTrackingMode == TrackingMode.ImageTracking
			|| currentTrackingMode == TrackingMode.SimpleFusionTracking
			|| currentTrackingMode == TrackingMode.KalmanFusionTracking;
		if (detectMarkerPose) {
			imagePose = DetectMarker(cameraTexture);
		}

		// Update sensor data (for sensor-based tracking)
		bool updateSensorData =
				currentTrackingMode == TrackingMode.SensorTracking
			|| currentTrackingMode == TrackingMode.SimpleFusionTracking
			|| currentTrackingMode == TrackingMode.KalmanFusionTracking;
		if (updateSensorData) {
			sensorPosition = GetSensorPosition(acceleration);
			sensorOrientation = rotation;
		}

		// Combine sensor and image data (if needed, using simple linear-interpolation-based fusion or sensor fusion techniques)
		if (currentTrackingMode == TrackingMode.ImageTracking) {
			// Only use image tracking
			UpdateTrackingPose(imagePose);
		} else if (currentTrackingMode == TrackingMode.SensorTracking) {
			// Only use sensor tracking
			Pose newPose = GetPoseFromSensors(sensorPosition, sensorOrientation, imagePose);
			UpdateTrackingPose(newPose);
		} else if (currentTrackingMode == TrackingMode.SimpleFusionTracking) {
			// Simple Fusion (Weighted average of image pose and sensor data)
			Pose fusedPose = SimpleFusion(imagePose, sensorPosition, sensorOrientation);
			UpdateTrackingPose(fusedPose);
		} else if (currentTrackingMode == TrackingMode.KalmanFusionTracking) {
			// Kalman Fusion (Using Kalman filters for position and orientation)
			Pose fusedPose = KalmanFusion(imagePose, sensorPosition, sensorOrientation);
			UpdateTrackingPose(fusedPose);
		}

		// Update the current pose
		imagePose = GetCurrentPose();

	}

	// Function to perform marker detection (simplified)
	private Pose DetectMarker(Texture2D cameraTexture) {
		// Detect markers and return the pose
		Pose? detectedPose = imageRecognitionModule.DetectMarker(cameraTexture);
		return detectedPose ?? new Pose(Vector3.zero, Quaternion.identity);
	}

	// Function to estimate position using sensor data (accelerometer)
	private Vector3 GetSensorPosition(Vector3 acceleration) {
		// Simple sensor-based position estimation (could be expanded with more filtering)
		return acceleration * Time.deltaTime;
	}

	// Function to estimate pose using sensor data (accelerometer and gyroscope) and the previous pose
	private Pose GetPoseFromSensors(Vector3 acceleration, Quaternion rotation, Pose previousPose) {
		// Simple sensor-based pose estimation (could be expanded with more filtering)
		Vector3 position = previousPose.position + acceleration * Time.deltaTime;
		Quaternion orientation = previousPose.rotation * rotation;
		return new Pose(position, orientation);
	}

	// Simple fusion of image pose and sensor data (weighted average)
	private Pose SimpleFusion(Pose imagePose, Vector3 sensorPosition, Quaternion sensorOrientation) {
		Vector3 fusedPosition = Vector3.Lerp(imagePose.position, sensorPosition, fusionWeight);
		Quaternion fusedOrientation = Quaternion.Slerp(imagePose.rotation, sensorOrientation, fusionWeight);
		return new Pose(fusedPosition, fusedOrientation);
	}

	// Kalman fusion of image pose and sensor data
	private Pose KalmanFusion(Pose imagePose, Vector3 sensorPosition, Quaternion sensorOrientation) {

		// Use Kalman filter to refine position
		Vector3 filteredPosition = new Vector3(
			positionKalmanFilter.Update(sensorPosition.x),
			positionKalmanFilter.Update(sensorPosition.y),
			positionKalmanFilter.Update(sensorPosition.z)
		);

		// Use Kalman filter to refine orientation (quaternion)
		Quaternion filteredOrientation = orientationKalmanFilter.Update(sensorOrientation);

		// Combine image pose with Kalman filtered sensor data
		Vector3 fusedPosition = Vector3.Lerp(imagePose.position, filteredPosition, fusionWeight);
		Quaternion fusedOrientation = Quaternion.Slerp(imagePose.rotation, filteredOrientation, fusionWeight);

		return new Pose(fusedPosition, fusedOrientation);
	}

	// Update the transform's position and rotation based on fused pose
	private void UpdateTrackingPose(Pose fusedPose) {
		transform.position = fusedPose.position;
		transform.rotation = fusedPose.rotation;
	}

	// Get the current pose of the object
	public Pose GetCurrentPose() {
		return new Pose(transform.position, transform.rotation);
	}
	
	// Function used for testing, returns the tracked pose based on the input image
	public Pose GetTrackImagePose(Texture2D image, TrackingMode trackingMode) {
		// Update the current tracking mode
		if (currentTrackingMode != trackingMode) currentTrackingMode = trackingMode;
		// Simulate a step of the tracking algorithm
		Update();
		// Return the tracked pose
		return GetCurrentPose();
	}

	// Kalman filter class for position
	public class KalmanFilter {

		private float estimate;
		private float estimateError;
		private float processNoise = 0.1f;
		private float measurementNoise = 1f;
		private float kalmanGain;

		public KalmanFilter() {
			estimate = 0f;
			estimateError = 1f;
		}

		public float Update(float measurement) {
			// Predict the next state
			kalmanGain = estimateError / (estimateError + measurementNoise);
			estimate += kalmanGain * (measurement - estimate);
			estimateError = (1f - kalmanGain) * estimateError + Mathf.Abs(estimate) * processNoise;
			return estimate;
		}
	}

	// Kalman filter class for Quaternion (orientation)
	public class KalmanQuaternionFilter {

		private Quaternion estimate;
		private float processNoise = 0.1f;
		private float measurementNoise = 1f;

		public KalmanQuaternionFilter() {
			estimate = Quaternion.identity;
		}

		public Quaternion Update(Quaternion measurement) {
			// We use a simple approach for quaternion filtering: blending current estimate with new measurement
			estimate = Quaternion.Slerp(estimate, measurement, 0.5f);
			return estimate;
		}
	}
}
#endif
