using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextWindowScript : MonoBehaviour {

	private Camera gameCamera;
	private RectTransform rectTransform;
	private TextMeshPro windowText;
	private SpriteRenderer windowSprite;
	private ContentSizeFitter contentSizeFitter;

	// Start is called before the first frame update
	private void Awake() {
		gameCamera = FindObjectOfType<Camera>();
		rectTransform = GetComponent<RectTransform>();
		windowText = transform.GetComponentInChildren<TextMeshPro>();
		windowSprite = GetComponent<SpriteRenderer>();
		contentSizeFitter = GetComponent<ContentSizeFitter>();
	}

	// Update is called once per frame
	private void Update() {
		FaceCamera();
		ResizeWindowSprite();
		// For debug, on press of key "J", set the text (use the new Input System for better key detection)
		//if (InputSystem.devices[0].name == "Keyboard" && Keyboard.current.jKey.wasPressedThisFrame) SetText("Hello World!\nThis is a test text");
	}

	private void FaceCamera() {
		// Rotate the object on its Y axis to face the camera
		Vector3 targetPosition = transform.position + gameCamera.transform.rotation * Vector3.forward;
		Vector3 targetOrientation = gameCamera.transform.rotation * Vector3.up;
		//transform.LookAt(targetPosition, targetOrientation);
		Quaternion rotation = Quaternion.LookRotation(targetPosition - transform.position, targetOrientation);
		transform.eulerAngles = new Vector3(0, rotation.eulerAngles.y, 0);
	}

	private void ResizeWindowSprite() {
		if (windowSprite.size != rectTransform.sizeDelta) windowSprite.size = new Vector2(rectTransform.sizeDelta.x, rectTransform.sizeDelta.y);
	}

	public void SetText(string text) {
		if (!windowText) Awake();
		windowText.text = text;
		windowText.ForceMeshUpdate(true, true);
		windowText.rectTransform.sizeDelta = new Vector2(windowText.preferredWidth, windowText.preferredHeight);
		contentSizeFitter.SetLayoutVertical();
		contentSizeFitter.SetLayoutHorizontal();
		rectTransform.ForceUpdateRectTransforms();
	}

}
