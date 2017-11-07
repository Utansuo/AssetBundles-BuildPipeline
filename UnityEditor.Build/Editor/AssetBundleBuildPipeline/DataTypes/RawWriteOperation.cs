using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Utilities;
using UnityEditor.Experimental.Build;
using UnityEditor.Experimental.Build.AssetBundle;

namespace UnityEditor.Build.AssetBundle.DataTypes
{
    public class RawWriteOperation : IWriteOperation
    {
        public WriteCommand command { get { return m_Command; } }
        protected WriteCommand m_Command = new WriteCommand();

        public RawWriteOperation() { }
        public RawWriteOperation(RawWriteOperation other)
        {
            // Notes: May want to switch to MemberwiseClone, for now those this is fine
            m_Command = other.m_Command;
        }

        public virtual List<WriteCommand> CalculateDependencies(List<WriteCommand> allCommands)
        {
            if (command == null)
                return null;
            if (command.dependencies.IsNullOrEmpty())
                return null;
            var result = allCommands.Where(x => command.dependencies.Contains(x.internalName));
            return result.ToList(); // TODO: Need to validate that we had all the dependencies
        }

        public virtual WriteResult Write(string outputFolder, List<WriteCommand> dependencies, BuildSettings settings, BuildUsageTagGlobal globalUsage)
        {
            return BundleBuildInterface.WriteSerializedFile(outputFolder, command, dependencies, settings, globalUsage);
        }
    }
}
