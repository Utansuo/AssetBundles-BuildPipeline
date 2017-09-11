using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.Build.Tests
{
    public static class TestDataGenerators
    {
        private static FieldInfo m_GUID;
        private static FieldInfo m_LocalIdentifierInFile;
        private static FieldInfo m_FileType;
        private static FieldInfo m_FilePath;

        static TestDataGenerators()
        {
            var objectIdentifier = typeof(ObjectIdentifier);
            m_GUID = objectIdentifier.GetField("m_GUID", BindingFlags.NonPublic | BindingFlags.Instance);
            m_LocalIdentifierInFile = objectIdentifier.GetField("m_LocalIdentifierInFile", BindingFlags.NonPublic | BindingFlags.Instance);
            m_FileType = objectIdentifier.GetField("m_FileType", BindingFlags.NonPublic | BindingFlags.Instance);
            m_FilePath = objectIdentifier.GetField("m_FilePath", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        internal static ObjectIdentifier ConstructObjectIdentifier(GUID guid, long localIdentifierInFile, FileType fileType, string filePath)
        {
            ValueType value = new ObjectIdentifier();
            m_GUID.SetValue(value, guid);
            m_LocalIdentifierInFile.SetValue(value, localIdentifierInFile);
            m_FileType.SetValue(value, fileType);
            m_FilePath.SetValue(value, filePath);
            return (ObjectIdentifier)value;
        }

        internal static BuildCommandSet.AssetLoadInfo CreatePrefabWithMeshReference(GUID prefabGuid, GUID meshGuid)
        {
            var asset = new BuildCommandSet.AssetLoadInfo();
            asset.address = prefabGuid.ToString();
            asset.asset = prefabGuid;
            asset.processedScene = "";
            asset.includedObjects = new[]
            {
                ConstructObjectIdentifier(prefabGuid, 1326890503170502, FileType.SerializedAssetType, ""),      // GameObject
                ConstructObjectIdentifier(prefabGuid, 4326504406238768, FileType.SerializedAssetType, ""),      // Transform
                ConstructObjectIdentifier(prefabGuid, 114305515917122674, FileType.SerializedAssetType, "")     // Monobehavior W/ Reference
            };
            asset.referencedObjects = new[]
            {
                ConstructObjectIdentifier(meshGuid, 4300000, FileType.MetaAssetType, ""),                                       // Mesh
                ConstructObjectIdentifier(new GUID("206794ec26056d846b1615847cacd2cc"), 11500000, FileType.MetaAssetType, "")   // MonoScript
            };
            return asset;
        }

        internal static BuildCommandSet.AssetLoadInfo CreateFBXWithMesh(GUID fbxGuid)
        {
            var asset = new BuildCommandSet.AssetLoadInfo();
            asset.address = fbxGuid.ToString();
            asset.asset = fbxGuid;
            asset.processedScene = "";
            asset.includedObjects = new[]
            {
                ConstructObjectIdentifier(fbxGuid, 100000, FileType.MetaAssetType, ""),     // GameObject
                ConstructObjectIdentifier(fbxGuid, 400000, FileType.MetaAssetType, ""),     // Transform
                ConstructObjectIdentifier(fbxGuid, 2100000, FileType.MetaAssetType, ""),    // Material
                ConstructObjectIdentifier(fbxGuid, 2300000, FileType.MetaAssetType, ""),    // MeshRenderer
                ConstructObjectIdentifier(fbxGuid, 3300000, FileType.MetaAssetType, ""),    // MeshFilter
                ConstructObjectIdentifier(fbxGuid, 4300000, FileType.MetaAssetType, "")     // Mesh
            };
            asset.referencedObjects = new[]
            {
                ConstructObjectIdentifier(new GUID("0000000000000000f000000000000000"), 6, FileType.NonAssetType, "resources/unity_builtin_extra"), // Shader
                ConstructObjectIdentifier(new GUID("0000000000000000f000000000000000"), 46, FileType.NonAssetType, "resources/unity_builtin_extra") // Shader
            };
            return asset;
        }

        // Generates example data layout of 2 Prefabs both referencing the Mesh of an FBX
        // Each Prefab and Mesh is located in a separate bundle
        public static BuildDependencyInformation CreateAssetsWithReferences(bool includeFbxInBundle)
        {
            var prefab1 = new GUID("00000000000000000000000000000001");
            var prefab2 = new GUID("00000000000000000000000000000002");
            var fbx = new GUID("00000000000000000000000000000010");

            var buildInfo = new BuildDependencyInformation();
            buildInfo.assetLoadInfo.Add(prefab1, CreatePrefabWithMeshReference(prefab1, fbx));
            buildInfo.assetLoadInfo.Add(prefab2, CreatePrefabWithMeshReference(prefab2, fbx));
            if (includeFbxInBundle)
                buildInfo.assetLoadInfo.Add(fbx, CreateFBXWithMesh(fbx));

            List<string> assetDependencies;
            buildInfo.assetToBundles.GetOrAdd(prefab1, out assetDependencies);
            assetDependencies.Add(prefab1.ToString());
            if (includeFbxInBundle)
                assetDependencies.Add(fbx.ToString());

            buildInfo.assetToBundles.GetOrAdd(prefab2, out assetDependencies);
            assetDependencies.Add(prefab2.ToString());
            if (includeFbxInBundle)
                assetDependencies.Add(fbx.ToString());

            if (includeFbxInBundle)
            {
                buildInfo.assetToBundles.GetOrAdd(fbx, out assetDependencies);
                assetDependencies.Add(fbx.ToString());
            }

            List<GUID> assetsInBundle;
            buildInfo.bundleToAssets.GetOrAdd(prefab1.ToString(), out assetsInBundle);
            assetsInBundle.Add(prefab1);

            buildInfo.bundleToAssets.GetOrAdd(prefab2.ToString(), out assetsInBundle);
            assetsInBundle.Add(prefab2);

            if (includeFbxInBundle)
            {
                buildInfo.bundleToAssets.GetOrAdd(fbx.ToString(), out assetsInBundle);
                assetsInBundle.Add(fbx);
            }

            return buildInfo;
        }
    }
}