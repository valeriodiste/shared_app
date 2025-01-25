using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ImageWindowScript : MonoBehaviour {

	private Camera gameCamera;
	private RectTransform rectTransform;
	//private TextMeshPro windowText;
	//private SpriteRenderer windowSprite;
	//private SpriteRenderer imageSprite;
	//private ContentSizeFitter contentSizeFitter;
	private RawImage windowImage;
	private Canvas canvas;

	// Start is called before the first frame update
	private void Awake() {
		gameCamera = FindObjectOfType<Camera>();
		rectTransform = GetComponent<RectTransform>();
		//windowText = transform.GetComponentInChildren<TextMeshPro>();
		//windowSprite = GetComponent<SpriteRenderer>();
		//contentSizeFitter = GetComponent<ContentSizeFitter>();
		windowImage = GetComponent<RawImage>();
		canvas = transform.parent.GetComponent<Canvas>();
		canvas.worldCamera = gameCamera;
	}

	// Update is called once per frame
	private void Update() {
		FaceCamera();
		//ResizeWindowSprite();
		// For debug, on press of key "J", set the text (use the new Input System for better key detection)
		//if (InputSystem.devices[0].name == "Keyboard" && Keyboard.current.jKey.wasPressedThisFrame) SetText("Hello World!\nThis is a test text");
	}

	//private void FaceCamera() {
	//	// Rotate the object on its Y axis to face the camera
	//	Vector3 targetPosition = transform.position + gameCamera.transform.rotation * Vector3.forward;
	//	Vector3 targetOrientation = gameCamera.transform.rotation * Vector3.up;
	//	//transform.LookAt(targetPosition, targetOrientation);
	//	Quaternion rotation = Quaternion.LookRotation(targetPosition - transform.position, targetOrientation);
	//	transform.eulerAngles = new Vector3(0, rotation.eulerAngles.y, 0);
	//}
	private void FaceCamera() {
		// Rotate the object on its Y axis to face the camera
		Vector3 targetPosition = canvas.transform.position + gameCamera.transform.rotation * Vector3.forward;
		Vector3 targetOrientation = gameCamera.transform.rotation * Vector3.up;
		//transform.LookAt(targetPosition, targetOrientation);
		Quaternion rotation = Quaternion.LookRotation(targetPosition - canvas.transform.position, targetOrientation);
		canvas.transform.eulerAngles = new Vector3(0, rotation.eulerAngles.y, 0);
	}

	//private void ResizeWindowSprite() {
	//	if (windowSprite.size != rectTransform.sizeDelta) windowSprite.size = new Vector2(rectTransform.sizeDelta.x, rectTransform.sizeDelta.y);
	//}

	public void SetImage(Texture2D image) {
		//windowText.text = text;
		//windowText.ForceMeshUpdate(true, true);
		//windowText.rectTransform.sizeDelta = new Vector2(windowText.preferredWidth, windowText.preferredHeight);
		//contentSizeFitter.SetLayoutVertical();
		//contentSizeFitter.SetLayoutHorizontal();

		if (!windowImage) Awake();

		windowImage.texture = image;

		rectTransform.ForceUpdateRectTransforms();
	}

}
