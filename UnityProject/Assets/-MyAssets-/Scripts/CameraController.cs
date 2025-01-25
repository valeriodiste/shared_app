using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.Video;
using static NativeFilePicker;
using GLTFast;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using UnityEditor;
using System.Text;


public class CameraController : MonoBehaviour {

	private Camera gameCamera;
	private ARCameraManager arCameraManager;
	private UserInputsManager userInputsManager;
	private ImageTracker imageTracker;

	private List<Texture2D> addedImages;

	public Canvas uiCanvas;
	public RectTransform uiMenu;
	public RectTransform uiLogin;
	public RectTransform uiAr;
	public RectTransform uiImage;
	public RectTransform uiList;
	public RectTransform uiEdit;

	public TextMeshProUGUI uiTextDebug01;

	public TextMeshProUGUI uiTextDebug02;

	public TextMeshProUGUI uiPopUpMessageText;

	public Transform imported3DModelsContainer;

	// UI AR view references
	public Button uiARListButton;
	public Button uiARAddButton;
	public Button uiARExitButton;

	// UI List references
	private RawImage displayedImage;
	private UILineRenderer imageAdjuster;
	private Slider imageAdjustSlider;
	public TMP_InputField[] addImageMarkerSizeInputs;
	private RectTransform uiListContent;
	public GameObject ARObjectsPrefab;

	// UI Edit references
	public RectTransform uiEdit_anchorImageContainer;
	public RawImage uiEdit_anchorImage;
	public TMP_Dropdown uiEdit_objectType;
	public TMP_InputField uiEdit_objectString;
	public TMP_InputField[] uiEdit_markerSizeInputs;
	public TMP_InputField[] uiEdit_positionInputs;
	public TMP_InputField[] uiEdit_rotationInputs;
	public TMP_InputField[] uiEdit_scaleInputs;
	public Button uiEdit_UploadButton;
	public Toggle uiEdit_FullScreenToggle;

	private float displayedImageScreenWidthPercentage = 0.85f;

	private Vector2[] imageAdjusterCorners = new Vector2[4];
	private int movingImageAdjusterCornerIndex = -1;   // Corners are numbered clockwise starting from the bottom left corner, with indexed 0 to 4 (-1 if not adjusting corners)
	private int movingImageAdjusterEdgeIndex = -1;     // Edges are numbered clockwise starting from the bottom edge, with indexed 0 to 4 (-1 if not adjusting edges)
	private Vector2[] previousImageAdjusterCorners = new Vector2[4];
	private Vector2 startTouchPosition;

	public GameObject displayARObjectPrefab;
	private List<ARDisplayObjectScript> displayedARObjects;

	// Index of the object in the ARTrackedImageInfos list that is currently being edited
	private int currentEditIndex = -1;
	
	// Path of the picked file
	private string uploadedFilePath = null;

	//public GltfAsset gltfImporterComponent;
	
	// Used to check if we should disable the currently shown popup message after some seconds or not
	private int popupMessagesCounter;

	// index of ghe current AR experience in the list of AR experiences
	private int currentARExperienceIndex = 0;
	private bool editingCurrentARExperience;

	// Main menu references and variables
	public RectTransform mainMenuContent;
	public GameObject displayARExperiencePrefab;
	private List<ARExperience> currentCreatedARExperiences;
	private List<ARExperience> currentDownloadedARExperiences;
	private List<ARDisplayExperienceScript> mainMenuDisplayedARExperiences;
	public TMP_InputField uiDownloadExperienceCode;
	public RectTransform mainMenuHeader_CreatedExperiences;
	public RectTransform mainMenuHeader_DownloadedExperiences;
	public Button mainMenuToggleButton_Created;
	public Button mainMenuToggleButton_Downloaded;
	public Button mainMenuProfile_LogInOutButton;
	public Button mainMenuProfile_ProfileButton;
	public TextMeshProUGUI mainMenuProfile_ProfileButtonText;
	public TextMeshProUGUI mainMenuProfile_LogInOutButtonText;
	public TextMeshProUGUI mainMenuProfile_UsernameText;
	// "Type" of experiences beng shown in the main menu experiences list (true if showing created experinces, false if showing downloaded experiences)
	private bool showingCreadetExperiences = true;

	// Login screen
	public TMP_InputField loginUsernameOrEmailInputField;
	public Button getOTPCodeButton;
	public TMP_InputField otpInputField;
	public Button verifyOTPButton;

	// User profile/login variables
	private string username;
	private string userEmail;
	private string userToken;
	private bool isTryingToLogin;	// True when waiting to verify the OTP code, or when already logged in
	private bool isLoggedIn;

	// Loading screen
	//private bool showingLoadingScreen;
	public RectTransform loadingScreen;
	// Camera state before entering loading screen
	private CameraState cameraStateBeforeLoading;

	public enum CameraState {
		MainMenu,
		LoginScreen,
		AR,
		EditImage,
		ListAnchors,
		EditARObject,
		Loading
		// Add other states
	}

	private CameraState currentCameraState;

	private void Awake() {
		// Object references
		gameCamera = GameObject.FindGameObjectsWithTag("MainCamera")[0].GetComponent<Camera>();   // Find by tag since Unity might add other (non "main") cameras for the XR Environment Simulation
		arCameraManager = gameCamera.gameObject.GetComponent<ARCameraManager>();
		imageTracker = FindObjectOfType<ImageTracker>();
		userInputsManager = FindObjectOfType<UserInputsManager>();
		addedImages = new List<Texture2D>();
		displayedImage = uiImage.Find("DisplayedImage").GetComponent<RawImage>();
		imageAdjuster = uiImage.GetComponentInChildren<UILineRenderer>();
		imageAdjustSlider = uiImage.GetComponentInChildren<Slider>();
		addImageMarkerSizeInputs = uiImage.GetComponentsInChildren<TMP_InputField>();
		uiListContent = uiList.GetComponentInChildren<ScrollRect>().content;
		//Debug.Log("Game Camera: " + gameCamera, gameCamera);
		//Debug.Log("AR Camera Manager: " + arCameraManager, arCameraManager);
		// Initialization (start from main menu)
		DisplayUI_MainMenu();
		ToggleLoadingScreen(false, true);
	}

	private async void Update() {
		ResizeImageAdjuster();
		// For debug, on press of the "T" key, add the last trackable image to the trackables
		//if (InputSystem.devices[0].name == "Keyboard" && Keyboard.current.tKey.wasPressedThisFrame) {
		//	// Add the last trackable image to the trackables
		//	ImageTracker imageTracker = FindObjectOfType<ImageTracker>();
		//	var mutableLibrary = imageTracker.trackedImagesManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;
		//	imageTracker.AddARTrackedImageAndObjectToLibrary(imageTracker.ARImagesInfos[^1], mutableLibrary);
		//}
#if UNITY_EDITOR
		// For debug, if in main menu, on press of key "S", print the JSON representation of each experience
		if (currentCameraState == CameraState.MainMenu && Keyboard.current.sKey.wasPressedThisFrame) {
			foreach (ARExperience experience in currentCreatedARExperiences) {
				string json = await experience.ToJson();
				Debug.Log("Experience: " + experience.experienceName + ", JSON:\n" + json);
				Debug.Log("> Experience AR images lenght: " + experience.ARObjectsInfos.Count);
				// Save the JSON file in the resources folder in editor
				string path = "Assets/Resources/cached_experience.json";
				File.WriteAllText(path, json);
			}
		}
		// For debug, if in main menu, on press of key "L", load the JSON representation of the first experience and create a new experience from the JSON representation 
		if (currentCameraState == CameraState.MainMenu && Keyboard.current.lKey.wasPressedThisFrame) {
			if (currentCreatedARExperiences != null && currentCreatedARExperiences.Count > 0) {
				string firstExperienceJSON = await currentCreatedARExperiences[0].ToJson();
				Debug.Log("Adding new experience, JSON: " + firstExperienceJSON);
				ARExperience newExperience = await ARExperience.FromJson(firstExperienceJSON);
				AddARExperience(newExperience);
				Debug.Log("> Added New Experience: " + newExperience.experienceName + ", JSON: " + await newExperience.ToJson());
				Debug.Log("> Experience AR images lenght: " + newExperience.ARObjectsInfos.Count);
			}
		}
#endif
		UpdateUiTextDebug();
	}

