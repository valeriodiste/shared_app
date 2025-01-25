using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static ImageTracker;

public class ARDisplayObjectScript : MonoBehaviour {

	public TMP_InputField objectName;
	public Button removeButton;
	public Button editButton;
	public RawImage anchorImage;
	//public TMP_Dropdown objectType;
	//public Button uploadFile;

	private int index;

	private ARTrackedImageInfos.ObjectType type;

	public void SetValues(CameraController cameraController, int index, string name, Texture2D anchorImage) {
		this.anchorImage.texture = anchorImage;
		float fixedHeight = this.anchorImage.rectTransform.rect.height;
		float aspectRatio = anchorImage.width / (float) anchorImage.height;
		//this.anchorImage.rectTransform.rect.Set(this.anchorImage.rectTransform.rect.x, this.anchorImage.rectTransform.rect.y, fixedHeight * aspectRatio, fixedHeight);
		this.anchorImage.rectTransform.sizeDelta = new Vector2(fixedHeight * aspectRatio, fixedHeight);
		objectName.text = name;
		this.type = ARTrackedImageInfos.ObjectType.Type_Text;
		//switch (type) {
		//	case ObjectType.Tpye_Text:
		//		objectType.value = 1;
		//		break;
		//	case ObjectType.Tpye_Image:
		//		objectType.value = 2;
		//		break;
		//	case ObjectType.Tpye_3D:
		//		objectType.value = 3;
		//		break;
		//	default:
		//		Debug.LogError("Invalid object type: " + type);
		//		objectType.value = 0;
		//		break;
		//}
		removeButton.onClick.AddListener(() => cameraController.RemoveARAnchor(index));
		editButton.onClick.AddListener(() => cameraController.DisplayUI_EditARObject(index));
	}


}
