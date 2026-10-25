using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine.AddressableAssets;
using System.Linq;
using UnityEditor.AddressableAssets.Build;
using System.IO.Compression;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Net;

public static class AddressableAssetGroupExtensions
{
	public static T GetOrCreateSchema<T>(this AddressableAssetGroup group, bool postEvents = true) where T : AddressableAssetGroupSchema
	{
		T schema = group.GetSchema<T>();
		if (schema == null)
			schema = group.AddSchema<T>(postEvents);

		return schema;
	}

	public static AddressableAssetGroupSchema GetOrCloneSchema(this AddressableAssetGroup group, AddressableAssetGroupSchema template, bool postEvents = true)
	{
		AddressableAssetGroupSchema schema = group.GetSchema(template.GetType());
		if (schema == null)
			schema = group.AddSchema(template, postEvents);

		return schema;
	}
}
namespace Assets.Editor.Rexporter
{
	public class RawExporter : EditorWindow
	{
		[SerializeField]
		private string groupName;
		[SerializeField]
		private string remoteLoadPath;

		[MenuItem("Window/Raw Exporter")]
		public static void OnWindow()
		{
			EditorWindow wnd = GetWindow<RawExporter>();
			wnd.titleContent = new GUIContent("Raw Exporter");
		}

		public static string[] builtInGroupNames = new string[]
		{
		"Built In Data",
		"Default Group",
		"Assets",
		"Other",
		"Music"
		};

		int selectedPreset = -1;
		[SerializeField]
		public string exportPath = "";
		[SerializeField]
		public string presetName = "";
		TextField groupNameField;
		TextField remoteLoadField;
		TextField exportPathField;
		TextField presetNameField;

		RawExporterPreset[] allPresets;

		public void RefreshPresets()
		{
			allPresets = Resources.FindObjectsOfTypeAll<RawExporterPreset>();
		}

