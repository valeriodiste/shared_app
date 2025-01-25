using UnityEngine;
using UnityEngine.Video;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[System.Serializable]
public class ARTrackedImageInfos {
	// General infos
	public string name;
	public Texture2D image;
	public GameObject ARObject;
	public ObjectType type;
	public Vector2 markerSize;
	// Object infos
	public bool fullScreen = false;
	public Vector3 objectStartPosition = Vector3.zero;
	public Vector3 objectStartRotation = Vector3.zero;
	public Vector3 objectStartScale = Vector3.one;
	private string textObject_startText = "Unset text...";
	private string imageObject_imageURL = "";
	private Texture2D imageObject_Image;
	private string videoObject_videoURL = "";
	private VideoClip videoObject_videoClip;
	private GameObject modelObject_3DModel;
	// Enum for object types
	public enum ObjectType {
		Unset,
		// Types
		Type_Text,
		Type_Image,
		Type_Video,
		Type_3D,
	}
	// Empty Constructor
	public ARTrackedImageInfos() { }
	// General Constructor
	public ARTrackedImageInfos(string name, Texture2D image, GameObject ARObject, ObjectType type) {
		this.name = name;
		this.image = image;
		this.ARObject = ARObject;
		this.type = type;
		this.markerSize = new Vector2(1, 1);
	}
	// Functions to set text, image, video, 3D model, ecc...
	public void SetMarkerSize(float width, float height) {
		markerSize = new Vector2(width, height);
	}
	public void SetObject_Text(string text) {
		textObject_startText = text;
	}
	public void SetObject_Image(string imageURL) {
		imageObject_imageURL = imageURL;
	}
	public void SetObject_Image(Texture2D image) {
		imageObject_Image = image;
	}
	public void SetObject_Video(string videoURL) {
		videoObject_videoURL = videoURL;
	}
	public void SetObject_Video(VideoClip videoClip) {
		videoObject_videoClip = videoClip;
	}
	public void SetObject_3DModel(GameObject model) {
		modelObject_3DModel = model;
	}
	// Function to get the object infos
	public string GetObject_Text() {
		return textObject_startText;
	}
	public Texture2D GetObject_Image(out string url) {
		url = imageObject_imageURL;
		return imageObject_Image;
	}
	public VideoClip GetObject_Video(out string url) {
		url = videoObject_videoURL;
		return videoObject_videoClip;
	}
	public GameObject GetObject_3DModel() {
		return modelObject_3DModel;
	}

	public async Task<string> ToJson() {
		byte[] imageBytes = image?.EncodeToPNG();
		string imageBase64 = "";
		if (imageBytes != null) imageBase64 = System.Convert.ToBase64String(imageBytes);
		byte[] imageObject = imageObject_Image?.EncodeToPNG();
		string imageObjectBase64 = "";
		if (imageObject != null) imageObjectBase64 = System.Convert.ToBase64String(imageObject);
		byte[] modelBytes = await SerializationUtils.Serialize3DModelAsync(modelObject_3DModel);
		string modelBase64 = "";
		if (modelBytes != null) modelBase64 = System.Convert.ToBase64String(modelBytes);
		return "{"
			 + "\"name\":\"" + name + "\","
			 + "\"image\":\"" + imageBase64 + "\","
			 + "\"ARObject\":\"\","
			 + "\"type\":\"" + type.ToString() + "\","
			 + "\"markerSize\":Vec" + markerSize.ToString() + ","
			 + "\"fullScreen\":" + fullScreen.ToString().ToLower() + ","
			 + "\"objectStartPosition\":Vec" + objectStartPosition.ToString() + ","
			 + "\"objectStartRotation\":Vec" + objectStartRotation.ToString() + ","
			 + "\"objectStartScale\":Vec" + objectStartScale.ToString() + ","
			 + "\"textObject_startText\":\"" + textObject_startText + "\","
			 + "\"imageObject_imageURL\":\"" + imageObject_imageURL + "\","
			 + "\"imageObject_Image\":\"" + imageObjectBase64 + "\","
			 + "\"videoObject_videoURL\":\"" + videoObject_videoURL + "\","
			 + "\"modelObject_3DModel\":\"" + modelBase64 + "\""
			 + "}";
	}

