using System.IO;
using UnityEditor;
using UnityEngine;
#if UNITY_IOS
using System.Collections.Generic;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

namespace NativeFilePickerNamespace
{
    [System.Serializable]
    public class Settings
    {
        private const string SAVE_PATH = "ProjectSettings/NativeFilePicker.json";

        public bool AutoSetupFrameworks = true;
        public bool AutoSetupiCloud = false;

        private static Settings m_instance = null;
        public static Settings Instance
        {
            get
            {
                if (m_instance == null)
                {
                    try
                    {
                        if (File.Exists(SAVE_PATH))
                            m_instance = JsonUtility.FromJson<Settings>(File.ReadAllText(SAVE_PATH));
                        else
                            m_instance = new Settings();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                        m_instance = new Settings();
                    }
                }

                return m_instance;
            }
        }

        public void Save()
        {
            File.WriteAllText(SAVE_PATH, JsonUtility.ToJson(this, true));
        }

        [SettingsProvider]
        public static SettingsProvider CreatePreferencesGUI()
        {
            return new SettingsProvider("Project/yasirkula/Native File Picker", SettingsScope.Project)
            {
                guiHandler = (searchContext) => PreferencesGUI(),
                keywords = new System.Collections.Generic.HashSet<string>() { "Native", "File", "Picker", "Android", "iOS" }
            };
        }

        public static void PreferencesGUI()
        {
            EditorGUI.BeginChangeCheck();

            Instance.AutoSetupFrameworks = EditorGUILayout.Toggle(
                new GUIContent("Auto Setup Frameworks", "Automatically adds necessary frameworks to the generated Xcode project"),
                Instance.AutoSetupFrameworks
            );

            Instance.AutoSetupiCloud = EditorGUILayout.Toggle(
                new GUIContent("Auto Setup iCloud", "Automatically enables iCloud capability of the generated Xcode project"),
                Instance.AutoSetupiCloud
            );

            if (EditorGUI.EndChangeCheck())
                Instance.Save();
        }
    }

    public class NativeFilePickerPostProcessBuild
    {
#if UNITY_IOS
#pragma warning disable 0162
        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
                return;

            // Add custom UTType declarations to Info.plist
            IReadOnlyList<NativeFilePickerCustomTypes.TypeHolder> customTypes = NativeFilePickerCustomTypes.GetCustomTypes();
            if (customTypes != null)
            {
                string plistPath = Path.Combine(buildPath, "Info.plist");

                PlistDocument plist = new PlistDocument();
                plist.ReadFromString(File.ReadAllText(plistPath));
                PlistElementDict rootDict = plist.root;

                for (int i = 0; i < customTypes.Count; i++)
                {
                    NativeFilePickerCustomTypes.TypeHolder customType = customTypes[i];
                    PlistElementArray customTypesArray = GetCustomTypesArray(rootDict, customType.isExported);

                    RemoveCustomTypeIfExists(customTypesArray, customType.identifier);

                    PlistElementDict customTypeDict = customTypesArray.AddDict();
                    customTypeDict.SetString("UTTypeIdentifier", customType.identifier);
                    customTypeDict.SetString("UTTypeDescription", customType.description);

                    PlistElementArray conformsTo = customTypeDict.CreateArray("UTTypeConformsTo");
                    foreach (string conf in customType.conformsTo)
                        conformsTo.AddString(conf);

                    PlistElementDict tagSpec = customTypeDict.CreateDict("UTTypeTagSpecification");
                    PlistElementArray tagExts = tagSpec.CreateArray("public.filename-extension");
                    foreach (string ext in customType.extensions)
                        tagExts.AddString(ext);
                }

                File.WriteAllText(plistPath, plist.WriteToString());
            }

            if (!Settings.Instance.AutoSetupFrameworks && !Settings.Instance.AutoSetupiCloud)
                return;

            string pbxPath = PBXProject.GetPBXProjectPath(buildPath);
            PBXProject proj = new PBXProject();
            proj.ReadFromFile(pbxPath);

            string targetGuid = proj.GetUnityFrameworkTargetGuid();

            if (Settings.Instance.AutoSetupFrameworks)
            {
                // ✅ Modern frameworks only (no MobileCoreServices)
                proj.AddFrameworkToProject(targetGuid, "CoreServices.framework", false);
                proj.AddFrameworkToProject(targetGuid, "UniformTypeIdentifiers.framework", true);
                proj.AddFrameworkToProject(targetGuid, "CloudKit.framework", true);
            }

            File.WriteAllText(pbxPath, proj.WriteToString());

            if (Settings.Instance.AutoSetupiCloud)
            {
                ProjectCapabilityManager manager = new ProjectCapabilityManager(pbxPath, "iCloud.entitlements", "Unity-iPhone");
                manager.AddiCloud(false, true, false, true, null);
                manager.WriteToFile();
            }
        }

        // PRODUCT_BUNDLE_IDENTIFIER safeguard
        [PostProcessBuild(99)]
        public static void OnPostprocessBuild2(BuildTarget target, string buildPath)
        {
            if (!Settings.Instance.AutoSetupFrameworks && !Settings.Instance.AutoSetupiCloud)
                return;

            if (target != BuildTarget.iOS)
                return;

            string pbxPath = PBXProject.GetPBXProjectPath(buildPath);
            PBXProject proj = new PBXProject();
            proj.ReadFromFile(pbxPath);

            string targetGuid = proj.GetUnityFrameworkTargetGuid();
            if (string.IsNullOrEmpty(proj.GetBuildPropertyForAnyConfig(targetGuid, "PRODUCT_BUNDLE_IDENTIFIER")))
                proj.AddBuildProperty(targetGuid, "PRODUCT_BUNDLE_IDENTIFIER", PlayerSettings.applicationIdentifier);

            File.WriteAllText(pbxPath, proj.WriteToString());
        }

        private static PlistElementArray GetCustomTypesArray(PlistElementDict rootDict, bool isExported)
        {
            string key = isExported ? "UTExportedTypeDeclarations" : "UTImportedTypeDeclarations";
            PlistElementArray result = rootDict[key] as PlistElementArray;
            if (result == null)
                result = rootDict.CreateArray(key);

            return result;
        }

        private static void RemoveCustomTypeIfExists(PlistElementArray array, string UTI)
        {
            List<PlistElement> values = array.values;
            if (values == null)
                return;

            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (values[i] is PlistElementDict dict &&
                    dict["UTTypeIdentifier"] is PlistElementString id &&
                    id.value == UTI)
                {
                    values.RemoveAt(i);
                }
            }
        }
#pragma warning restore 0162
#endif
    }
}
