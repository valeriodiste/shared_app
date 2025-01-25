using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;
using GLTFast;
#if UNITY_EDITOR
using GLTFast.Export;
#endif

public static class SerializationUtils {

	// Serialize Texture2D to byte array
	public static byte[] SerializeTexture(Texture2D texture) {
		if (texture == null)
			return null;
		return texture.EncodeToPNG();
	}

	// Deserialize byte array to Texture2D
	public static Texture2D DeserializeTexture(byte[] textureBytes) {
		if (textureBytes == null || textureBytes.Length == 0)
			return null;
		Texture2D texture = new Texture2D(2, 2);
		texture.LoadImage(textureBytes);
		return texture;
	}

	// Serialize VideoClip to byte array
	public static byte[] SerializeVideoClip(VideoClip videoClip) {
		if (videoClip == null)
			return null;
		return System.Text.Encoding.UTF8.GetBytes(videoClip.originalPath);
	}

	// Deserialize VideoClip from byte array
	public static VideoClip DeserializeVideoClip(byte[] videoBytes) {
		if (videoBytes == null || videoBytes.Length == 0)
			return null;
		string videoPath = System.Text.Encoding.UTF8.GetString(videoBytes);
		return Resources.Load<VideoClip>(videoPath);
	}

	// Serialize 3D model to GLB byte array
	public static async Task<byte[]> Serialize3DModelAsync(GameObject model) {
		if (model == null)
			return null;
		///*
#if UNITY_EDITOR
		if (model == null)
			return null;

		var exportSettings = new ExportSettings {
			Format = GltfFormat.Binary // GLB file
		};

		var export = new GameObjectExport(exportSettings);

		// Use the scene's world to local matrix to center the model
		export.AddScene(
			 new[] { model },
			 model.transform.worldToLocalMatrix,
			 "Serialized Scene"
		);

		using (MemoryStream stream = new MemoryStream()) {
			var success = await export.SaveToStreamAndDispose(stream);

			if (!success) {
				Debug.LogError("Failed to serialize 3D model");
				return null;
			}

			return stream.ToArray();
		}
#endif
		//*/
		Debug.Log("WARNING: Serialization was disabled, uncomment the lines above to enable it");
		return null;
	}

	public static async Task<GameObject> Deserialize3DModelAsync(byte[] modelBytes) {
		var gltf = new GltfImport();
		bool success = await gltf.LoadGltfBinary(modelBytes);
		if (success) {
			var transform = new GameObject("Deserialized Scene").transform;
			success = await gltf.InstantiateMainSceneAsync(transform);
			if (success) {
				return transform.gameObject;
			} else {
				Debug.LogError("Failed to deserialize 3D model");
				return null;
			}
		}
		Debug.LogError("Failed to load GLB file");
		return null;
	}
}