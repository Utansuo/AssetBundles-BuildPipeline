using System;
using System.Collections.Generic;
using UnityEditor.Experimental.Build;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.Build
{
    [Serializable]
    public class BuildDependencyInformation
    {
        // AssetLoadInfo for all scenes and assets
        public Dictionary<GUID, BuildCommandSet.AssetLoadInfo> assetLoadInfo = new Dictionary<GUID, BuildCommandSet.AssetLoadInfo>();
        
        // Scene specific dependency information
        public Dictionary<GUID, ResourceFile[]> sceneResourceFiles = new Dictionary<GUID, ResourceFile[]>();
        public Dictionary<GUID, BuildUsageTagGlobal> sceneUsageTags = new Dictionary<GUID, BuildUsageTagGlobal>();

        // Lookup maps for fast dependency calculation
        public Dictionary<GUID, string> assetToBundle = new Dictionary<GUID, string>();
        public Dictionary<string, List<GUID>> bundleToAssets = new Dictionary<string, List<GUID>>();
    }
}
