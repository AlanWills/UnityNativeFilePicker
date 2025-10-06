#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace CelesteEditor.BuildSystem.iOSPostProcess
{
    public class FixUpNativeFilePickerDependencies
    {
        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target == BuildTarget.iOS)
            {
                string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
                PBXProject proj = new PBXProject();
                proj.ReadFromFile(projPath);

#if UNITY_2019_3_OR_NEWER
                string targetGuid = proj.GetUnityFrameworkTargetGuid();
#else
				string targetGuid = proj.TargetGuidByName("Unity-iPhone");
#endif

                proj.AddFrameworkToProject(targetGuid, "UniformTypeIdentifiers.framework", true);
                proj.AddFrameworkToProject(targetGuid, "CoreServices.framework", false);
                proj.AddFrameworkToProject(targetGuid, "MobileCoreServices.framework", true);

                proj.WriteToFile(projPath);
            }
        }
    }
}
#endif