	public static async Task<ARTrackedImageInfos> FromJson(string json) {
		Vector3 StringToVector3(string str) {
			str = str.Trim('"');
			str = str.Replace("Vec", "");
			string[] parts = str.Substring(1, str.Length - 2).Split(',');
			return new Vector3(
				 float.Parse(parts[0]),
				 float.Parse(parts[1]),
				 float.Parse(parts[2])
			);
		}
		Vector2 StringToVector2(string str) {
			str = str.Trim('"');
			str = str.Replace("Vec", "");
			string[] parts = str.Substring(1, str.Length - 2).Split(',');
			return new Vector2(
				 float.Parse(parts[0]),
				 float.Parse(parts[1])
			);
		}
		ARTrackedImageInfos infos = new ARTrackedImageInfos();
		string jsonStr = json.Substring(1, json.Length - 2);
		string[] jsonParts = jsonStr.Split(',');
		// Merge all JSON parts by avoiding splitting nested JSON objects or arrays
		List<string> mergedParts = new List<string>();
		string currentPart = "";
		int openBrackets = 0;
		foreach (string part in jsonParts) {
			currentPart += part;
			openBrackets += Regex.Matches(part, "{").Count;
			openBrackets -= Regex.Matches(part, "}").Count;
			if (openBrackets == 0) {
				mergedParts.Add(currentPart);
				currentPart = "";
			} else {
				currentPart += ",";
			}
		}
		jsonParts = mergedParts.ToArray();
		// Merge all JSON parts by also avoiding splitting Vectors (of the form '"key":"Vec(x,y,z)"')
		List<string> mergedParts2 = new List<string>();
		string currentPart2 = "";
		int openBrackets2 = 0;
		foreach (string part in jsonParts) {
			currentPart2 += part;
			openBrackets2 += Regex.Matches(part, "\"Vec[(]").Count;
			openBrackets2 -= Regex.Matches(part, " [)]\"").Count;
			if (openBrackets2 == 0) {
				mergedParts2.Add(currentPart2);
				currentPart2 = "";
			} else {
				currentPart2 += ",";
			}

		}
		jsonParts = mergedParts2.ToArray();
		// Parse each JSON part
		foreach (string part in jsonParts) {
			if (part == "") continue;
			int splitIndex = part.IndexOf(":");
			string[] keyValue = new string[] { part.Substring(0, splitIndex), part.Substring(splitIndex + 1) };
			string key = keyValue[0].Trim('"');
			string value = keyValue[1].Trim('"');
			//Debug.Log("> > | " + key + " : " + value);
			switch (key) {
				case "name":
					infos.name = value;
					break;
				case "image":
					byte[] imageBytes = System.Convert.FromBase64String(value);
					infos.image = new Texture2D(2, 2);
					infos.image.LoadImage(imageBytes);
					break;
				case "ARObject":
					infos.ARObject = null;
					break;
				case "type":
					infos.type = (ObjectType) System.Enum.Parse(typeof(ObjectType), value);
					break;
				case "markerSize":
					infos.markerSize = StringToVector2(value);
					break;
				case "fullScreen":
					infos.fullScreen = bool.Parse(value);
					break;
				case "objectStartPosition":
					infos.objectStartPosition = StringToVector3(value);
					break;
				case "objectStartRotation":
					infos.objectStartRotation = StringToVector3(value);
					break;
				case "objectStartScale":
					infos.objectStartScale = StringToVector3(value);
					break;
				case "textObject_startText":
					infos.textObject_startText = value;
					break;
				case "imageObject_imageURL":
					infos.imageObject_imageURL = value;
					break;
				case "videoObject_videoURL":
					infos.videoObject_videoURL = value;
					break;
				case "modelObject_3DModel":
					byte[] modelBytes = System.Convert.FromBase64String(value);
					if (modelBytes.Length > 0) infos.modelObject_3DModel = await SerializationUtils.Deserialize3DModelAsync(modelBytes);
					else infos.modelObject_3DModel = null;
					break;
			}
		}
		return infos;
	}

}