// Command-line and menu build entry points.
//   Unity -batchmode -quit -executeMethod CommunityWorld.EditorTools.BuildScript.BuildWebGL
// The game builds itself from code at start-up, so the build only needs an empty scene.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CommunityWorld.EditorTools
{
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        private static string[] EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            return new string[] { ScenePath };
        }

        private static string Arg(string name, string def)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }

        private static void CommonSettings()
        {
            PlayerSettings.companyName = "Community World";
            PlayerSettings.productName = "Community World";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.colorSpace = ColorSpace.Gamma;   // flat-color art: no linear conversion, colors match the design tokens
        }

        [MenuItem("Community World/Build WebGL")]
        public static void BuildWebGL()
        {
            CommonSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;   // GitHub Pages cannot serve .br/.gz with the right headers
            PlayerSettings.WebGL.template = "PROJECT:CommunityWorld";
            PlayerSettings.WebGL.dataCaching = true;
            string outPath = Arg("-cwOutput", "build/WebGL");
            BuildPlayerOptions opts = new BuildPlayerOptions();
            opts.scenes = EnsureScene();
            opts.locationPathName = outPath;
            opts.target = BuildTarget.WebGL;
            opts.options = BuildOptions.None;
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("Build result: " + report.summary.result + " -> " + outPath);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Community World/Build Android APK")]
        public static void BuildAndroid()
        {
            CommonSettings();
            BuildPlayerOptions opts = new BuildPlayerOptions();
            opts.scenes = EnsureScene();
            opts.locationPathName = Arg("-cwOutput", "build/Android/community-world.apk");
            opts.target = BuildTarget.Android;
            opts.options = BuildOptions.None;
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
