using System;
using System.Diagnostics;
using System.IO;
using UnityEditor.Build.AssetBundle;
using UnityEditor.Build.Player;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;
using UnityEditor.Experimental.Build.Player;
using UnityEngine;

namespace UnityEditor.Build
{
    public class DebugBundleBuildWindow : EditorWindow
    {
        [Serializable]
        private struct Settings
        {
            public BuildTarget buildTarget;
            public BuildTargetGroup buildGroup;
            public CompressionType compressionType;
            public bool useBuildCache;
            public bool useExperimentalPipeline;
            public string outputPath;
        }

        [SerializeField]
        Settings m_Settings;

        SerializedObject m_SerializedObject;
        SerializedProperty m_TargetProp;
        SerializedProperty m_GroupProp;
        SerializedProperty m_CompressionProp;
        SerializedProperty m_CacheProp;
        SerializedProperty m_ExpProp;
        SerializedProperty m_OutputProp;

        // Add menu named "My Window" to the Window menu
        [MenuItem("Window/Build Pipeline/Debug Window")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            var window = GetWindow<DebugBundleBuildWindow>("Debug Build");
            window.m_Settings.buildTarget = EditorUserBuildSettings.activeBuildTarget;
            window.m_Settings.buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            window.m_Settings.useExperimentalPipeline = true;

            window.Show();
        }

        private void OnEnable()
        {
            m_SerializedObject = new SerializedObject(this);
            m_TargetProp = m_SerializedObject.FindProperty("m_Settings.buildTarget");
            m_GroupProp = m_SerializedObject.FindProperty("m_Settings.buildGroup");
            m_CompressionProp = m_SerializedObject.FindProperty("m_Settings.compressionType");
            m_CacheProp = m_SerializedObject.FindProperty("m_Settings.useBuildCache");
            m_ExpProp = m_SerializedObject.FindProperty("m_Settings.useExperimentalPipeline");
            m_OutputProp = m_SerializedObject.FindProperty("m_Settings.outputPath");
        }

        private void OnGUI()
        {
            m_SerializedObject.Update();
            
            EditorGUILayout.PropertyField(m_TargetProp);
            EditorGUILayout.PropertyField(m_GroupProp);
            EditorGUILayout.PropertyField(m_CompressionProp);
            EditorGUILayout.PropertyField(m_CacheProp);
            EditorGUILayout.PropertyField(m_ExpProp);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(m_OutputProp);
            if (GUILayout.Button("Pick", GUILayout.Width(50)))
                PickOutputFolder();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Purge Cache"))
                BuildCache.PurgeCache();
            if (GUILayout.Button("Purge Output"))
                PurgeOutputFolder();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Build Bundles"))
                BuildAssetBundles();
            EditorGUILayout.EndHorizontal();

            m_SerializedObject.ApplyModifiedProperties();
        }

        private void PickOutputFolder()
        {
            var folder = EditorUtility.SaveFolderPanel("Build output location", m_OutputProp.stringValue, "");
            if (!string.IsNullOrEmpty(folder) && BuildPathValidator.ValidOutputFolder(folder, true))
            {
                var relativeFolder = FileUtil.GetProjectRelativePath(folder);
                m_OutputProp.stringValue = string.IsNullOrEmpty(relativeFolder) ? folder : relativeFolder;
            }
            GUIUtility.keyboardControl = 0;
        }

        private void PurgeOutputFolder()
        {
            if (!BuildPathValidator.ValidOutputFolder(m_Settings.outputPath, true))
                return;

            if (!EditorUtility.DisplayDialog("Purge Output Folder", "Do you really want to delete your output folder?", "Yes", "No"))
                return;

            if (Directory.Exists(m_Settings.outputPath))
                Directory.Delete(m_Settings.outputPath, true);
        }

        private void BuildAssetBundles()
        {
            if (!BuildPathValidator.ValidOutputFolder(m_Settings.outputPath, true))
                return;

            var buildTimer = new Stopwatch();
            buildTimer.Start();

            var success = true;
            if (m_Settings.useExperimentalPipeline)
                success = ExperimentalBuildPipeline();
            else
                success = LegacyBuildPipeline();

            buildTimer.Stop();
            BuildLogger.Log("Build Asset Bundles {0} in: {1:c}", success ? "completed" : "failed", buildTimer.Elapsed);
        }

        private bool ExperimentalBuildPipeline()
        {
            var playerSettings = PlayerBuildPipeline.GeneratePlayerBuildSettings(m_Settings.buildTarget, m_Settings.buildGroup);
            ScriptCompilationResult scriptResults;
            var errorCode = PlayerBuildPipeline.BuildPlayerScripts(playerSettings, out scriptResults);
            if (errorCode < BuildPipelineCodes.Success)
                return false;

            var bundleSettings = BundleBuildPipeline.GenerateBundleBuildSettings(m_Settings.buildTarget, m_Settings.buildGroup);

            BuildCompression compression = BuildCompression.DefaultLZ4;
            if (m_Settings.compressionType == CompressionType.None)
                compression = BuildCompression.DefaultUncompressed;
            else if (m_Settings.compressionType == CompressionType.Lzma)
                compression = BuildCompression.DefaultLZMA;

            BundleBuildResult bundleResult;
            var success = BundleBuildPipeline.BuildAssetBundles_Internal(BundleBuildInterface.GenerateBuildInput(), bundleSettings, compression, m_Settings.outputPath, null, m_Settings.useBuildCache, out bundleResult);
            return success >= BuildPipelineCodes.Success;
        }

        private bool LegacyBuildPipeline()
        {
            var options = BuildAssetBundleOptions.None;
            if (m_Settings.compressionType == CompressionType.None)
                options |= BuildAssetBundleOptions.UncompressedAssetBundle;
            else if (m_Settings.compressionType == CompressionType.Lz4HC || m_Settings.compressionType == CompressionType.Lz4)
                options |= BuildAssetBundleOptions.ChunkBasedCompression;

            if (!m_Settings.useBuildCache)
                options |= BuildAssetBundleOptions.ForceRebuildAssetBundle;

            Directory.CreateDirectory(m_Settings.outputPath);
            var manifest = BuildPipeline.BuildAssetBundles(m_Settings.outputPath, options, m_Settings.buildTarget);
            return manifest != null;
        }
    }
}
