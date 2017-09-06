using UnityEditor.SceneManagement;

namespace UnityEditor.Build.Utilities
{
    public static class ProjectValidator
    {
        public static bool UnsavedChanges()
        {
            var unsavedChanges = false;
            var sceneCount = EditorSceneManager.sceneCount;
            for (var i = 0; i < sceneCount; ++i)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isDirty)
                    continue;
                unsavedChanges = true;
                break;
            }

            unsavedChanges |= AssetDatabase.HasDirtyAssets();
            return unsavedChanges;
        }
    }
}
