using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Threading.Tasks;

public class ImageTracker : MonoBehaviour {

	private CameraController cameraController;

	public ARTrackedImageManager trackedImagesManager;

	public ARTrackedImageInfos[] ARImagesInfos;

	private Dictionary<string, GameObject> ARObjects;
	
	private bool isTracking;

	void Awake() {
		if (!trackedImagesManager) trackedImagesManager = FindObjectOfType<ARTrackedImageManager>();
		if (!cameraController) cameraController = FindObjectOfType<CameraController>();
		ARObjects = new Dictionary<string, GameObject>();
		// For start, remove every image in the ARImagesInfos (comment for testing)
		ARImagesInfos = new ARTrackedImageInfos[0];
		// Create a new MutableRuntimeReferenceImageLibrary and assign it to the ARTrackedImageManager (i.e. create a reference image library at runtime)
		if (trackedImagesManager.descriptor != null && trackedImagesManager.descriptor.supportsMutableLibrary) {
			var mutableLibrary = trackedImagesManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
			for (int i = 0; i < ARImagesInfos.Length; i++) {
				// Get the AR object infos (image to track and AR object prefab)
				ARTrackedImageInfos infos = ARImagesInfos[i];
				if (infos.ARObject == null || infos.image == null) {
					Debug.LogError("ERROR: ARObject or image is null.");
					continue;
				}
				// Add trackable image
				AddARTrackedImageAndObjectToLibrary(infos, mutableLibrary);
			}
			// Initialize the native image manager (only needed for the first time)
			InitializeNativeImageManager(mutableLibrary);
		} else {
			Debug.LogError("ERROR: MutableRuntimeReferenceImageLibrary is not supported.");
		}
	}

	public void InitializeNativeImageManager(MutableRuntimeReferenceImageLibrary mutableLibrary) {
		IEnumerator InitializeNativeImageManagerCoroutine() {
			trackedImagesManager.enabled = false;
			yield return new WaitForEndOfFrame();
			trackedImagesManager.referenceLibrary = mutableLibrary;
			yield return new WaitForEndOfFrame();
			trackedImagesManager.enabled = true;
		}
		StartCoroutine(InitializeNativeImageManagerCoroutine());
	}

	public void AddARTrackedImageAndObjectToLibrary(ARTrackedImageInfos infos, MutableRuntimeReferenceImageLibrary mutableLibrary) {
		try {
			Texture2D newImageTexture = infos.image;
			string newImageName = infos.name;
			Debug.Log("Trying to add image to library... (" + newImageName + ")");
			// Check if the trackable is already in the list of trackables
			if (mutableLibrary is IReferenceImageLibrary library) {
				for (int i = 0; i < library.count; i++) {
					if (library[i].name == newImageName) {
						Debug.LogWarning($"Image {newImageName} is already in the library, not adding it again...");
						return;
					}
				}
			}
			float imageWidth = infos.markerSize.x;
			AddReferenceImageJobState jobState = mutableLibrary.ScheduleAddImageWithValidationJob(
				 newImageTexture,
				 newImageName,
				 //null // Dont set a default size instead of using the size from the image in image.size
				 imageWidth
			);
			JobHandle jobHandle = jobState.jobHandle;
			jobHandle.Complete();
			if (jobState.status == AddReferenceImageJobStatus.Success) {
				Debug.Log($"Image {newImageName} added to library successfully.");
			} else {
				//should report status "ErrorInvalidImage" if arcore rejects image
				Debug.LogWarning($"Failed to add image {newImageName} to library. {jobState.status}");
			}
			// Also add the object to the list of AR objects
			GameObject newARObject = Instantiate(infos.ARObject, Vector3.zero, Quaternion.identity);
			newARObject.SetActive(false);
			newARObject.name = infos.name;
			ARObjects.Add(newImageName, newARObject);
			// Add the AR object to the list of AR Infos object to the array
			List<ARTrackedImageInfos> newARImagesInfos = ARImagesInfos.ToList();
			if (newARImagesInfos.Count(item => item.name == infos.name) == 0)
				newARImagesInfos.Add(infos);
			ARImagesInfos = newARImagesInfos.ToArray();
		} catch (Exception e) {
			Debug.LogError($"Failed to add image {infos.name} to library:\n{e}");
			// Print the supported texture formats (note that the RGBA32 format should almost always be supported, use that if in doubt)
			int supportedFormatCount = mutableLibrary.supportedTextureFormatCount;
			string supportedFormatsNoteString = $"NOTE: Mutable library supports {supportedFormatCount} texture formats, maybe the image format you are using is not supported.";
			for (int i = 0; i < supportedFormatCount; i++) {
				TextureFormat supportedFormat = mutableLibrary.GetSupportedTextureFormatAt(i);
				supportedFormatsNoteString += $"\nSupported Texture Format {i}: {supportedFormat}";
			}
			Debug.Log(supportedFormatsNoteString);
		}
	}

