using System.Collections.Generic;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.Build.AssetBundle
{
    public struct BundleBuildResult
    {
        public Dictionary<string, uint> bundleCRCs;
        public List<BuildOutput.Result> bundleDetails;
    }
}