		public void CreateGUI()
		{
			if (allPresets == null || allPresets.Length <= 0)
				RefreshPresets();

			VisualElement topSpace = new VisualElement();
			topSpace.style.height = new StyleLength(10);
			rootVisualElement.Add(topSpace);

			Label infoLabel = new Label("Separate group names with commas");
			infoLabel.style.whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
			rootVisualElement.Add(infoLabel);

			groupNameField = new TextField("Groups to build");
			groupNameField.value = groupName;
			groupNameField.RegisterValueChangedCallback(e => groupName = e.newValue);
			rootVisualElement.Add(groupNameField);

			remoteLoadField = new TextField("Remote load path");
			remoteLoadField.value = remoteLoadPath;
			remoteLoadField.RegisterValueChangedCallback(e => remoteLoadPath = e.newValue);
			rootVisualElement.Add(remoteLoadField);

			exportPathField = new TextField("Path to export");
			exportPathField.RegisterValueChangedCallback(e => exportPath = e.newValue);
			exportPathField.SetValueWithoutNotify(exportPath);
			rootVisualElement.Add(exportPathField);

			Button openFolder = new Button();
			openFolder.text = "Open export folder";
			openFolder.clicked += () =>
			{
				string destinationFolder = exportPath;
				if (!Directory.Exists(destinationFolder))
				{
					destinationFolder = Application.dataPath;
				}

				exportPath = EditorUtility.SaveFolderPanel("Open export folder", destinationFolder, "Levels");
				exportPathField.value = exportPath;
			};
			rootVisualElement.Add(openFolder);

			presetNameField = new TextField("Preset Name");
			presetNameField.RegisterValueChangedCallback(e => presetName = e.newValue);
			presetNameField.SetValueWithoutNotify(presetName);
			rootVisualElement.Add(presetNameField);

			Button saveButton = new Button();
			saveButton.text = "Save as preset";
			saveButton.clicked += () =>
			{
				RefreshPresets();

				bool flag = true;
				foreach (RawExporterPreset preset in allPresets)
				{
					if (preset == null) continue;
					if (preset.name == presetName)
					{
						if (!EditorUtility.DisplayDialog("Save preset", "Overwrite already saved?", "Yes", "No"))
							return;

						preset.exportPath = exportPath;
						preset.groupName = groupName;
						preset.remoteLoadPath = remoteLoadPath;
						RefreshPresets();
						flag = false;
					}
				}
				if (flag)
				{
					if (!EditorUtility.DisplayDialog("Save preset", "Are you sure?", "Yes", "No"))
						return;

					if (!AssetDatabase.IsValidFolder("Assets/RawExporterPresets")) AssetDatabase.CreateFolder("Assets", "RawExporterPresets");

					RawExporterPreset preset = new();
					preset.name = presetName;
					preset.remoteLoadPath = remoteLoadPath;
					preset.groupName = groupName;
					preset.exportPath = exportPath;
					AssetDatabase.CreateAsset(preset, "Assets/RawExporterPresets/" + preset.name + ".asset");
					RefreshPresets();
				}
			};
			rootVisualElement.Add(saveButton);

			VisualElement space2 = new VisualElement();
			space2.style.height = new StyleLength(25);
			rootVisualElement.Add(space2);

			Button loadButton = new Button();
			loadButton.text = "Load preset";
			loadButton.clicked += () =>
			{
				RefreshPresets();
				List<Button> newButtons = new();
                Button loadButton3 = new Button();
                loadButton3.text = "Cancel";
                loadButton3.clicked += () =>
                {
                    foreach (Button b in newButtons)
                    {
                        loadButton.Remove(b);
                    }
                    loadButton.Remove(loadButton3);
                };

                foreach (RawExporterPreset preset in allPresets)
				{
                    Button loadButton2 = new Button();
                    loadButton2.text = "Load "+preset.name;
                    loadButton2.clicked += () =>
                    {
                        if (!EditorUtility.DisplayDialog("Load Preset", "Are you sure?", "Yes", "No"))
                            return;
                        foreach (Button b in newButtons)
                        {
                            loadButton.Remove(b);
                        }
                        loadButton.Remove(loadButton3);
						presetName = preset.name;
						groupName = preset.groupName;
						remoteLoadPath = preset.remoteLoadPath;
						exportPath = preset.exportPath;
						presetNameField.SetValueWithoutNotify(presetName);
						groupNameField.SetValueWithoutNotify(groupName);
						remoteLoadField.SetValueWithoutNotify(remoteLoadPath);
						exportPathField.SetValueWithoutNotify(exportPath);
                    };
					newButtons.Add(loadButton2);
					loadButton.Add(loadButton2);
				}

                loadButton.Add(loadButton3);
            };
			rootVisualElement.Add(loadButton);

            Button refreshButton = new Button();
            refreshButton.text = "Refresh presets";
            refreshButton.clicked += () =>
            {
				RefreshPresets();
            };
            rootVisualElement.Add(refreshButton);

            VisualElement space3 = new VisualElement();
			space3.style.height = new StyleLength(25);
			rootVisualElement.Add(space3);

			VisualElement space4 = new VisualElement();
			space4.style.height = new StyleLength(25);
			//rootVisualElement.Add(space4);

			Button clearAllCacheButton = new Button();
			clearAllCacheButton.text = "Clear all cache";
			clearAllCacheButton.clicked += () =>
			{
				if (!EditorUtility.DisplayDialog("Clear build cache", "Delete all ultrakill bundle cache?", "Yes", "No"))
					return;

				AddressableAssetSettings.CleanPlayerContent(null);
				BuildCache.PurgeCache(false);
			};

			rootVisualElement.Add(clearAllCacheButton);


			VisualElement space = new VisualElement();
			space.style.flexGrow = 1;
			rootVisualElement.Add(space);

			Label infoText = new Label("First builds after cache clears will take significantly more time");
			infoText.style.whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
			rootVisualElement.Add(infoText);

			Button buildAndCopyButtonSlow = new Button();
			buildAndCopyButtonSlow.text = "Build raw bundles";
			buildAndCopyButtonSlow.clicked += BuildLevelCatalog;
			rootVisualElement.Add(buildAndCopyButtonSlow);
		}

