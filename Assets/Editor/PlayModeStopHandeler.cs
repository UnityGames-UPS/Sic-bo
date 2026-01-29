using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeStopHandler
{
    static PlayModeStopHandler()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // Call a function on a scene object BEFORE it gets destroyed
            CallFunctionOnSceneObject();
        }
    }

    private static void CallFunctionOnSceneObject()
    {
        // Find the GameObject in the active scene
        GameObject targetObject = GameObject.Find("SocketManager"); // Change this name as needed

        if (targetObject != null)
        {
            // Call a method from the script attached to this GameObject
            var targetScript = targetObject.GetComponent<SocketIOManager>(); // Replace with your actual script name
            if (targetScript != null)
            {
               // targetScript.CloseSocket(); // Call your desired method
            }
            else
            {
                Debug.LogWarning("TargetScript not found on the GameObject.");
            }
        }
        else
        {
            Debug.LogWarning("GameObject not found in the scene.");
        }
    }
}