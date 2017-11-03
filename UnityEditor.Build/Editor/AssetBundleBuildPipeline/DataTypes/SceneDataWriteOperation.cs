using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.Build.AssetBundle.DataTypes
{
    public class SceneDataWriteOperation : RawWriteOperation
    {
        public string scene = "";
        public string processedScene = "";
        public List<ObjectIdentifier> preloadObjects = new List<ObjectIdentifier>();

        public SceneDataWriteOperation() { }
        public SceneDataWriteOperation(RawWriteOperation other) : base(other) { }
        public SceneDataWriteOperation(SceneDataWriteOperation other) : base(other)
        {
            // Notes: May want to switch to MemberwiseClone, for now those this is fine
            scene = other.scene;
            processedScene = other.processedScene;
            preloadObjects = other.preloadObjects;
        }

        public override WriteResult Write(string outputFolder, List<WriteCommand> dependencies, BuildSettings settings, BuildUsageTagGlobal globalUsage)
        {
            return BundleBuildInterface.WriteSceneSerializedFile(outputFolder, scene, processedScene, command, dependencies, settings, globalUsage, preloadObjects);
        }
    }
}
