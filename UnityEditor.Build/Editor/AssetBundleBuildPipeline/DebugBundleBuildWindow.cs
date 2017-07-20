using System;
using System.Diagnostics;
using System.IO;
using UnityEditor.Build.AssetBundle;
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
            public string outputPath;
        }

        [SerializeField]
        Settings m_Settings;

        SerializedObject m_SerializedObject;
        SerializedProperty m_TargetProp;
        SerializedProperty m_GroupProp;
        SerializedProperty m_CompressionProp;
        SerializedProperty m_CacheProp;
        SerializedProperty m_OutputProp;

        // Add menu named "My Window" to the Window menu
        [MenuItem("Window/Build Pipeline/Debug Window")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            var window = GetWindow<DebugBundleBuildWindow>("Debug Build");
            window.m_Settings.buildTarget = EditorUserBuildSettings.activeBuildTarget;
            window.m_Settings.buildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            window.Show();
        }

        private void OnEnable()
        {
            m_SerializedObject = new SerializedObject(this);
            m_TargetProp = m_SerializedObject.FindProperty("m_Settings.buildTarget");
            m_GroupProp = m_SerializedObject.FindProperty("m_Settings.buildGroup");
            m_CompressionProp = m_SerializedObject.FindProperty("m_Settings.compressionType");
            m_CacheProp = m_SerializedObject.FindProperty("m_Settings.useBuildCache");
            m_OutputProp = m_SerializedObject.FindProperty("m_Settings.outputPath");
        }

        private void OnGUI()
        {
            m_SerializedObject.Update();
            
            EditorGUILayout.PropertyField(m_TargetProp);
            EditorGUILayout.PropertyField(m_GroupProp);
            EditorGUILayout.PropertyField(m_CompressionProp);
            EditorGUILayout.PropertyField(m_CacheProp);

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
            if (!string.IsNullOrEmpty(folder))
            {
                var relativeFolder = FileUtil.GetProjectRelativePath(folder);
                m_OutputProp.stringValue = string.IsNullOrEmpty(relativeFolder) ? folder : relativeFolder;
            }
            GUIUtility.keyboardControl = 0;
        }

        private void PurgeOutputFolder()
        {
            if (!EditorUtility.DisplayDialog("Purge Output Folder", "Do you really want to delete your output folder?", "Yes", "No"))
                return;

            if (Directory.Exists(m_Settings.outputPath))
                Directory.Delete(m_Settings.outputPath, true);
        }

        private void BuildAssetBundles()
        {
            var buildTimer = new Stopwatch();
            buildTimer.Start();

            var playerSettings = BundleBuildPipeline.GeneratePlayerBuildSettings();
            playerSettings.target = m_Settings.buildTarget;
            var playerResults = PlayerBuildInterface.CompilePlayerScripts(playerSettings, BundleBuildPipeline.kTempPlayerBuildPath);
            if (Directory.Exists(BundleBuildPipeline.kTempPlayerBuildPath))
                Directory.Delete(BundleBuildPipeline.kTempPlayerBuildPath, true);

            var bundleSettings = BundleBuildPipeline.GenerateBundleBuildSettings();
            bundleSettings.target = m_Settings.buildTarget;
            bundleSettings.group = m_Settings.buildGroup;
            bundleSettings.typeDB = playerResults.typeDB;

            BuildCompression compression;
            switch (m_Settings.compressionType)
            {
                case CompressionType.Lzma:
                    compression = BuildCompression.DefaultLZMA;
                    break;
                case CompressionType.None:
                    compression = BuildCompression.DefaultUncompressed;
                    break;
                default:
                    compression = BuildCompression.DefaultLZ4;
                    break;
            }

            var success = BundleBuildPipeline.BuildAssetBundles(BundleBuildInterface.GenerateBuildInput(), bundleSettings, m_Settings.outputPath, compression, m_Settings.useBuildCache);

            buildTimer.Stop();
            BuildLogger.Log("Build Asset Bundles {0} in: {1:c}", success ? "completed" : "failed", buildTimer.Elapsed);
        }
    }
}
