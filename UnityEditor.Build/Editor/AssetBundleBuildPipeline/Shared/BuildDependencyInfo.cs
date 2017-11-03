using System;
using System.Collections.Generic;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.Build
{
    [Serializable]
    public class BuildDependencyInfo
    {
        // AssetLoadInfo for all assets
        public Dictionary<GUID, AssetLoadInfo> assetInfo = new Dictionary<GUID, AssetLoadInfo>();
        public Dictionary<GUID, SceneDependencyInfo> sceneInfo = new Dictionary<GUID, SceneDependencyInfo>();

        // Lookup maps for fast dependency calculation
        public Dictionary<GUID, List<string>> assetToBundles = new Dictionary<GUID, List<string>>();
        public Dictionary<string, List<GUID>> bundleToAssets = new Dictionary<string, List<GUID>>();

        public HashSet<GUID> virtualAssets = new HashSet<GUID>();
        public Dictionary<ObjectIdentifier, GUID> objectToVirtualAsset = new Dictionary<ObjectIdentifier, GUID>();
    }
}
