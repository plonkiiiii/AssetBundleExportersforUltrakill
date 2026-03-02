using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using System.Collections.Generic;

public class RawBundleExporter : EditorWindow
{
	[SerializeField]
	private string labelName, groupName, catName;


	[MenuItem("Window/Raw Asset Bundle Exporter")]
	public static void OnWindow()
	{
		EditorWindow wnd = GetWindow<RawBundleExporter>();
		wnd.titleContent = new GUIContent("Raw Bundle Exporter");
	}

	[SerializeField]
	public string exportPath = "";
	TextField labelNameField, labelGroupField, catalogValueField;
	TextField exportPathField;

	public void CreateGUI()
	{
		VisualElement topSpace = new VisualElement();
		topSpace.style.height = new StyleLength(10);
		rootVisualElement.Add(topSpace);

		Label infoLabel = new Label("Separate bundle names with commas");
		infoLabel.style.whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
		rootVisualElement.Add(infoLabel);

		labelNameField = new TextField("Bundles to build");
		labelNameField.value = labelName;
		labelNameField.RegisterValueChangedCallback(e => labelName = e.newValue);
		rootVisualElement.Add(labelNameField);

        VisualElement space = new VisualElement();
		space.style.flexGrow = 1;
		rootVisualElement.Add(space);

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

		VisualElement space2 = new VisualElement();
		space2.style.height = new StyleLength(10);
		rootVisualElement.Add(space2);

		Button buildAndCopyButtonSlow = new Button();
		buildAndCopyButtonSlow.text = "Build raw asset bundles";
		buildAndCopyButtonSlow.clicked += BuildBundle;
		rootVisualElement.Add(buildAndCopyButtonSlow);
	}

    private string[] builtInGroupNames = new string[]
	{
		"Built In Data",
		"Default Group",
		"Assets",
		"Other",
		"Music"
	};

    private T GetOrCreateSchemaFromTemplate<T>(AddressableAssetGroup group, T template, bool postEvents = true) where T : AddressableAssetGroupSchema
    {
        T schema = (T)group.GetSchema(typeof(T));
        if (schema == null)
            schema = (T)group.AddSchema(template, postEvents);

        return schema;
    }

    private void Build(string group)
    {
		var settings = AddressableAssetSettingsDefaultObject.Settings;
		if (settings == null)
		{
			EditorUtility.DisplayDialog("Error", "", "Ok");
			return;
		}

        settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", "Library/com.unity.addressables/aa/Windows");
        settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", @catName);
		settings.MonoScriptBundleCustomNaming = group;

        if (settings.ActivePlayerDataBuilderIndex < 0 || settings.DataBuilders.Count == 0)
        {
            var builder = ScriptableObject.CreateInstance<BuildScriptPackedMode>();
            string path = "Assets/AddressableAssetsData/DataBuilders/BuildScriptPackedMode.asset";
            AssetDatabase.CreateAsset(builder, path);
            settings.DataBuilders.Add(builder);
            settings.ActivePlayerDataBuilderIndex = settings.DataBuilders.Count - 1;
			//settings.RemoteCatalogLoadPath.SetVariableByName(settings);// @"{UnityEngine.AddressableAssets.Addressables.RuntimePath}\\\\StandaloneWindows64\\\\";
		}

        foreach (var sg in settings.groups)
        {
            if (sg.ReadOnly || builtInGroupNames.Contains(sg.name)) continue;

            var groupSchema = sg.GetSchema<BundledAssetGroupSchema>();
            if (groupSchema == null)
                groupSchema = sg.AddSchema<BundledAssetGroupSchema>();


            groupSchema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
            groupSchema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");

			groupSchema.IncludeAddressInCatalog = true;

			groupSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
            groupSchema.InternalIdNamingMode = BundledAssetGroupSchema.AssetNamingMode.GUID;
            groupSchema.UseAssetBundleCrcForCachedBundles = false;
            groupSchema.UseAssetBundleCrc = false;

            groupSchema.IncludeInBuild = (sg.Name == group);
            EditorUtility.SetDirty(sg);
        }
        Debug.Log(settings.RemoteCatalogLoadPath.GetValue(settings));
        AddressableAssetSettings.BuildPlayerContent(out var result);
        if (result == null)
        {
            EditorUtility.DisplayDialog("Error", "Failed to build Addressables for group: " + group + ". Check console for details.", "Ok");
			return;
        }
		else
		{

			EditorUtility.DisplayDialog(result.OutputPath, result.ContentStateFilePath, result.OutputPath);
			string pathLocal = result.OutputPath.Replace("settings.json", "").Replace("catalog.json", "");
			pathLocal.Remove(pathLocal.Length - 1);
            CopyDirectory(pathLocal.Replace("\\", "/"), exportPath, true);
		}

        //EditorUtility.DisplayDialog("Build", group + " AddressableGroup has successfully built. \n" + result.OutputPath, "Ok");
    }

    static void CopyDirectory(string sourceDir, string destDir, bool overwrite = false)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

		Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite);
        }

        /*foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir, overwrite);
        }*/
    }

    private void BuildBundle()
	{
		if (labelName != "" && labelName != " ")
		{

			string[] bundleNames = labelName.Split(',');
			string[] allBundles = AssetDatabase.GetAllAssetBundleNames();

			string notFound = string.Join(", ", bundleNames.Where(b => !allBundles.Contains(b)));
			if (!string.IsNullOrEmpty(notFound))
			{
				if (!EditorUtility.DisplayDialog("Warning", $"Following bundle labels are not found\n{notFound}", "Continue", "Cancel"))
					return;
			}

			AssetBundleBuild[] builds = bundleNames.Where(b => allBundles.Contains(b)).Select((name) => new AssetBundleBuild() { assetBundleName = name, assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(name) }).ToArray();

			BuildPipeline.BuildAssetBundles(exportPath, builds, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
		}
	}
}
