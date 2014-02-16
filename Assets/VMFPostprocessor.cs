﻿using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class VMFPostprocessor : AssetPostprocessor
{
	private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromPath)
	{
		importedAssets = importedAssets.Where(x => Path.GetExtension(x) == ".vmf").ToArray();
		deletedAssets = deletedAssets.Where(x => Path.GetExtension(x) == ".vmf").ToArray();
		movedAssets = movedAssets.Where(x => Path.GetExtension(x) == ".vmf").ToArray();
		movedFromPath = movedFromPath.Where(x => Path.GetExtension(x) == ".vmf").ToArray();

		foreach (string asset in importedAssets)
		{
			Debug.Log("VMF imported: " + asset);

			// We can get rid of the file once we have the contents imported.

			AssetDatabase.DeleteAsset(asset);
		}
		
		foreach (string asset in deletedAssets)
		{
			Debug.Log("VMF deleted: " + asset);
		}

		for (int i = 0; i < movedAssets.Length; i++)
		{
			Debug.Log("VMF moved from " + movedFromPath[i] + " to " + movedAssets[i]);
		}
	}
}