	public void RemoveARTrackedImage(int index, Action OnComplete = null) {
		if (ARImagesInfos.Length <= index) {
			Debug.Log("ERROR: Index out of range (" + index + ").");
			return;
		}
		ARTrackedImageInfos infos = ARImagesInfos[index];
		if (infos == null) {
			Debug.Log("ERROR: ARTrackedImageInfos is null.");
			return;
		}
		if (infos.name == null) {
			Debug.Log("ERROR: ARTrackedImageInfos name is null.");
			return;
		}
		if (ARObjects.ContainsKey(infos.name)) {
			ARObjects.Remove(infos.name);
		}
		List<ARTrackedImageInfos> newARImagesInfos = ARImagesInfos.ToList();
		newARImagesInfos.RemoveAt(index);
		ARImagesInfos = newARImagesInfos.ToArray();
		// Update the library
		if (trackedImagesManager.descriptor != null && trackedImagesManager.descriptor.supportsMutableLibrary) {
			var mutableLibrary = trackedImagesManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;
			var newLibrary = trackedImagesManager.CreateRuntimeLibrary() as MutableRuntimeReferenceImageLibrary;
			//List<AddReferenceImageJobState> jobStates = new List<AddReferenceImageJobState>();
			for (int i = 0; i < ARImagesInfos.Length; i++) {
				AddReferenceImageJobState job = newLibrary.ScheduleAddImageWithValidationJob(
					ARImagesInfos[i].image,
					ARImagesInfos[i].name,
					null
				);
				//jobStates.Add(job);
				job.jobHandle.Complete();
			}
			if (OnComplete != null)
				OnComplete();
		} else {
			Debug.LogError("ERROR: MutableRuntimeReferenceImageLibrary is not supported.");
		}
	}

	void OnEnable() {
		trackedImagesManager.trackedImagesChanged += OnTrackedImagesChanged;
	}

	void OnDisable() {
		trackedImagesManager.trackedImagesChanged -= OnTrackedImagesChanged;
	}

