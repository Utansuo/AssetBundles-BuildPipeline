using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityEditor.Build.Utilities
{
    public static class BuildPathValidator
    {
        [Conditional("DEBUG")]
        public static void LogError(bool logError, string msg, params object[] attrs)
        {
            if (!logError)
                return;

            BuildLogger.LogError(string.Format(msg, attrs));
        }

        public static bool ValidOutputFolder(string outputFolder, bool logError)
        {
            if (string.IsNullOrEmpty(outputFolder))
            {
                LogError(logError, "Path: '{0}' is not a valid output folder for a build.", outputFolder);
                return false;
            }
            
            if (Path.GetFullPath(outputFolder) == Path.GetFullPath(Application.dataPath))
            {
                LogError(logError, "Path: '{0}' is not a valid output folder for a build.", outputFolder);
                return false;
            }
            
            if (Path.GetFullPath(outputFolder) == Path.GetFullPath(Application.dataPath + "\\..\\ProjectSettings"))
            {
                LogError(logError, "Path: '{0}' is not a valid output folder for a build.", outputFolder);
                return false;
            }

            if (Path.GetFullPath(outputFolder) == Path.GetFullPath(Application.dataPath + "\\..\\Packages"))
            {
                LogError(logError, "Path: '{0}' is not a valid output folder for a build.", outputFolder);
                return false;
            }

            if (Path.GetFullPath(outputFolder) == Path.GetFullPath(Application.dataPath + "\\..\\Temp"))
            {
                LogError(logError, "Path: '{0}' is not a valid output folder for a build.", outputFolder);
                return false;
            }

            // TODO: Platform dependent checks
#if UNITY_EDITOR_WIN
#elif UNITY_EDITOR_OSX
#elif UNITY_EDITOR_LINUX
#endif
            return true;
        }
    }
}