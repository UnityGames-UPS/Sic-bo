using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// JavaScript bridge for WebGL communication
/// Unified version for both Plinko and Slot games
/// </summary>
public class JSFunctCalls : MonoBehaviour
{
    // External JavaScript functions
    [DllImport("__Internal")]
    private static extern void SendLogToReactNative(string message);

    [DllImport("__Internal")]
    private static extern void SendPostMessage(string message);

    private void OnEnable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.logMessageReceived += HandleLog;
        Debug.Log("[JS] Log forwarding enabled");
#endif
    }

    private void OnDisable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.logMessageReceived -= HandleLog;
        Debug.Log("[JS] Log forwarding disabled");
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string formattedMessage = $"[{type}] {logString}";
        SendLogToReactNative(formattedMessage);
    }
#endif

    /// <summary>
    /// Send custom message to React Native platform
    /// Used for: "authToken", "OnEnter", "OnExit", "error"
    /// </summary>
    internal void SendCustomMessage(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log($"[JS] Sending message to platform: {message}");
        SendPostMessage(message);
#else
        Debug.Log($"[JS] Would send message (editor mode): {message}");
#endif
    }
}