	private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs) {
		string debugString = "";
		foreach (ARTrackedImage trackedImage in eventArgs.added) {
			UpdateARObject(trackedImage);
		}
		foreach (ARTrackedImage trackedImage in eventArgs.updated) {
			UpdateARObject(trackedImage);
		}
		foreach (ARTrackedImage trackedImage in eventArgs.removed) {
			Debug.Log("Removed: " + trackedImage.referenceImage.name);
			ARObjects[trackedImage.referenceImage.name].SetActive(false);
		}
		// Update the AR object position and rotation
		void UpdateARObject(ARTrackedImage trackedImage) {
			if (trackedImage.referenceImage.name == null) return;
			if (!ARObjects.ContainsKey(trackedImage.referenceImage.name)) {
				Debug.Log("WARNING: AR object not found for image " + trackedImage.referenceImage.name);
				return;
			}
			ARTrackedImageInfos infos = ARImagesInfos.ToList().Find(item => item.name == trackedImage.referenceImage.name);
			GameObject ARObject = ARObjects[trackedImage.referenceImage.name];
			ARObject.transform.position = trackedImage.transform.position + infos.objectStartPosition;
			ARObject.transform.localEulerAngles = trackedImage.transform.localEulerAngles + infos.objectStartRotation;
			float defaultScale = 0.025f;
			ARObject.transform.localScale = infos.objectStartScale * defaultScale;
			ARObject.SetActive(true);
			//Debug.Log("Updated: " + trackedImage.referenceImage.name + " (state: " + trackedImage.trackingState + " | active: " + ARObject.activeSelf + ")");
			if (ARObject.transform.localScale.x == 0f || ARObject.transform.localScale.y == 0f || ARObject.transform.localScale.z == 0f) {
				Debug.Log("WARNING: AR object " + trackedImage.referenceImage.name + " has a scale of 0...");
			}
			debugString += trackedImage.referenceImage.name + " - State: " + trackedImage.trackingState + " | Active: " + ARObject.activeSelf + "\n";
		}
		cameraController.AddTextUIDebug(debugString);
	}

	public void UpdateARObjectSpecificInfos(int index) {
		if (ARImagesInfos.Length <= index) {
			Debug.LogError("ERROR: Index out of range (" + index + ").");
			return;
		}
		ARTrackedImageInfos infos = ARImagesInfos[index];
		if (infos == null) {
			Debug.LogError("ERROR: ARTrackedImageInfos is null.");
			return;
		}
		if (infos.name == null) {
			Debug.LogError("ERROR: ARTrackedImageInfos name is null.");
			return;
		}
		if (ARObjects.ContainsKey(infos.name)) {
			GameObject ARObject = ARObjects[infos.name];
			if (ARObject != null) {
				// Get the AR Object script
				ARObjectScript arObjectScript = ARObject.GetComponent<ARObjectScript>();
				// Disable all object components
				//TextWindowScript textWindow = ARObject.transform.GetChild(0).GetComponentInChildren<TextWindowScript>(true);
				TextWindowScript textWindow = arObjectScript.textWindowScript;
				if (textWindow) textWindow.gameObject.SetActive(false);
				//Canvas imageAndVideoCanvas = ARObject.transform.GetChild(1).GetComponentInChildren<Canvas>(true);
				Canvas imageAndVideoCanvas = arObjectScript.imageAndVideoCanvas;
				if (imageAndVideoCanvas) imageAndVideoCanvas.gameObject.SetActive(false);
				//ImageWindowScript imageWindow = imageAndVideoCanvas.transform.GetComponentInChildren<ImageWindowScript>(true);
				ImageWindowScript imageWindow = arObjectScript.imageWindow;
				if (imageWindow) imageWindow.gameObject.SetActive(false);
				//VideoWindowScript videoWindowScript = imageAndVideoCanvas.transform.GetChild(1).GetComponentInChildren<VideoWindowScript>(true);
				VideoWindowScript videoWindowScript = arObjectScript.videoWindowScript;
				if (videoWindowScript) videoWindowScript.gameObject.SetActive(false);
				//Transform modelContainer = ARObject.transform.GetChild(2).Find("3DModelContainer");
				Transform modelContainer = arObjectScript.modelContainer;
				if (modelContainer) modelContainer.gameObject.SetActive(false);
				// Update object type specific values
				if (infos.type == ARTrackedImageInfos.ObjectType.Type_Text) {
					textWindow.gameObject.SetActive(true);
					string text = infos.GetObject_Text();
					textWindow.SetText(text);
				} else if (infos.type == ARTrackedImageInfos.ObjectType.Type_Image) {
					imageAndVideoCanvas.gameObject.SetActive(true);
					imageWindow.gameObject.SetActive(true);
					Texture2D image = infos.GetObject_Image(out string _);
					imageWindow.SetImage(image);
				} else if (infos.type == ARTrackedImageInfos.ObjectType.Type_Video) {
					imageAndVideoCanvas.gameObject.SetActive(true);
					videoWindowScript.gameObject.SetActive(true);
					VideoClip video = infos.GetObject_Video(out string _);
					videoWindowScript.SetVideo(video);
				} else if (infos.type == ARTrackedImageInfos.ObjectType.Type_3D) {
					modelContainer.gameObject.SetActive(true);
					GameObject model = infos.GetObject_3DModel();
					bool shouldUpdateObject = modelContainer.childCount == 0 || modelContainer.GetChild(0).gameObject.name != model.name;
					if (shouldUpdateObject) {
						if (modelContainer.childCount > 0) GameObject.Destroy(modelContainer.GetChild(0).gameObject);
						Instantiate(model, modelContainer);
					}
				}
				// Print a debug message
				Debug.Log("Updated AR object " + infos.name + " specific infos (type: " + infos.type + ")");
			} else {
				Debug.Log("<color=red>ERROR: AR object not found for image " + infos.name + "</color>");
			}
		}
	}

	public Dictionary<string, GameObject> GetARObjects() {
		return ARObjects;
	}

	public void StartARExperience(bool forceStart = false) {
		if (!forceStart && isTracking) return;
		trackedImagesManager.SetTrackablesActive(true);
		if (!trackedImagesManager.enabled) trackedImagesManager.enabled = true;
		isTracking = true;
		Debug.Log("AR experience started.");
	}

	public void StopARExperience(bool forceStop = false) {
		if (!forceStop && !isTracking) return;
		foreach (GameObject ARObject in ARObjects.Values) {
			ARObject.SetActive(false);
		}
		trackedImagesManager.SetTrackablesActive(false);
		if (!trackedImagesManager.enabled) trackedImagesManager.enabled = false;
		isTracking = false;
		Debug.Log("AR experience stopped.");
	}

	
	// Return the current AR experience's anchor images
	public Texture2D[] GetAnchorImages() {
		Texture2D[] anchorImages = new Texture2D[ARImagesInfos.Length];
		for (int i = 0; i < ARImagesInfos.Length; i++) {
			anchorImages[i] = ARImagesInfos[i].image;
		}
		return anchorImages;
	}

}
