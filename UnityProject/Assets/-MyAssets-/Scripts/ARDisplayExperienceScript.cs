using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using static CameraController;

public class ARDisplayExperienceScript : MonoBehaviour {

	private CameraController cameraController;

	public TMP_InputField experienceNameText;

	public Button editButton;
	public Button removeButton;
	public Button startButton;
	public Button uploadOrUpdateButton;

	private ARExperience ARExperience;

	// Stored values for the experience
	private int experienceIndex;
	private string experienceCode;
	private bool isDownloaded;

	// Bool to avoid removing the same ability more than once
	private bool removedExperience;

	//// Start is called before the first frame update
	//void Start() {
	//	if (!cameraController) cameraController = FindObjectOfType<CameraController>();
	//}

	private void OnEnable() {
		if (!cameraController)
			cameraController = FindObjectOfType<CameraController>();
	}

	// Update is called once per frame
	void Update() {
		// On click on the start button, start the AR experience
		if (startButton != null) {
			startButton.onClick.AddListener(() => {
				// Start the AR experience
				cameraController.SetCurrentARExperience(experienceIndex, false);
				cameraController.DisplayUI_ARView();
			});
		}
		// On click on the upload/update button, upload or update the AR experience online
		if (uploadOrUpdateButton != null) {
			uploadOrUpdateButton.onClick.AddListener(() => {
				// Upload or update the AR experience online

			});
		}
		// On click on the remove button, remove the AR experience
		IEnumerator RemoveThisExperience() {
			yield return new WaitForEndOfFrame();
			cameraController.RemoveARExperience(experienceIndex);
		}
		if (removeButton != null) {
			removeButton.onClick.AddListener(() => {
				// Remove the AR experience
				if (!removedExperience) {
					removedExperience = true;
					StartCoroutine(RemoveThisExperience());   // Use a coroutine to avoid triggering multiple presses of different AR experiences remove buttons
				}
			});
		}
		// On click on the edit button, edit the AR experience
		if (editButton != null) {
			editButton.onClick.AddListener(() => {
				// Edit the AR experience
				cameraController.SetCurrentARExperience(experienceIndex, true);
				cameraController.DisplayUI_ARView();
			});
		}


	}

	public void InitializeExperience(int index, bool isDownloaded, string experienceCode) {
		// Set the experience variables
		this.experienceIndex = index;
		this.experienceCode = experienceCode;
		this.isDownloaded = isDownloaded;
		// Set the appearance
		experienceNameText.text = name;
		experienceNameText.interactable = !isDownloaded;
		editButton.interactable = !isDownloaded;
		SetUploadOrUpdateButtonState();
		// Set the removed experience flag
		removedExperience = false;
		// Set the AR experience
		ARExperience = cameraController.GetARExperience(index, !isDownloaded);
	}

	private void SetUploadOrUpdateButtonState() {
		TextMeshProUGUI buttonText = uploadOrUpdateButton.GetComponentInChildren<TextMeshProUGUI>();
		bool interactable;
		string buttonTextString;
		if (ARExperience == null || isDownloaded) { 
			interactable = false;
			buttonTextString = "UPLOAD";
		} else {
			interactable = true;
			if (ARExperience.isPublicExperience && ARExperience.experienceCode != null && ARExperience.experienceCode != "") {
				string experienceCode = ARExperience.experienceCode;
				buttonTextString = $"UPDATE<alpha=#55><size=0.8em>({experienceCode})</size>";
			} else {
				buttonTextString = "UPLOAD";
			}
		}
		uploadOrUpdateButton.interactable = interactable;
		buttonText.text = buttonTextString;
	}

}
