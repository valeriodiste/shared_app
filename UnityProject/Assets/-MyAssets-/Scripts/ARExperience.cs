using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

// Class for a single AR experience
[System.Serializable]
public class ARExperience {

	// General info
	public string experienceName;
	public string experienceCreator;
	public bool isPublicExperience;
	public string experienceCode;		// Only available if the experience is public

	// Experience data
	public List<ARTrackedImageInfos> ARObjectsInfos;

	// Constructor
	public ARExperience(string name, string creator, bool isPublic, string code = "") {
		experienceName = name;
		experienceCreator = creator;
		isPublicExperience = isPublic;
		experienceCode = code;
		ARObjectsInfos = new List<ARTrackedImageInfos>();
	}
	public ARExperience(string name, string creator, bool isPublic, string code, List<ARTrackedImageInfos> infos) {
		experienceName = name;
		experienceCreator = creator;
		isPublicExperience = isPublic;
		experienceCode = code;
		ARObjectsInfos = infos;
	}

	// Add an AR object to the experience
	public void AddARObject(ARTrackedImageInfos infos) {
		if (ARObjectsInfos == null) ARObjectsInfos = new List<ARTrackedImageInfos>();
		ARObjectsInfos.Add(infos);
	}
	
	public async Task<string> ToJson() {
		string json = "{";
		json += "\"experienceName\":\"" + experienceName + "\",";
		json += "\"experienceCreator\":\"" + experienceCreator + "\",";
		json += "\"isPublicExperience\":" + isPublicExperience.ToString().ToLower() + ",";
		json += "\"experienceCode\":\"" + experienceCode + "\",";
		json += "\"ARObjectsInfos\":[";
		if (ARObjectsInfos != null && ARObjectsInfos.Count > 0) {
			foreach (ARTrackedImageInfos infos in ARObjectsInfos) {
				if (infos == null) continue;
				json += await infos.ToJson() + ",";
			}
			json = json.Remove(json.Length - 1);
		}
		json += "]";
		json += "}";
		return json;
	}

	public static async Task<ARExperience> FromJson(string json) {
		bool printDebug = false;
		ARExperience experience = new ARExperience("", "", false);
		if (json == "") return experience;
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
		// Parse each JSON part
		foreach (string part in jsonParts) {
			if (printDebug) Debug.Log(part);
			int splitIndex = part.IndexOf(":");
			string[] keyValue = new string[] { part.Substring(0, splitIndex), part.Substring(splitIndex + 1) };
			string key = keyValue[0].Trim('"');
			string value = keyValue[1];
			if (value.StartsWith("\"")) value = value.Trim('"');
			switch (key) {
				case "experienceName":
					experience.experienceName = value;
					break;
				case "experienceCreator":
					experience.experienceCreator = value;
					break;
				case "isPublicExperience":
					experience.isPublicExperience = bool.Parse(value);
					break;
				case "experienceCode":
					experience.experienceCode = value;
					break;
				case "ARObjectsInfos":
					// Value is a list of other JSON objects
					if (value == "[]") break;
					value = value.Substring(1, value.Length - 2);
					string[] infos = value.Split('{');
					if (printDebug) Debug.Log("> " + infos.Length + ": [\n\t" + string.Join(",\n\t", infos) + "\n]");
					foreach (string info in infos) {
						if (info == "") continue;
						string finalInfoJSONString = "{" + info.Replace("},", "}");
						if (printDebug) Debug.Log("> > " + finalInfoJSONString);
						if (info.Length > 0) {
							ARTrackedImageInfos trackedImageInfos = await ARTrackedImageInfos.FromJson(finalInfoJSONString);
							experience.AddARObject(trackedImageInfos);
						}
					}
					break;
			}
		}
		return experience;

	}


}