		private void BuildLevelCatalog()
		{
			AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
			string[] groupNames = groupName.Split(',');

			foreach (string g in groupNames)
				if (settings.FindGroup(g) == null)
				{
					EditorUtility.DisplayDialog("Error", $"Could not build bundle because given addressable group name {(string.IsNullOrEmpty(g) ? "<empty>" : g)} does not exist", "Ok");
					return;
				}

			foreach (string g in groupNames)
				if (builtInGroupNames.Contains(g))
				{
					EditorUtility.DisplayDialog("Error", $"{g} cannot be used for the build since it collides with an internal addressable group names", "Ok");
					return;
				}

			if (!Directory.Exists(exportPath))
			{
				EditorUtility.DisplayDialog("Error", $"Could not find export folder at {(string.IsNullOrEmpty(exportPath) ? "<empty>" : exportPath)}", "Ok");
				return;
			}

			settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", exportPath);
			settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", remoteLoadPath);
			settings.MonoScriptBundleCustomNaming = groupName;

			BundledAssetGroupSchema defSchema = settings.DefaultGroup.GetOrCreateSchema<BundledAssetGroupSchema>();
			defSchema.IncludeInBuild = true;
			defSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
			defSchema.BuildPath.SetVariableByName(settings, "Local.BuildPath");
			defSchema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
			defSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
			defSchema.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash;
			defSchema.UseAssetBundleCrc = false;

			foreach (var grp in settings.groups)
			{
				BundledAssetGroupSchema grpSchema = grp.GetSchema<BundledAssetGroupSchema>();
				if (grpSchema != null)
				{
					if (builtInGroupNames.Contains(grp.Name))
					{
						grpSchema.IncludeInBuild = true;
					}
					else
					{
						grpSchema.IncludeInBuild = false;
					}
				}
			}

			foreach (string g in groupNames)
			{
				AddressableAssetGroup group = settings.FindGroup(g);
				BundledAssetGroupSchema schema = group.GetOrCreateSchema<BundledAssetGroupSchema>();
				schema.IncludeInBuild = true;
				schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
				schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
			}

			int indexOfBuilder = -1;
			for (int i = 0; i < settings.DataBuilders.Count; i++)
				if (settings.DataBuilders[i] is BuildScriptPackedMode)
				{
					indexOfBuilder = i;
					break;
				}

			settings.ActivePlayerDataBuilderIndex = indexOfBuilder;
			AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

			if (result == null || !string.IsNullOrEmpty(result.Error))
			{
				EditorUtility.DisplayDialog("Error", "Encountered an error while building content. Please try to reopen the project. If the issue persists send error log", "Ok");
				return;
			}

			string tempBuildDir = exportPath;

			string monoPath = "";
			foreach (var bundle in result.AssetBundleBuildResults)
			{
				string realPath = bundle.FilePath.Substring(0, bundle.FilePath.Length - 40) + ".bundle";

				if (bundle.SourceAssetGroup.Name != groupName)
				{
					if (Path.GetFileName(bundle.FilePath).StartsWith(settings.MonoScriptBundleCustomNaming))
					{
						monoPath = realPath;
						File.Copy(monoPath, Path.Combine(tempBuildDir, Path.GetFileName(monoPath)), true);
						break;
					}

					continue;
				}

				//File.Copy(realPath, Path.Combine(tempBuildDir, Path.GetFileName(realPath)));
			}

			string sourcePath = @"{UnityEngine.AddressableAssets.Addressables.RuntimePath}\\StandaloneWindows64\\" + Path.GetFileName(monoPath);
			string destinationPath = remoteLoadPath + @"\\" + Path.GetFileName(monoPath);
			string catalog = File.ReadAllText(Path.Combine(result.OutputPath, "../", "catalog.json"));
			File.WriteAllText(Path.Combine(tempBuildDir, "catalog.json"), catalog.Replace(sourcePath, destinationPath));
		}
	}
}