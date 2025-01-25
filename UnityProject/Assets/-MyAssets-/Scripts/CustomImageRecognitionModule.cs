#if UNITY_EDITOR
using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Features2dModule;
using OpenCVForUnity.Xfeatures2dModule;
using OpenCVForUnity.Calib3dModule;
using System.Collections.Generic;

public class CustomImageRecognitionModule : MonoBehaviour {

	// Bool field to check if the image recognition module is initialized
	private bool isInitialized = false;

	// Marker data
	private Mat[] markerDescriptors; // Descriptors for each marker
	private Mat[] markerImages; // Grayscale marker images
	private KeyPoint[][] markerKeypoints; // Keypoints of each marker image

	// Feature detector and matcher
	private Feature2D featureDetector;
	private BFMatcher matcher;

	// Supported algorithms
	public enum FeatureAlgorithm { ORB, SIFT, SURF, AKAZE }
	private FeatureAlgorithm currentAlgorithm;

	// Initializes markers with selected algorithm
	public void InitializeMarkers(Texture2D[] markerTextures, FeatureAlgorithm algorithm) {

		// Set the current algorithm
		currentAlgorithm = algorithm;

		// Initialize the feature detector and matcher based on the algorithm
		switch (currentAlgorithm) {
			case FeatureAlgorithm.ORB:
				featureDetector = ORB.create();
				matcher = new BFMatcher(BFMatcher.BRUTEFORCE_HAMMING, crossCheck: true);
				break;

			case FeatureAlgorithm.SIFT:
				featureDetector = SIFT.create();
				matcher = new BFMatcher(BFMatcher.BRUTEFORCE_SL2, crossCheck: true);
				break;

			case FeatureAlgorithm.SURF:
				featureDetector = SURF.create();
				matcher = new BFMatcher(BFMatcher.BRUTEFORCE_SL2, crossCheck: true);
				break;

			case FeatureAlgorithm.AKAZE:
				featureDetector = AKAZE.create();
				matcher = new BFMatcher(BFMatcher.BRUTEFORCE_HAMMING, crossCheck: true);
				break;

			default:
				Debug.LogError("Unsupported feature detection algorithm.");
				return;
		}

		// Prepare marker storage
		int markerCount = markerTextures.Length;
		markerDescriptors = new Mat[markerCount];
		markerImages = new Mat[markerCount];
		markerKeypoints = new KeyPoint[markerCount][];

		// Process each marker texture
		for (int i = 0; i < markerCount; i++) {
			Mat image = new Mat();
			OpenCVForUnity.UnityUtils.Utils.texture2DToMat(markerTextures[i], image);
			Imgproc.cvtColor(image, image, Imgproc.COLOR_RGBA2GRAY);
			markerImages[i] = image;

			MatOfKeyPoint keyPoints = new MatOfKeyPoint();
			featureDetector.detect(image, keyPoints);
			markerKeypoints[i] = keyPoints.toArray();

			markerDescriptors[i] = new Mat();
			featureDetector.compute(image, new MatOfKeyPoint(markerKeypoints[i]), markerDescriptors[i]);
		}

		// Set the initialization flag
		isInitialized = true;

	}

	// Detects markers in the given camera frame
	public Pose? DetectMarker(Texture2D cameraTexture) {
		Mat cameraFrame = new Mat();
		OpenCVForUnity.UnityUtils.Utils.texture2DToMat(cameraTexture, cameraFrame);
		Imgproc.cvtColor(cameraFrame, cameraFrame, Imgproc.COLOR_RGBA2GRAY);

		MatOfKeyPoint keyPoints = new MatOfKeyPoint();
		featureDetector.detect(cameraFrame, keyPoints);
		KeyPoint[] frameKeypoints = keyPoints.toArray();

		Mat frameDescriptors = new Mat();
		featureDetector.compute(cameraFrame, new MatOfKeyPoint(frameKeypoints), frameDescriptors);

		for (int i = 0; i < markerDescriptors.Length; i++) {

			MatOfDMatch matches = new MatOfDMatch();
			matcher.match(markerDescriptors[i], frameDescriptors, matches);

			DMatch[] matchArray = matches.toArray();
			if (matchArray.Length < 4)
				continue;

			System.Array.Sort(matchArray, (a, b) => a.distance.CompareTo(b.distance));
			MatOfDMatch goodMatches = new MatOfDMatch(matchArray);

			Point[] objPoints = new Point[goodMatches.rows()];
			Point[] scenePoints = new Point[goodMatches.rows()];

			for (int j = 0; j < goodMatches.rows(); j++) {
				objPoints[j] = markerKeypoints[i][matchArray[j].queryIdx].pt;
				scenePoints[j] = frameKeypoints[matchArray[j].trainIdx].pt;
			}

			Mat homography = Calib3d.findHomography(new MatOfPoint2f(objPoints), new MatOfPoint2f(scenePoints), Calib3d.RANSAC, 5);
			if (homography.empty())
				continue;

			Pose pose = CalculatePose(homography, markerImages[i].size());
			return pose;
		}

		return null;
	}

	// Calculates the pose from the homography matrix
	private Pose CalculatePose(Mat homography, Size markerSize) {
		
		double[] h = new double[9];
		homography.get(0, 0, h);

		Vector3 position = new Vector3((float) h[2], (float) h[5], (float) h[8]);
		Quaternion rotation = Quaternion.LookRotation(new Vector3((float) h[0], (float) h[3], (float) h[6]));

		float scaleX = (float) (markerSize.width / Mathf.Sqrt((float) (h[0] * h[0] + h[3] * h[3])));
		float scaleY = (float) (markerSize.height / Mathf.Sqrt((float) (h[1] * h[1] + h[4] * h[4])));
		Vector3 scale = new Vector3(scaleX, scaleY, 1);

		return new Pose(position, rotation);
	}

	// Function that returns true if the module is initialized
	public bool IsInitialized() {
		return isInitialized;
	}
	
	// Function used for testing, returns the detected pose based on the input image (if any)
	public Pose GetTrackImagePose(Texture2D image, FeatureAlgorithm featureAlgorithm) {
		// Initialize the markers if not already initialized or if the algorithm has changed
		if (!isInitialized || currentAlgorithm != featureAlgorithm) InitializeMarkers(new Texture2D[] { image }, featureAlgorithm);
		// Detect the marker in the image and calculate the pose
		Pose? pose = DetectMarker(image);
		if (pose == null) return new Pose(Vector3.negativeInfinity, Quaternion.identity);
		return pose.Value;
	}

}

// Dummy class to prevent compilation errors when the patented SURF algorithm is not available in the OpenCV library for the target Android/iOS platform
#if !UNITY_EDITOR || PLATFORM_ANDROID || PLATFORM_IOS
internal class SURF : Feature2D {
	protected internal SURF(System.IntPtr addr) : base(addr) {
		throw new System.NotSupportedException("SURF algorithm is not available in the OpenCV library for the target platform.");
	}

	internal static SURF create() {
		throw new System.NotSupportedException("SURF algorithm is not available in the OpenCV library for the target platform.");
	}

}
#endif

#endif