	// Function to take a screenshot of the camera view and return it as a Texture2D
	public void TakeScreenshot(Action<Texture2D> OnComplete) {
		// Function to get the final texture
		Texture2D GetTexture(XRCpuImage image) {
			// Create a Texture2D to store the image data
			Texture2D texture = new Texture2D(image.width, image.height, TextureFormat.RGBA32, false);
			// Create a buffer to store the raw image data
			byte[] rawImageBytes = new byte[image.GetPlane(0).data.Length];
			// Copy the raw image data to the buffer
			image.GetPlane(0).data.CopyTo(rawImageBytes);
			// Load the raw image data into the Texture2D
			texture.LoadRawTextureData(rawImageBytes);
			texture.Apply();
			// Dispose of the acquired image
			image.Dispose();
			// Return the Texture2D
			return texture;
		}
		// Function to rotate or mirror the image if needed
		Texture2D CropAndRotateOrMirrorImage(Texture2D image) {
			// Create an image
			Texture2D toRet = new Texture2D(image.height, image.width);
#if !UNITY_EDITOR
			// Rotate the image 90 degrees clockwise and mirror it on the X and Y axes
			for (int x = 0; x < image.width; x++) {
				for (int y = 0; y < image.height; y++) {
					int newX = image.height - y - 1;
					int newY = image.width - x - 1;
					toRet.SetPixel(newX, newY, image.GetPixel(x, y));
				}
			}
			toRet.Apply();
#else
			// Do nothing in the editor
			toRet = image;
#endif
			// Return the rotated or mirrored image
			return toRet;
		}
		// Get the camera's current acquired image (without AR overlays)
		arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image);
		// Check if we can use GetPlane() to get the image data
		if (image.format != XRCpuImage.Format.RGBA32) {
			// Convert the image to RGBA32 format
			XRCpuImage.ConversionParams conversionParams = new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32);
			Action<XRCpuImage.AsyncConversionStatus, XRCpuImage.ConversionParams, NativeArray<byte>> OnConversionComplete = (status, conversionParams, data) => {
				// Check if the conversion was successful
				if (status != XRCpuImage.AsyncConversionStatus.Ready) {
					Debug.LogError("Failed to convert the image to RGBA32 format.");
					return;
				}
				// Convert the bytes to a Texture2D
				Texture2D texture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, conversionParams.outputFormat, false);
				texture.LoadRawTextureData(data);
				texture.Apply();
				// Rotate or mirror the image
				texture = CropAndRotateOrMirrorImage(texture);
				// Criop the image to 
				// Call the OnComplete callback with the texture
				OnComplete(texture);
			};
			image.ConvertAsync(conversionParams, OnConversionComplete);
		} else {
			// Get the image as a Texture2D
			Texture2D texture = GetTexture(image);
			// Rotate or mirror the image
			texture = CropAndRotateOrMirrorImage(texture);
			// Call the OnComplete callback with the texture
			OnComplete(texture);
		}

	}

	public void DisplayUI_MainMenu() {
		Debug.Log("Displaying the main menu...");
		SetCameraState(CameraState.MainMenu);
		showingCreadetExperiences = true;
		RefreshMainMenu();
	}

	public void DisplayUI_LoginScreen() {
		Debug.Log("Displaying the login screen...");
		SetCameraState(CameraState.LoginScreen);
		RefreshLoginScreen();
	}

	public void DisplayUI_ARView() {
		Debug.Log("Displaying the AR view...");
		SetCameraState(CameraState.AR);
		// Set the AR view
		RefreshUIARView();
		// Set the current experience
		ARExperience currentExperience = showingCreadetExperiences ? currentCreatedARExperiences[currentARExperienceIndex] : currentDownloadedARExperiences[currentARExperienceIndex];
		InitializeARExperience(currentExperience, editingCurrentARExperience && showingCreadetExperiences);
	}

	public void DisplayUI_AddImage() {
		Debug.Log("Trying to add the screenshot image...");
		TakeScreenshot((Texture2D newImage) => {
			// Acquire the image and add it to the list of images
			addedImages.Add(newImage);
			// Display the image on the screen
			DisplayImage(newImage);
		});
	}

	public void DisplayUI_AnchorsList() {
		Debug.Log("Showing the list of AR Anchors and Objects");
		RefreshARAnchorsDisplayedList();
		SetCameraState(CameraState.ListAnchors);
	}

	public void DisplayUI_EditARObject(int editIndex) {
		Debug.Log("Editing the AR Anchor at index " + editIndex);
		currentEditIndex = editIndex;
		// Refresh the displayed AR Anchor in the edit menu
		RefreshEditAROjectView();
		// Change the camera state to display the UI
		SetCameraState(CameraState.EditARObject);
	}

	public void StartARExperience(int experienceindex) {
		currentARExperienceIndex = experienceindex;
		DisplayUI_ARView();
	}

	private void DisplayImage(Texture2D image) {
		// Set the displayed image to the new image
		displayedImage.texture = image;
		// Set the size of the image and the image adjuster points
		float aspectRatio = (float) image.width / image.height;
		imageAdjustSlider.value = aspectRatio;
		SetDisplayedImageAspectRatio(aspectRatio);
		// Set the displayed image to be active
		SetCameraState(CameraState.EditImage);
	}

	public void SetCameraState(CameraState state) {
		currentCameraState = state;
		RefreshCameraState();
	}
	private void RefreshCameraState() {
		loadingScreen.gameObject.SetActive(currentCameraState == CameraState.Loading);
		if (currentCameraState != CameraState.Loading) {
			uiMenu.gameObject.SetActive(currentCameraState == CameraState.MainMenu);
			uiLogin.gameObject.SetActive(currentCameraState == CameraState.LoginScreen);
			uiAr.gameObject.SetActive(currentCameraState == CameraState.AR);
			uiImage.gameObject.SetActive(currentCameraState == CameraState.EditImage);
			uiList.gameObject.SetActive(currentCameraState == CameraState.ListAnchors);
			uiEdit.gameObject.SetActive(currentCameraState == CameraState.EditARObject);
		}
	}

	private void ResizeImageAdjuster() {
		// When the camera is in "EditImage" state, the user can crop the image by dragging the corners of the image adjuster
		if (currentCameraState == CameraState.EditImage) {
			// Get the touch infos
			bool touching = userInputsManager.GetTouchInfos(out Vector2 touchPosition, out Vector2 touchUp, out Vector2 touchDown, out int touchPhase);
			if (touching) {
				// Get the touch position relative to the image adjuster rendered lines
				Vector2 relativeTouchPosition = imageAdjuster.GetRelativePosition(touchPosition);
				// Check if the user touched a corner of the image adjuster
				if (touchPhase == 0) {
					// Check if the touch position is close to a corner of the image adjuster
					imageAdjusterCorners = new List<Vector2>(imageAdjuster.points).ToArray();
					// Set the start touch position
					startTouchPosition = relativeTouchPosition;
					//Debug.Log("Touch position: " + relativeTouchPosition);
					//Debug.Log("Image adjuster corners: " + imageAdjusterCorners[0] + ", " + imageAdjusterCorners[1] + ", " + imageAdjusterCorners[2] + ", " + imageAdjusterCorners[3]);
					movingImageAdjusterCornerIndex = -1;
					movingImageAdjusterEdgeIndex = -1;
					if (imageAdjuster.IsCornerPosition(relativeTouchPosition, out int cornerIndex)) {
						movingImageAdjusterCornerIndex = cornerIndex;
						previousImageAdjusterCorners = new List<Vector2>(imageAdjuster.points).ToArray();
					} else if (imageAdjuster.IsEdgePosition(relativeTouchPosition, out int edgeIndex)) {
						movingImageAdjusterEdgeIndex = edgeIndex;
						previousImageAdjusterCorners = new List<Vector2>(imageAdjuster.points).ToArray();
					}
				}
				if (touchPhase == 1) {
					// Check if the user is dragging a corner of the image adjuster
					if (movingImageAdjusterCornerIndex != -1) {
						// Move the corner to the new position
						imageAdjusterCorners[movingImageAdjusterCornerIndex] = relativeTouchPosition;
						// Update the image adjuster
						imageAdjuster.SetPoints(new Vector2[] {
							imageAdjusterCorners[0],
							imageAdjusterCorners[1],
							imageAdjusterCorners[2],
							imageAdjusterCorners[3]
						}, true);
						//Debug.Log("Moving corner: " + movingImageAdjusterCornerIndex);
					} else if (movingImageAdjusterEdgeIndex != -1) {
						// Move the edge to the new position
						Vector2 positionDelta = relativeTouchPosition - startTouchPosition;
						bool onlyAllowOneDirectionalMovement = true;
						if (onlyAllowOneDirectionalMovement) {
							if (Math.Abs(positionDelta.x) > Math.Abs(positionDelta.y)) {
								positionDelta.y = 0;
							} else {
								positionDelta.x = 0;
							}
						}
						imageAdjusterCorners[movingImageAdjusterEdgeIndex] = previousImageAdjusterCorners[movingImageAdjusterEdgeIndex] + positionDelta;
						imageAdjusterCorners[(movingImageAdjusterEdgeIndex + 1) % 4] = previousImageAdjusterCorners[(movingImageAdjusterEdgeIndex + 1) % 4] + positionDelta;
						// Update the image adjuster
						imageAdjuster.SetPoints(new Vector2[] {
							imageAdjusterCorners[0],
							imageAdjusterCorners[1],
							imageAdjusterCorners[2],
							imageAdjusterCorners[3]
						}, true);
						//Debug.Log("Moving edge: " + movingImageAdjusterEdgeIndex);
					}
				}
				if (touchPhase == 2 || touchPhase == -1) {
					// Stop dragging the corner
					movingImageAdjusterCornerIndex = -1;
					movingImageAdjusterEdgeIndex = -1;
					//Debug.Log("Stopped moving corner.");
				}
			}
		}
	}

	public void SetDisplayedImageAspectRatio(float aspectRatio) {
		// Set the size of the image to always occupy a certain percentage of the canvas
		float width = uiCanvas.pixelRect.width * displayedImageScreenWidthPercentage / uiCanvas.scaleFactor;
		float height = width / aspectRatio;
		displayedImage.rectTransform.sizeDelta = new Vector2(width, height);
		// Get the corners of the image adjuster
		Vector2[] corners = new Vector2[] {
			new Vector2(-width / 2, -height / 2),
			new Vector2(width / 2, -height / 2),
			new Vector2(width / 2, height / 2),
			new Vector2(-width / 2, height / 2)
		};
		// Set the image adjuster points
		imageAdjuster.SetPoints(corners, true);
	}

	public void SliderChangeFunction() {
		SetDisplayedImageAspectRatio(imageAdjustSlider.value);
	}

	public void CropImage() {
		// Take the 4 positions of the image adjuster and remap the image to the rectangle formed by these points
		Vector2[] corners = imageAdjuster.points;
		// Get the displayed image
		Texture2D displayedTexture = displayedImage.texture as Texture2D;
		// Get the displayed image size
		int width = displayedTexture.width;
		int height = displayedTexture.height;
		// Convert the corners to coordinates in the displayed image (coordinates in [0, width] x [0, height])
		Vector2[] newCorners = new Vector2[4];
		for (int i = 0; i < corners.Length; i++) {
			newCorners[i] = new Vector2(
				(corners[i].x + displayedImage.rectTransform.rect.width / 2) / displayedImage.rectTransform.rect.width * width,
				(corners[i].y + displayedImage.rectTransform.rect.height / 2) / displayedImage.rectTransform.rect.height * height
			);
		}
		// Warp the texture to the new rectangle
		Texture2D newTexture = WarpTexture(displayedTexture, newCorners);
		/*
		// Get the new width and height of the image
		int newWidthTop = Mathf.FloorToInt(Vector2.Distance(newCorners[0], newCorners[1]));
		int newWidthBottom = Mathf.FloorToInt(Vector2.Distance(newCorners[3], newCorners[2]));
		int newHeightLeft = Mathf.FloorToInt(Vector2.Distance(newCorners[0], newCorners[3]));
		int newHeightRight = Mathf.FloorToInt(Vector2.Distance(newCorners[1], newCorners[2]));
		int newWidth = Mathf.Max(newWidthTop, newWidthBottom);
		int newHeight = Mathf.Max(newHeightLeft, newHeightRight);
		// Warp the texture to the new rectangle
		Texture2D newTexture = WarpTexture(displayedTexture, newCorners, newWidth, newHeight);
		*/
		// Display the new image
		DisplayImage(newTexture);
	}

	// Resize a texture
	public static Texture2D ResizeTexture(Texture2D texture, int newWidth, int newHeight) {
		Texture2D newTexture = new Texture2D(newWidth, newHeight);
		for (int y = 0; y < newHeight; y++) {
			for (int x = 0; x < newWidth; x++) {
				float u = (float) x / newWidth * texture.width;
				float v = (float) y / newHeight * texture.height;
				Color color = BilinearSample(texture, u, v);
				newTexture.SetPixel(x, y, color);
			}
		}
		newTexture.Apply();
		return newTexture;
	}

	// Texture bilinear sampling
	public static Color BilinearSample(Texture2D texture, float u, float v) {
		if (u < 0 || v < 0 || u >= texture.width || v >= texture.height)
			return Color.clear;
		int x = Mathf.FloorToInt(u);
		int y = Mathf.FloorToInt(v);
		float dx = u - x;
		float dy = v - y;
		Color c00 = texture.GetPixel(x, y);
		Color c10 = texture.GetPixel(x + 1, y);
		Color c01 = texture.GetPixel(x, y + 1);
		Color c11 = texture.GetPixel(x + 1, y + 1);
		return Color.Lerp(
			 Color.Lerp(c00, c10, dx),
			 Color.Lerp(c01, c11, dx),
			 dy
		);
	}

	public static Texture2D WarpTexture(Texture2D source, Vector2[] destCorners, int newWidth = -1, int newHeight = -1) {
		// Sub-function: Compute Homography Matrix
		float[,] ComputeHomographyMatrix(Vector2[] src, Vector2[] dst) {
			// Solve the homography matrix using the linear system A * h = b
			float[,] A = new float[8, 8];
			float[] b = new float[8];
			for (int i = 0; i < 4; i++) {
				float x = src[i].x, y = src[i].y;
				float u = dst[i].x, v = dst[i].y;
				A[2 * i, 0] = x;
				A[2 * i, 1] = y;
				A[2 * i, 2] = 1;
				A[2 * i, 6] = -u * x;
				A[2 * i, 7] = -u * y;
				b[2 * i] = u;
				A[2 * i + 1, 3] = x;
				A[2 * i + 1, 4] = y;
				A[2 * i + 1, 5] = 1;
				A[2 * i + 1, 6] = -v * x;
				A[2 * i + 1, 7] = -v * y;
				b[2 * i + 1] = v;
			}
			float[] h = SolveLinearSystem(A, b);
			float[,] H = new float[3, 3] {
					 { h[0], h[1], h[2] },
					 { h[3], h[4], h[5] },
					 { h[6], h[7], 1    }
			};
			return H;
		}
		// Sub-function: Solve a linear system (A * x = b) using Gaussian elimination
		float[] SolveLinearSystem(float[,] A, float[] b) {
			int n = b.Length;
			for (int i = 0; i < n; i++) {
				// Find the pivot row
				int pivot = i;
				for (int j = i + 1; j < n; j++)
					if (Mathf.Abs(A[j, i]) > Mathf.Abs(A[pivot, i]))
						pivot = j;
				// Swap rows
				for (int j = 0; j < n; j++) {
					float temp = A[i, j];
					A[i, j] = A[pivot, j];
					A[pivot, j] = temp;
				}
				float tempB = b[i];
				b[i] = b[pivot];
				b[pivot] = tempB;
				// Eliminate below the pivot
				for (int j = i + 1; j < n; j++) {
					float factor = A[j, i] / A[i, i];
					for (int k = i; k < n; k++)
						A[j, k] -= factor * A[i, k];
					b[j] -= factor * b[i];
				}
			}
			// Back substitution
			float[] x = new float[n];
			for (int i = n - 1; i >= 0; i--) {
				x[i] = b[i];
				for (int j = i + 1; j < n; j++)
					x[i] -= A[i, j] * x[j];
				x[i] /= A[i, i];
			}
			return x;
		}
		// Sub-function: Transform a point using a homography matrix
		Vector3 TransformPoint(float[,] H, Vector3 point) {
			float x = H[0, 0] * point.x + H[0, 1] * point.y + H[0, 2] * point.z;
			float y = H[1, 0] * point.x + H[1, 1] * point.y + H[1, 2] * point.z;
			float z = H[2, 0] * point.x + H[2, 1] * point.y + H[2, 2] * point.z;
			return new Vector3(x, y, z);
		}

		// Ensure the destination corners array has exactly 4 points
		if (destCorners.Length != 4)
			throw new System.ArgumentException("destCorners must have exactly 4 points");
		// Define the source points (corners of the original texture)
		Vector2[] sourceCorners = new Vector2[4] {
				new Vector2(0, 0), // Top-left
				new Vector2(source.width - 1, 0), // Top-right
				new Vector2(source.width - 1, source.height - 1), // Bottom-right
				new Vector2(0, source.height - 1) // Bottom-left
		};
		// Compute the homography matrix
		float[,] homography = ComputeHomographyMatrix(sourceCorners, destCorners);
		// Create a new texture for the warped result
		int width = source.width;
		int height = source.height;
		Texture2D warpedTexture = new Texture2D(width, height);
		Color[] pixels = new Color[width * height];
		// Warp each pixel in the new texture
		for (int y = 0; y < height; y++) {
			for (int x = 0; x < width; x++) {
				// Map the pixel position (x, y) in the new texture to the source texture
				Vector3 sourcePos = TransformPoint(homography, new Vector3(x, y, 1));
				// Normalize homogeneous coordinates
				float u = sourcePos.x / sourcePos.z;
				float v = sourcePos.y / sourcePos.z;
				// Sample color from the source texture using bilinear interpolation
				Color color = BilinearSample(source, u, v);
				pixels[y * width + x] = color;
			}
		}
		// Apply the warped pixels to the new texture
		warpedTexture.SetPixels(pixels);
		warpedTexture.Apply();
		// Resize the new texture to the new width and height
		if (newWidth == -1)
			newWidth = warpedTexture.width;
		if (newHeight == -1)
			newHeight = warpedTexture.height;
		if (newWidth != warpedTexture.width || newHeight != warpedTexture.height) {
			warpedTexture = ResizeTexture(warpedTexture, newWidth, newHeight);
		}
		return warpedTexture;
	}

	// NOTE: include the other functions with the same name but different parameters for adding other things other than an object prefab as the augmented object for the image anchor
	public int AddARAnchor(string name, Texture2D image, GameObject prefab) {
		ARTrackedImageInfos infos = new ARTrackedImageInfos(name, image, prefab, ARTrackedImageInfos.ObjectType.Type_Text);
		//XRReferenceImageLibrary library = imageTracker.trackedImagesManager.referenceLibrary as XRReferenceImageLibrary;
		//var mutableLibrary = imageTracker.trackedImagesManager.CreateRuntimeLibrary(library) as MutableRuntimeReferenceImageLibrary;
		var mutableLibrary = imageTracker.trackedImagesManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;
		int librarySize = mutableLibrary.count;
		imageTracker.AddARTrackedImageAndObjectToLibrary(infos, mutableLibrary);
		// Return the new number of anchors
		return librarySize + 1;
	}

	public void RemoveARAnchor(int index) {
		// Remove the AR Anchor from the list of AR Anchors
		imageTracker.RemoveARTrackedImage(index, () => {
			// Refresh the list of AR Anchors on complete
			RefreshARAnchorsDisplayedList();
		});
	}

	// NOTE: for now, this function is called by the "ADD" button to add the currently displayed image and a random GameObject to the tracked images AR library
	public void AddActiveImageToAnchors() {
		// Resize the image to the actually displayed image size
		float currentAspetRatio = imageAdjustSlider.value;
		int newWidth = displayedImage.texture.width;
		int newHeight = Mathf.FloorToInt(newWidth / currentAspetRatio);
		Texture2D image = ResizeTexture(displayedImage.texture as Texture2D, newWidth, newHeight);
		// Get the name if the anchor object (next avaiable name)
		int imageNumber = imageTracker.trackedImagesManager.referenceLibrary.count + 1;
		string name = "Object" + imageNumber.ToString().PadLeft(3, '0');
		// Add a new anchor image with the default prefab object
		GameObject ARObject = ARObjectsPrefab;
		int numAnchors = AddARAnchor(name, image, ARObject);
		// Whether to display AR anchors immediately after adding a new anchor
		bool displayARAnchorsList = true;
		// Show the newly added anchor in edit mode or show the AR view
		if (displayARAnchorsList) {
			// Display the list of AR Anchors and Objects
			// NOTE: may not display correctly if the added image takes too much time to be added to the library (e.g. large images)
			int anchorIndex = numAnchors - 1;
			DisplayUI_EditARObject(anchorIndex);
		} else {
			// Display the AR view
			DisplayUI_ARView();
		}
	}

	private void RefreshARAnchorsDisplayedList() {
		// Get the list of AR Anchors and Objects
		List<(Texture2D, GameObject)> ARAnchors = new List<(Texture2D, GameObject)>();
		// Get the list of AR Anchors and Objects from the ARTrackedImageManager
		var library = imageTracker.trackedImagesManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;
		Dictionary<string, GameObject> ARObjects = imageTracker.GetARObjects();
		for (int i = 0; i < library.count; i++) {
			var referenceImage = library[i];
			if (!ARObjects.ContainsKey(referenceImage.name)) {
				Debug.LogWarning("AR Object not found for image: " + referenceImage.name);
				continue;
			}
			Texture2D image = referenceImage.texture;
			GameObject ARObject = ARObjects[referenceImage.name];
			ARAnchors.Add((image, ARObject));
		}
		// Display the list of AR Anchors and Objects
		List<ARTrackedImageInfos> ARObjectsInfosList = new List<ARTrackedImageInfos>(imageTracker.ARImagesInfos);
		if (displayedARObjects == null) {
			displayedARObjects = new List<ARDisplayObjectScript>();
			displayedARObjects.AddRange(uiListContent.GetComponentsInChildren<ARDisplayObjectScript>());
		}
		for (int i = 0; i < ARObjectsInfosList.Count; i++) {
			if (i >= displayedARObjects.Count) {
				// Create a new AR Display Object
				GameObject newARObject = Instantiate(displayARObjectPrefab, uiListContent);
				ARDisplayObjectScript newARDisplayObject = newARObject.GetComponent<ARDisplayObjectScript>();
				displayedARObjects.Add(newARDisplayObject);
			}
			// Set the object values
			Debug.Log("Setting AR Display Object: " + i);
			displayedARObjects[i].gameObject.SetActive(true);
			displayedARObjects[i].SetValues(this, i, ARObjectsInfosList[i].name, ARObjectsInfosList[i].image);
		}
		for (int i = ARObjectsInfosList.Count; i < displayedARObjects.Count; i++) {
			Debug.Log("Disabling AR Display Object: " + i);
			displayedARObjects[i].gameObject.SetActive(false);
		}
		float singleObjectHeight = displayARObjectPrefab.GetComponent<RectTransform>().rect.height + 3f;
		float padding = 9f;
		uiListContent.sizeDelta = new Vector2(uiListContent.sizeDelta.x, ARObjectsInfosList.Count * singleObjectHeight + padding);
	}

	private void RefreshEditAROjectView() {
		// Get the infos of the current object being edited in the AR Object edit menu
		if (currentEditIndex < 0 || currentEditIndex >= imageTracker.ARImagesInfos.Length) {
			Debug.LogError("Invalid index for the AR Object edit menu: " + currentEditIndex);
			return;
		}
		ARTrackedImageInfos infos = imageTracker.ARImagesInfos[currentEditIndex];
		// Reset the upload file path
		uploadedFilePath = null;
		// Set the values of the UI elements
		float anchorImageWidth = uiEdit_anchorImageContainer.rect.width;
		float anchorImageHeight = uiEdit_anchorImageContainer.rect.height;
		float normalAspectRatio = anchorImageWidth / (float) anchorImageHeight;
		float imageAspectRatio = infos.image.width / (float) infos.image.height;
		//Debug.Log("Displaying AR edit image | Width: " + infos.image.width + ", Height: " + infos.image.height);
		//Debug.Log("Normal Aspect Ratio: " + normalAspectRatio);
		//Debug.Log("Image Aspect Ratio: " + imageAspectRatio);
		if (normalAspectRatio < imageAspectRatio) {
			anchorImageHeight = anchorImageWidth / imageAspectRatio;
		} else {
			anchorImageWidth = anchorImageHeight * imageAspectRatio;
		}
		//Debug.Log("New Anchor Image Size | Width: " + anchorImageWidth + ", Height: " + anchorImageHeight);
		uiEdit_anchorImage.texture = infos.image;
		uiEdit_anchorImage.rectTransform.sizeDelta = new Vector2(anchorImageWidth, anchorImageHeight);
		uiEdit_objectType.value = (int) infos.type;
		uiEdit_FullScreenToggle.isOn = infos.fullScreen;
		uiEdit_positionInputs[0].text = infos.objectStartPosition.x.ToString();
		uiEdit_positionInputs[1].text = infos.objectStartPosition.y.ToString();
		uiEdit_positionInputs[2].text = infos.objectStartPosition.z.ToString();
		uiEdit_rotationInputs[0].text = infos.objectStartRotation.x.ToString();
		uiEdit_rotationInputs[1].text = infos.objectStartRotation.y.ToString();
		uiEdit_rotationInputs[2].text = infos.objectStartRotation.z.ToString();
		uiEdit_scaleInputs[0].text = infos.objectStartScale.x.ToString();
		uiEdit_scaleInputs[1].text = infos.objectStartScale.y.ToString();
		uiEdit_scaleInputs[2].text = infos.objectStartScale.z.ToString();
		uiEdit_objectString.text = "";
		switch (infos.type) {
			case ARTrackedImageInfos.ObjectType.Type_Text:
				uiEdit_objectString.text = infos.GetObject_Text();
				break;
			case ARTrackedImageInfos.ObjectType.Type_Image:
				infos.GetObject_Image(out string imageURL);
				if (imageURL != null && imageURL.Length > 0) uiEdit_objectString.text = imageURL;
				break;
			case ARTrackedImageInfos.ObjectType.Type_Video:
				infos.GetObject_Video(out string videoURL);
				if (videoURL != null && videoURL.Length > 0) uiEdit_objectString.text = videoURL;
				break;
			case ARTrackedImageInfos.ObjectType.Type_3D:
				// Do nothing
				break;
			case ARTrackedImageInfos.ObjectType.Unset:
				Debug.Log("WARNING: Invalid object type: " + infos.type);
				break;
			default:
				Debug.LogError("Unknown object type: " + infos.type);
				break;
		}
		// Update the state of the "UPLOAD" button
		UpdateUploadButtonState();
	}

	// Updates the state of the dropdown for the "UPLOAD" button of the AR objects editor UI screen
	// NOTE: this is also set as a callback function of the dropdown button
	public void UpdateUploadButtonState() {
		// Get the current object type
		ARTrackedImageInfos.ObjectType type = (ARTrackedImageInfos.ObjectType) uiEdit_objectType.value;
		// Set the state of the "UPLOAD" button
		uiEdit_UploadButton.interactable =
			type != ARTrackedImageInfos.ObjectType.Unset &&
			type != ARTrackedImageInfos.ObjectType.Type_Text;
	}

	// Toggles the "useFullScreen" option of the current object being edited in the AR Object edit menu
	public void ToggleCurrentEditARImageFullScreen() {
		// Get the previous infos
		ARTrackedImageInfos infos = imageTracker.ARImagesInfos[currentEditIndex];
		// Toggle the full screen option
		infos.fullScreen = uiEdit_FullScreenToggle.isOn;
		// Set the object specific infos
		imageTracker.ARImagesInfos[currentEditIndex] = infos;
		// Update the actual AR objects on the scene
		imageTracker.UpdateARObjectSpecificInfos(currentEditIndex);
		// Show a debug with the new information
		Debug.Log("Toggled the full screen option for the AR Object.");
	}

	// Saves the infos of the current object being edited in the AR Object edit menu
	public void SaveCurrentEditARImageInfos() {
		// Get the previous infos
		ARTrackedImageInfos infos = imageTracker.ARImagesInfos[currentEditIndex];
		// Auxiliary function to parse a string to a float
		float ParseFloat(string str, float defaultValue = 0f) {
			float value = defaultValue;
			bool success = float.TryParse(str, out value);
			if (!success) {
				return defaultValue;
			} else {
				return value;
			}
		}
		// Get the marker size
		Vector2 markerSize = new Vector2(
			ParseFloat(uiEdit_markerSizeInputs[0].text),
			ParseFloat(uiEdit_markerSizeInputs[1].text)
		);
		// Get the current infos variables
		ARTrackedImageInfos.ObjectType type = (ARTrackedImageInfos.ObjectType) uiEdit_objectType.value;
		string objectString = uiEdit_objectString.text;
		Vector3 position = new Vector3(
			ParseFloat(uiEdit_positionInputs[0].text),
			ParseFloat(uiEdit_positionInputs[1].text),
			ParseFloat(uiEdit_positionInputs[2].text)
		);
		Vector3 rotation = new Vector3(
			ParseFloat(uiEdit_rotationInputs[0].text),
			ParseFloat(uiEdit_rotationInputs[1].text),
			ParseFloat(uiEdit_rotationInputs[2].text)
		);
		Vector3 scale = new Vector3(
			ParseFloat(uiEdit_scaleInputs[0].text, 1f),
			ParseFloat(uiEdit_scaleInputs[1].text, 1f),
			ParseFloat(uiEdit_scaleInputs[2].text, 1f)
		);
		// Set the new type
		infos.type = type;
		infos.markerSize = markerSize;
		// Set transform values (position, rotation, scale)
		infos.objectStartPosition = position;
		infos.objectStartRotation = rotation;
		infos.objectStartScale = scale;
		// Show a debug with the new information
		Debug.Log($"Saved the new AR Object infos:\nP: {position}\nR: {rotation}\nS: {scale}\nType: {type}\nString: {objectString}");
		// Set the object specific infos
		imageTracker.ARImagesInfos[currentEditIndex] = infos;
		// Auxiliary function to update the actual AR objects on the scene
		void UpdateARObjectSpecificInfos() {
			imageTracker.UpdateARObjectSpecificInfos(currentEditIndex);
		}
		// Set other variables
		bool userUploadedFile = uploadedFilePath != null && uploadedFilePath.Length > 0;
		switch (type) {
			case ARTrackedImageInfos.ObjectType.Type_Text:
				// Set the infos text
				infos.SetObject_Text(objectString);
				UpdateARObjectSpecificInfos();
				ShowPopupMessage("Saved the text for the AR Object.");
				break;
			case ARTrackedImageInfos.ObjectType.Type_Image:
				if (userUploadedFile) {
					// Get the uploaded image file (at runtime, from the path of the picked file)
					Texture2D uploadedImage = null;
					uploadedImage = GetImageFromPath(uploadedFilePath);
					// Set the uploaded image
					infos.SetObject_Image(uploadedImage);
					UpdateARObjectSpecificInfos();
				} else {
					infos.SetObject_Image(objectString);
					// Download the image from the URL and also set the image texture
					DownloadImageFromWeb(objectString,
						(Texture2D downloadedImage) => {
							// Set the downloaded image
							infos.SetObject_Image(downloadedImage);
							UpdateARObjectSpecificInfos();
						}, () => {
							Debug.LogError("Failed to download the image from the URL: " + objectString);
						}
					);
				}
				ShowPopupMessage("Saved the image for the AR Object.");
				break;
			case ARTrackedImageInfos.ObjectType.Type_Video:
				if (userUploadedFile) {
					// Get the uploaded video file (at runtime, using the file picker with the appropriate library)
					VideoClip uploadedVideo = null;
					infos.SetObject_Video(uploadedVideo);
					UpdateARObjectSpecificInfos();
				} else {
					infos.SetObject_Video(objectString);
					// Download the video from the URL and also set the video URL (as a VideoClip)
					DownloadImageFromWeb(objectString,
						(VideoClip downloadedVideo) => {
							infos.SetObject_Video(downloadedVideo);
							UpdateARObjectSpecificInfos();
						}, () => {
							Debug.LogError("Failed to download the video from the URL: " + objectString);
						}
					);
				}
				ShowPopupMessage("Saved the video for the AR Object.");
				break;
			case ARTrackedImageInfos.ObjectType.Type_3D:
				// Get the uploaded GLTF file (at runtime, using the file picker with the appropriate library)
				if (uploadedFilePath != null) {
					ShowPopupMessage("Trying to import the GLTF model...");
					ImportGLTFModelFromPath(uploadedFilePath, (GameObject obj) => {
						infos.SetObject_3DModel(obj);
						UpdateARObjectSpecificInfos();
						obj.SetActive(false);
						ShowPopupMessage("Successfully imported the GLTF model!");
					}, () => {
						Debug.LogError("Failed to import the GLTF model from the file: " + uploadedFilePath);
						ShowPopupMessage("Failed to import the GLTF model!");
					});
				} else {
					Debug.Log("No GLTF file uploaded for the 3D model, skipping update of associated 3D object...");
				}
				break;
			case ARTrackedImageInfos.ObjectType.Unset:
				Debug.Log("WARNING: Invalid object type: " + type);
				break;
			default:
				Debug.LogError("Unknown object type: " + type);
				break;
		}
		Debug.Log("Saved the current AR Object infos.");
		// Refresh the edit view
		RefreshEditAROjectView();
	}

	/// <summary>
	/// Download an image web resource and call the OnSuccess callback with the downloaded data, or the OnError callback if the download fails.
	/// </summary>
	public void DownloadImageFromWeb<T>(string MediaUrl, Action<T> OnSuccess, Action OnError = null) {
		IEnumerator DownloadImage_Aux() {
			UnityWebRequest request = UnityWebRequestTexture.GetTexture(MediaUrl);
			yield return request.SendWebRequest();
			//if (request.isNetworkError || request.isHttpError) {
			if (request.result != UnityWebRequest.Result.Success) {
				Debug.Log(request.error + " - " + request.downloadHandler.text);
				OnError();
			} else {
				Debug.Log("Image request succesful");
				T downloaded = (T) Convert.ChangeType(DownloadHandlerTexture.GetContent(request), typeof(T));
				OnSuccess(downloaded);
			}
		}
		StartCoroutine(DownloadImage_Aux());
	}

	/// <summary>
	/// Download a JSON web resource and call the OnSuccess callback with the downloaded data, or the OnError callback if the download fails.
	/// </summary>
	public void DownloadJSONFromWeb(string MediaUrl, Action<string> OnSuccess, Action OnError = null) {
		IEnumerator DownloadJSON_Aux() {
			UnityWebRequest request = UnityWebRequest.Get(MediaUrl);
			yield return request.SendWebRequest();
			//if (request.isNetworkError || request.isHttpError) {
			if (request.result != UnityWebRequest.Result.Success) {
				Debug.Log(request.error + " - " + request.downloadHandler.text);
				OnError();
			} else {
				Debug.Log("JSON request succesful");
				string downloaded = request.downloadHandler.text;
				OnSuccess(downloaded);
			}
		}
		StartCoroutine(DownloadJSON_Aux());
	}

	public void SendJSONRequest(string url, string JSONData, Action<string> OnSuccess, Action OnError = null) {
		IEnumerator SendJSONRequest_Aux() {
			UnityWebRequest request = new UnityWebRequest(url, "POST");
			byte[] bodyRaw = Encoding.UTF8.GetBytes(JSONData);
			request.uploadHandler = new UploadHandlerRaw(bodyRaw);
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", "application/json");
			yield return request.SendWebRequest();
			if (request.result != UnityWebRequest.Result.Success) {
				Debug.Log(request.error + " - " + request.downloadHandler.text);
				OnError();
			} else {
				Debug.Log("JSON request succesful");
				string downloaded = request.downloadHandler.text;
				OnSuccess(downloaded);
			}
		}
		StartCoroutine(SendJSONRequest_Aux());
	}

	// Function to open the file picker and get the file path
	public void OpenFilePicker() {
		Debug.Log("Opening the file picker...");
		// Get the currently chosen type
		//ARTrackedImageInfos.ObjectType type = imageTracker.ARImagesInfos[currentEditIndex].type;
		ARTrackedImageInfos.ObjectType type = (ARTrackedImageInfos.ObjectType) uiEdit_objectType.value;
		//string[] allowedFileTypes = null;
		//switch (type) {
		//	case ARTrackedImageInfos.ObjectType.Type_Image:
		//		allowedFileTypes = null;
		//		break;
		//	case ARTrackedImageInfos.ObjectType.Type_Video:
		//		allowedFileTypes = null;
		//		break;
		//	case ARTrackedImageInfos.ObjectType.Type_3D:
		//		// GLTF file
		//		allowedFileTypes = null;
		//		break;
		//	default:
		//		// Other types wont allow picking files
		//		allowedFileTypes = null;
		//		break;
		//}
		FilePickedCallback Callback = (string filePath) => {
			if (filePath == null || filePath.Length == 0) {
				Debug.LogWarning("No file picked.");
				ShowPopupMessage("No file picked...");
				return;
			}
			Debug.Log("File picked: [" + string.Join(", ", filePath) + "]");
			// Set the uploaded file path
			uploadedFilePath = filePath;
			// Show a popup message
			ShowPopupMessage("File picked: [" + string.Join(", ", filePath) + "]");
		};

		Permission p;
		//if (allowedFileTypes != null) {
		//	p = NativeFilePicker.PickFile(Callback, allowedFileTypes);
		//} else {
		//	p = NativeFilePicker.PickFile(Callback);
		//}
		p = NativeFilePicker.PickFile(Callback);
		if (p == Permission.Denied) {
			Debug.LogError("Permission denied to pick a file.");
		} else if (p == Permission.ShouldAsk) {
			Debug.LogWarning("Permission should be asked to pick a file.");
		} else if (p == Permission.Granted) {
			Debug.Log("Permission granted to pick a file.");
		}
	}

	// Use the glTFast library to import a GLTF model at the given path
	private async void ImportGLTFModelFromPath(string path, Action<GameObject> OnComplete, Action OnError = null) {
		// First step: load glTF
		Debug.Log("Loading glTF from path: " + path);
		// Create a container for the imported 3D models
		GltfImport gltf = new GltfImport();
		// Auxiliary function to try to load the file multiple times
		async Task<bool> TryImportModel(int attempt) {
			if (attempt >= 6) return false;
			string prefix = "";
			if (attempt >= 3) {
				prefix = "file://";
			}
			bool success = false;
			try {
				if (attempt % 3 == 0) {
					byte[] gltfData = File.ReadAllBytes(path);
					Debug.Log("Loaded GLTF binary data: " + gltfData.Length + " bytes");
					string absolutePath = Path.GetFullPath(path);
					success = await gltf.LoadGltfBinary(gltfData, new Uri(absolutePath));
				} else if (attempt % 3 == 1) {
					success = await gltf.Load(prefix + path);
				} else if (attempt % 3 == 2) {
					success = await gltf.LoadFile(prefix + path);
				}
			} catch (Exception e) {
				Debug.LogError("Error loading GLTF: " + e.Message);
				success = false;
			}
			return success;
		}
		int maxAttempts = 6;
		bool success = false;
		for (int i = 0; i < maxAttempts; i++) {
			success = await TryImportModel(i);
			Debug.Log("> Tried to load GLTF (attempt " + i + "): " + success);
			if (success) break;
		}
		if (success) {
			// Here you can customize the post-loading behavior
			// Instantiate each of the glTF's scenes
			for (int sceneId = 0; sceneId < gltf.SceneCount; sceneId++) {
				Debug.Log("Instantiating scene " + sceneId);
				await gltf.InstantiateSceneAsync(imported3DModelsContainer, sceneId);
			}
			// Call the OnComplete callback with the instantiated GameObject
			OnComplete(imported3DModelsContainer.gameObject);
		} else {
			Debug.LogError("Loading glTF failed!");
			if (OnError != null)
				OnError();
		}
	}

	// Get the image from the given path
	private Texture2D GetImageFromPath(string path) {
		byte[] fileData = File.ReadAllBytes(path);
		Texture2D texture = new Texture2D(2, 2);
		texture.LoadImage(fileData);
		return texture;
	}

	// Show a popup message
	public void ShowPopupMessage(string message) {
		uiPopUpMessageText.transform.parent.gameObject.SetActive(true);
		uiPopUpMessageText.text = message;
		popupMessagesCounter++;
		// Disable after some seconds
		float duration = 3f;
		StartCoroutine(DisableAfterTime(uiPopUpMessageText.transform.parent.gameObject, duration, popupMessagesCounter));
		IEnumerator DisableAfterTime(GameObject obj, float time, int counter) {
			yield return new WaitForSeconds(time);
			if (popupMessagesCounter == counter) obj.SetActive(false);
		}
	}

	// Refresh the UI of the AR view (either editing or not)
	private void RefreshUIARView() {
		imageTracker.StartARExperience();
		uiARListButton.gameObject.SetActive(editingCurrentARExperience);
		uiARAddButton.gameObject.SetActive(editingCurrentARExperience);
		//uiARExitButton.gameObject.SetActive(!editingCurrentARExperience);
		TextMeshProUGUI exitButtonText = uiARExitButton.GetComponentInChildren<TextMeshProUGUI>();
		if (editingCurrentARExperience) {
			exitButtonText.text = "l";	// Smallcase L to be shown as a "tick" character (rotated by 45 degrees)
			exitButtonText.margin = new Vector4(2.5f, -4.5f, 0, 0);
		} else {
			exitButtonText.text = "+"; // Plus sign to be shown as an "X" character (rotated by 45 degrees)
			exitButtonText.margin = new Vector4(0, 0, 0, 0);
		}
	}

	// While in main menu, toggle the list of created or downloaded AR experiences
	public void ToggleMainMenuCreatedOrDownloadedARExperiencesList() {
		showingCreadetExperiences = !showingCreadetExperiences;
		RefreshMainMenu();
	}
	
	// Refresh the main menu (with its AR experiences list)
	private void RefreshMainMenu() {
		// Initialize the AR Experience list if needed
		if (currentCreatedARExperiences == null) currentCreatedARExperiences = new List<ARExperience>();
		if (currentDownloadedARExperiences == null) currentDownloadedARExperiences = new List<ARExperience>();
		if (mainMenuDisplayedARExperiences == null) mainMenuDisplayedARExperiences = new List<ARDisplayExperienceScript>();
		// Set the list of created experiences as active
		mainMenuHeader_CreatedExperiences.gameObject.SetActive(showingCreadetExperiences);
		mainMenuHeader_DownloadedExperiences.gameObject.SetActive(!showingCreadetExperiences);
		// Swt the toggle buttons
		mainMenuToggleButton_Created.interactable = !showingCreadetExperiences;
		mainMenuToggleButton_Downloaded.interactable = showingCreadetExperiences;
		// Stop the current experience
		imageTracker.StopARExperience();
		// For each AR experience, show it in the main menu
		int mainMenuDisplayedExperiences = mainMenuContent.childCount;
		List<ARExperience> experiencesToUse;
		if (showingCreadetExperiences) experiencesToUse = currentCreatedARExperiences;
		else experiencesToUse = currentDownloadedARExperiences;
		for (int i = 0; i < experiencesToUse.Count; i++) {
			if (i >= mainMenuDisplayedExperiences) {
				// Create a new AR Experience object
				GameObject newARExperience = Instantiate(displayARExperiencePrefab, mainMenuContent);
				ARDisplayExperienceScript newARExperienceScript = newARExperience.GetComponent<ARDisplayExperienceScript>();
				mainMenuDisplayedARExperiences.Add(newARExperienceScript);
			}
			// Set the values of the AR Experience object
			mainMenuDisplayedARExperiences[i].gameObject.SetActive(true);
			int index = i;
			mainMenuDisplayedARExperiences[i].InitializeExperience(index, experiencesToUse[i].isPublicExperience, experiencesToUse[i].experienceCode);
		}
		for (int i = experiencesToUse.Count; i < mainMenuDisplayedExperiences; i++) {
			if (i >= mainMenuDisplayedARExperiences.Count) mainMenuDisplayedARExperiences.Add(mainMenuContent.GetChild(i).GetComponent<ARDisplayExperienceScript>());
			mainMenuDisplayedARExperiences[i].gameObject.SetActive(false);
		}
		// Set the height of the content of the main mennu list
		float singleObjectHeight = displayARExperiencePrefab.GetComponent<RectTransform>().rect.height + 3f;
		float padding = 9f;
		mainMenuContent.sizeDelta = new Vector2(mainMenuContent.sizeDelta.x, experiencesToUse.Count * singleObjectHeight + padding);
		// Refresh main menu user profile UI
		RefreshUserProfileMainMenuInfos();
	}

	public void RemoveARExperience(int index) {
		// Initialize the AR Experience list if needed
		if (currentCreatedARExperiences == null) currentCreatedARExperiences = new List<ARExperience>();
		if (currentDownloadedARExperiences == null) currentDownloadedARExperiences = new List<ARExperience>();
		// Remove the AR Experience from the list of AR Experiences
		Debug.Log("Removing AR Experience at index: " + index);
		if (showingCreadetExperiences) currentCreatedARExperiences.RemoveAt(index);
		else currentDownloadedARExperiences.RemoveAt(index);
		// Refresh the main menu
		RefreshMainMenu();
	}

	public void AddEmptyARExperience() {
		int experienceNumber = 1 + (showingCreadetExperiences ? currentCreatedARExperiences.Count : currentDownloadedARExperiences.Count);
		string name = "Experience " + (experienceNumber).ToString().PadLeft(2, '0');
		AddARExperience(name, GetCurrentUsername(), false);
	}

	public void AddDownloadedARExperience() {
		// Get the experience code
		string experienceCode = uiDownloadExperienceCode.text;
		// Check that the code is valid
		bool validCode = 
				(experienceCode[0] == '#' && experienceCode.Length == 5)
			|| (experienceCode[0] != '#' && experienceCode.Length == 4);
		if (!validCode) { 
			// Code is invalid
			Debug.LogError("Invalid experience code: " + experienceCode);
			ShowPopupMessage("Invalid experience code\nCode must start with '#' and be followed by alphanumeric 4 characters.");
		} else {
			// Show a loading screen
			ToggleLoadingScreen(true);
			// Download the AR Experience from the server
			string url = "https://usernamealreadytaken.eu.pythonanywhere.com/get_experience?code=" + experienceCode;
			DownloadJSONFromWeb(url, async (string downloaded) => {
				// Add the downloaded AR Experience (a JSON string) to the list of AR Experiences

				if (downloaded == null || downloaded.Length == 0) {
					Debug.LogError("AR Experience data not found");
					ToggleLoadingScreen(false);
					ShowPopupMessage("AR Experience data not found...");
				} else {
					// Get the actual experience json
					string experienceJson;
					// Find the field "result" and take its json content as the AR experience
					int resultIndex = downloaded.IndexOf("\"result\":");
					if (resultIndex == -1) {
						Debug.LogError("Invalid JSON format for the downloaded AR Experience\n" + downloaded);
						ToggleLoadingScreen(false);
						ShowPopupMessage("Invalid JSON format for the downloaded AR Experience...");
						return;
					}
					int startIndex = downloaded.IndexOf("{", resultIndex);
					int endIndex = -1;
					int openBrackets = 0;
					for (int i = startIndex; i < downloaded.Length; i++) {
						if (downloaded[i] == '{')
							openBrackets++;
						if (downloaded[i] == '}')
							openBrackets--;
						if (openBrackets == 0) {
							endIndex = i;
							break;
						}
					}
					if (openBrackets == 0) endIndex = downloaded.Length - 1;
					if (startIndex == -1 || endIndex == -1) {
						Debug.LogError("Invalid JSON format for the downloaded AR Experience");
						ToggleLoadingScreen(false);
						ShowPopupMessage("Invalid JSON format for the downloaded AR Experience...");
						return;
					}
					experienceJson = downloaded.Substring(startIndex, endIndex - startIndex + 1);
					Debug.Log("Downloaded AR Experience:\n" + experienceJson);
					// Done downloading
					Debug.Log("Succesfully downloaded the AR Experience!");
					ToggleLoadingScreen(false);
					ShowPopupMessage("Succesfully downloaded the AR Experience!");
					ARExperience newExperience = await ARExperience.FromJson(experienceJson);
					AddARExperience(newExperience);
				}
			}, () => {
				// Failed to download the AR Experience
				Debug.LogError("Failed to download the AR Experience from the URL: " + url);
				ToggleLoadingScreen(false);
				ShowPopupMessage("Failed to download the AR Experience...");
			});
		}
	}

	public void AddARExperience(string name, string creator, bool isPublic, string experienceCode = "") {
		// Add the AR Experience to the list of AR Experiences
		ARExperience newExperience = new ARExperience(name, creator, isPublic, experienceCode);
		AddARExperience(newExperience);
	}

	public void AddARExperience(ARExperience experience) {
		// Initialize the AR Experience list if needed
		if (currentCreatedARExperiences == null) currentCreatedARExperiences = new List<ARExperience>();
		if (currentDownloadedARExperiences == null) currentDownloadedARExperiences = new List<ARExperience>();
		// Add the AR Experience to the list of AR Experiences
		if (showingCreadetExperiences) currentCreatedARExperiences.Add(experience);
		else currentDownloadedARExperiences.Add(experience);
		// Refresh the main menu (if needed)
		if (currentCameraState == CameraState.MainMenu) RefreshMainMenu();
	}

	public void SetCurrentARExperience(int experienceindex, bool editMode) {
		currentARExperienceIndex = experienceindex;
		editingCurrentARExperience = editMode;
	}

	private void InitializeARExperience(ARExperience experience, bool isEditing) {
		// TO DO: actually initialize the AR Experience
		Debug.Log("Initializing AR Experience: " + experience.experienceName + " (Editing: " + isEditing + ")");
	}

	/// <summary>
	/// Returns the username of the current user if logged in, otherwise returns "GUEST".
	/// </summary>
	public string GetCurrentUsername() {
		if (isLoggedIn) return username;
		else return "GUEST";
	}

	private string textUiDebugAdditionalText;

	private float fpsTextRefreshRate = 2f;  // N times per second
	private float lastFPSRefreshTime = 0f;
	private float fpsTotal = 0f;
	private int fpsMeasurements = 0;

	private void UpdateUiTextDebug() {
		// Set the top left text
		if (imageTracker == null || imageTracker.trackedImagesManager == null || imageTracker.trackedImagesManager.referenceLibrary == null) {
			uiTextDebug01.text = "No AR Anchors...";
		} else {
			string stringToSet = "";
			stringToSet += "Current Camera State: " + currentCameraState.ToString() + "\n";
			stringToSet += imageTracker.trackedImagesManager.referenceLibrary.count + " AR Anchors\n";
			stringToSet += imageTracker.GetARObjects().Count + " AR Objects (active: " + imageTracker.GetARObjects().Select(o => o.Value.activeSelf).Count() + ")\n";
			stringToSet += textUiDebugAdditionalText;
			uiTextDebug01.text = stringToSet;
		}
		// Set the top right text
		if (Time.time - lastFPSRefreshTime >= 1f / fpsTextRefreshRate) {
			lastFPSRefreshTime = Time.time;
			// Set the text for the FPS
			float meanFPS = (fpsTotal + (1f / Time.deltaTime)) / (fpsMeasurements+1);
			string fpsText = meanFPS.ToString("F1") + " FPS";
			uiTextDebug02.text = fpsText;
			// Reset fps counters
			fpsTotal = 0;
			fpsMeasurements = 0;
		} else {
			fpsTotal += 1f / Time.deltaTime;
			fpsMeasurements++;
		}
	}

	public void AddTextUIDebug(string text) {
		textUiDebugAdditionalText = text;
	}

	// Save the experience then display the Main Menu UI
	public void SaveExperience() {
		// Save the current AR Experience
		int ARImagesCount = imageTracker.ARImagesInfos.Length;
		if (showingCreadetExperiences) {
			currentCreatedARExperiences[currentARExperienceIndex].ARObjectsInfos = new List<ARTrackedImageInfos>();
			for (int i = 0; i < ARImagesCount; i++) {
				currentCreatedARExperiences[currentARExperienceIndex].ARObjectsInfos.Add(imageTracker.ARImagesInfos[i]);
			}
		} else {
			currentDownloadedARExperiences[currentARExperienceIndex].ARObjectsInfos = new List<ARTrackedImageInfos>();
			for (int i = 0; i < ARImagesCount; i++) {
				currentDownloadedARExperiences[currentARExperienceIndex].ARObjectsInfos.Add(imageTracker.ARImagesInfos[i]);
			}
		}
		// Set the AR Experience as initialized
		DisplayUI_MainMenu();
	}

	// Toggle the in-game loading screen
	public void ToggleLoadingScreen(bool show, bool forceSet = false) {
		bool showingLoadingScreen = currentCameraState == CameraState.Loading;
		if (showingLoadingScreen == show && !forceSet) return;
		if (currentCameraState != CameraState.Loading) cameraStateBeforeLoading = currentCameraState;
		if (show) SetCameraState(CameraState.Loading);
		else SetCameraState(cameraStateBeforeLoading);
		//showingLoadingScreen = show;
	}

	// Returns true if currently loading something
	public bool GetShowingLoadingScreen() {
		return currentCameraState == CameraState.Loading;
	}

	public ARExperience GetARExperience(int index, bool useCreatedExperiences) {
		if (useCreatedExperiences) {
			if (index < 0 || index >= currentCreatedARExperiences.Count) return null;
			return currentCreatedARExperiences[index];
		} else {
			if (index < 0 || index >= currentDownloadedARExperiences.Count) return null;
			return currentDownloadedARExperiences[index];
		}
	}

	private void RefreshUserProfileMainMenuInfos() {
		if (isLoggedIn) {
			string username = GetCurrentUsername();
			mainMenuProfile_UsernameText.text = username;
			mainMenuProfile_ProfileButton.interactable = true;
			mainMenuProfile_ProfileButtonText.text = username[0].ToString().ToUpper();
			mainMenuProfile_LogInOutButtonText.text = "LOGOUT";
			ColorBlock colors = mainMenuProfile_LogInOutButton.colors;
			ColorUtility.TryParseHtmlString("#D95555FF", out Color col);
			mainMenuProfile_LogInOutButton.colors = new ColorBlock() {
				normalColor = col,
				highlightedColor = col,
				pressedColor = colors.pressedColor,
				selectedColor = colors.selectedColor,
				disabledColor = colors.disabledColor,
				colorMultiplier = colors.colorMultiplier,
				fadeDuration = colors.fadeDuration
			};
		} else {
			mainMenuProfile_UsernameText.text = "GUEST";
			mainMenuProfile_ProfileButton.interactable = false;
			mainMenuProfile_ProfileButtonText.text = "?";
			mainMenuProfile_LogInOutButtonText.text = "LOGIN";
			ColorBlock colors = mainMenuProfile_LogInOutButton.colors;
			ColorUtility.TryParseHtmlString("#87C160FF", out Color col);
			mainMenuProfile_LogInOutButton.colors = new ColorBlock() {
				normalColor = col,
				highlightedColor = col,
				pressedColor = colors.pressedColor,
				selectedColor = colors.selectedColor,
				disabledColor = colors.disabledColor,
				colorMultiplier = colors.colorMultiplier,
				fadeDuration = colors.fadeDuration
			};
		}
	}

	private void RefreshLoginScreen() {
		if (isTryingToLogin && !isLoggedIn) {
			// User is not logged but is trying to login (i.e. now needs to input the OTP code to be able to login)
			loginUsernameOrEmailInputField.interactable = false;
			getOTPCodeButton.interactable = false;
			otpInputField.interactable = true;
			verifyOTPButton.interactable = true;
		} else {
			// User is not logged in and needs to input its email to then get the OTP code
			loginUsernameOrEmailInputField.interactable = true;
			getOTPCodeButton.interactable = true;
			otpInputField.interactable = false;
			verifyOTPButton.interactable = false;
		}
	}

	public void SendLoginRequest(string usernameOrEmail) {
		// Send a "login" request for the given username
		string url = "https://usernamealreadytaken.eu.pythonanywhere.com/login";
		string requestJSONBody = "{\"email\": \"" + usernameOrEmail + "\"}";
		ToggleLoadingScreen(true);
		SendJSONRequest(url, requestJSONBody, (string response) => {
			ToggleLoadingScreen(false);
			// Parse the response
			if (response == null || response.Length == 0) {
				Debug.LogError("Failed to login with the username: " + usernameOrEmail + "\nResponse is empty");
				ShowPopupMessage("Failed to login with the username: " + usernameOrEmail);
			} else {
				// Parse the response and check if the "success" json value is set to True
				bool success = false;
				int successIndex = response.IndexOf("\"success\":");
				if (successIndex != -1) {
					int startIndex = response.IndexOf(":", successIndex);
					int endIndex = response.IndexOf(",", successIndex);
					if (endIndex == -1) endIndex = response.IndexOf("}", successIndex);
					if (startIndex != -1 && endIndex != -1) {
						string successString = response.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
						success = successString.ToLower() == "true";
					}
				}
				if (success) {
					// Set the "isTryingToLogin" flag
					isTryingToLogin = true;
					// Refresh the main menu user profile UI
					RefreshLoginScreen();
					// Show a popup message
					ShowPopupMessage("Enter the OTP code sent to " + usernameOrEmail);
				} else {
					Debug.LogError("Failed to login with the username: " + usernameOrEmail + "\nResponse: " + response);
					ShowPopupMessage("Failed to login with the username: " + usernameOrEmail);
				}
			}
		}, () => {
			Debug.LogError("Failed to login with the username: " + usernameOrEmail);
			ShowPopupMessage("Failed to login with the username: " + usernameOrEmail);
		});

	}

	public void SendVerifyOTPRequest(string email, string otp) {
		// Send a "verify OTP" request for the given OTP
		string url = "https://usernamealreadytaken.eu.pythonanywhere.com/verify";
		string requestJSONBody = "{\"otp\": \"" + otp + "\",\"email\": \"" + email + "\"}";
		ToggleLoadingScreen(true);
		SendJSONRequest(url, requestJSONBody, (string response) => {
			// Parse the response
			ToggleLoadingScreen(false);
			if (response == null || response.Length == 0) {
				Debug.LogError("Failed to verify the OTP: " + otp + "\nResponse is empty");
				ShowPopupMessage("Failed to verify the OTP: " + otp);
			} else {
				// Parse the response and check if the "verified" json value is set to True
				bool success = false;
				int successIndex = response.IndexOf("\"verified\":");
				if (successIndex != -1) {
					int startIndex = response.IndexOf(":", successIndex);
					int endIndex = response.IndexOf(",", successIndex);
					if (endIndex == -1) endIndex = response.IndexOf("}", successIndex);
					if (startIndex != -1 && endIndex != -1) {
						string successString = response.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
						success = successString.ToLower() == "true";
					}
				}
				// Get the username and the token as the "username" and "token" json values
				string username = null;
				string token = null;
				if (success) {
					int usernameIndex = response.IndexOf("\"username\":");
					if (usernameIndex != -1) {
						int startIndex = response.IndexOf("\"", usernameIndex + 10);
						int endIndex = response.IndexOf("\"", startIndex + 1);
						if (startIndex != -1 && endIndex != -1) {
							username = response.Substring(startIndex + 1, endIndex - startIndex - 1);
						}
					}
					int tokenIndex = response.IndexOf("\"token\":");
					if (tokenIndex != -1) {
						int startIndex = response.IndexOf("\"", tokenIndex + 8);
						int endIndex = response.IndexOf("\"", startIndex + 1);
						if (startIndex != -1 && endIndex != -1) {
							token = response.Substring(startIndex + 1, endIndex - startIndex - 1);
						}
					}
					if (username == null || token == null) {
						success = false;
					}
				}
				if (success) { 
					// Store the username and the token
					isLoggedIn = true;
					this.username = username;
					this.userToken = token;
					// Show a popup message
					ShowPopupMessage("Successfully logged in as " + username);
					Debug.Log("Successfully logged in as " + username + " (token: " + token + ")");
					// Display the main menu UI (refreshed as logged in)
					DisplayUI_MainMenu();
				} else {
					Debug.LogError("Failed to verify the OTP: " + otp + "\nResponse: " + response);
					ShowPopupMessage("Failed to verify the OTP code " + otp);
				}
			}
		}, () => {
			Debug.LogError("Failed to verify the OTP: " + otp);
			ShowPopupMessage("Failed to verify the OTP code " + otp);
		});

	}

	// Function executed at the press of the login/logout button
	public void LoginFunction_ToggleLogInOut() {
		if (isLoggedIn) {
			// Logout
			isLoggedIn = false;
			username = null;
			userToken = null;
			DisplayUI_MainMenu();
		} else {
			// Go to login screen
			DisplayUI_LoginScreen();
		}
	}

	// Function executed at the press of the get OTP code button
	public void LoginFunction_SendLoginRequest() {
		string usernameOrEmail = loginUsernameOrEmailInputField.text;
		SendLoginRequest(usernameOrEmail);
	}

	// Function executed at the press of the verify OTP button
	public void LoginFunction_VerifyOTP() {
		string email = loginUsernameOrEmailInputField.text;
		string otp = otpInputField.text;
		SendVerifyOTPRequest(email, otp);
	}

}
