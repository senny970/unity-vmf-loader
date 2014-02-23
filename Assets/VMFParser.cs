﻿using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityVMFLoader
{
	public static class VMFParser
	{
		private static readonly Queue<ParserTask> taskQueue;
		private static readonly List<ParserTask> doneTasks;

		static VMFParser()
		{
			taskQueue = new Queue<ParserTask>();
			doneTasks = new List<ParserTask>();
		}

		public static void AddTask<T>() where T : ParserTask
		{
			taskQueue.Enqueue(Activator.CreateInstance<T>());
		}

		public static ParserTask GetTask<T>() where T : ParserTask
		{
			return doneTasks.FirstOrDefault(task => task.GetType() == typeof (T));
		}

		public static bool TaskDone<T>() where T : ParserTask
		{
			return TaskDone(typeof (T));
		}

		public static bool TaskDone(Type type)
		{
			return doneTasks.Any(task => task.GetType() == type);
		}

		public static Nodes.Node Parse(string path)
		{
			var lines = File.ReadAllLines(path).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

			var root = new Nodes.Node();
			var active = root;

			foreach (var line in lines)
			{
				var firstCharacter = line[0];

				switch (firstCharacter)
				{
					case '{':

						break;

					case '}':

						// End of current node.

						active = active.Parent ?? root;

						break;

					case '"':

						// A key-value.

						var keyEnd = line.IndexOf('"', 1);
						var key = line.Substring(1, keyEnd - 1);

						var valueStart = line.IndexOf('"', keyEnd + 1);
						var value = line.Substring(valueStart, line.Length - valueStart).Trim('"');

						active.Parse(key, value);

						break;

					default:

						// Start of a new node.

						var type = Type.GetType("UnityVMFLoader.Nodes." + char.ToUpper(line[0]) + line.Substring(1)) ?? typeof(Nodes.Node);

						var parent = active;

						active = (Nodes.Node) Activator.CreateInstance(type);

						active.Key = line;
						active.Parent = parent;

						break;
				}
			}

			// Create groups from the parsed tree.

			var groups = new Dictionary<Nodes.Group, GameObject>();

			foreach (var group in root.Children.OfType<Nodes.World>().First().Children.OfType<Nodes.Group>())
			{
				groups[group] = new GameObject("Group " + group.Identifier);
			}

			if (Settings.ImportBrushes)
			{
				// Create solids from the parsed tree.

				IEnumerable<Nodes.Solid> solids = null;

				if (Settings.ImportWorldBrushes)
				{
					solids = root.Children.OfType<Nodes.World>().First().Children.OfType<Nodes.Solid>();
				}

				if (Settings.ImportDetailBrushes)
				{
					foreach (var entity in root.Children.OfType<Nodes.Entity>())
					{
						var entities = entity.Children.OfType<Nodes.Solid>();

						solids = solids == null ? entities : solids.Concat(entities);
					}
				}

				if (solids != null)
				{
					foreach (var solid in solids)
					{
						GameObject gameObject = new GameObject("Solid " + solid.Identifier);

						gameObject.AddComponent<UnityEngine.MeshRenderer>();
						gameObject.AddComponent<MeshFilter>();

						gameObject.GetComponent<MeshFilter>().sharedMesh = (Mesh) solid;

						// Assign the placeholder material.

						var material = (Material) AssetDatabase.LoadAssetAtPath("Assets/dev_measuregeneric01b.mat", typeof(Material));

						gameObject.GetComponent<UnityEngine.MeshRenderer>().material = material;

						// The vertices of the mesh are in world coordinates so we'll need to center them.

						var mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;

						var center = mesh.vertices.Average();

						var vertices = mesh.vertices;

						for (var vertex = 0; vertex < vertices.Count(); vertex++)
						{
							vertices[vertex] -= center;
						}

						mesh.vertices = vertices;

						mesh.RecalculateBounds();

						// And move the object itself to those world coordinates.

						gameObject.transform.position = center;

						// In order to make lightmap baking work, make object static.

						gameObject.isStatic = true;

						// Add a MeshCollider.

						var collider = gameObject.AddComponent<MeshCollider>();

						collider.convex = true;

						// If the solid is in a group, move it there.

						var editor = solid.Parent.Children.OfType<Nodes.Editor>().FirstOrDefault();

						if (editor != null)
						{
							var pair = groups.FirstOrDefault(x => x.Key.Identifier == editor.GroupIdentifier);

							if (pair.Value != null)
							{
								gameObject.transform.parent = pair.Value.transform;
							}
						}
					}
				}
			}

			// Create lights.

			if (Settings.ImportLights)
			{
				var lights = root.Children.OfType<Nodes.Entity>().Where(entity => entity.ClassName.StartsWith("light")).ToList();

				const float sourceToUnityBrightnessDivisor = 200;

				foreach (var light in lights)
				{
					var lightProperties = Regex.Replace(light["_light"], @"\s+", " ").Split(' ');

					var color = lightProperties.Take(3).Select(v => float.Parse(v)/255f).ToArray();
					var brightness = float.Parse(lightProperties[3])/sourceToUnityBrightnessDivisor;

					var lightObject = new GameObject("Light " + light.Identifier);

					lightObject.transform.position = light.Origin;
					lightObject.transform.rotation = light.Angles;

					var lightComponent = lightObject.AddComponent<Light>();

					lightComponent.intensity = brightness;
					lightComponent.color = new Color(color[0], color[1], color[2]);
					lightComponent.range = 25.0f;

					switch (light.ClassName)
					{
						case "light":

							lightComponent.type = LightType.Point;

							break;

						case "light_spot":

							lightComponent.type = LightType.Spot;
							lightComponent.spotAngle = int.Parse(light["_cone"]);

							break;
					}
				}
			}

			// Destroy the GameObjects of groups with a single child or none.

			var groupsCopy = groups.ToDictionary(entry => entry.Key, entry => entry.Value);

			foreach (var pair in groupsCopy.Where(x => x.Value.GetComponentsInChildren<Transform>().Length < 3))
			{
				var child = pair.Key.Children.FirstOrDefault();

				if (child != null)
				{
					child.Parent = pair.Key.Parent;
				}

				UnityEngine.Object.DestroyImmediate(pair.Value);

				groups.Remove(pair.Key);
			}

			return root;
		}
	}
}
