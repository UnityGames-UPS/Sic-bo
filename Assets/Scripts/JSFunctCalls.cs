using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// JavaScript bridge for WebGL communication with React Native
/// </summary>
public class JSFunctCalls : MonoBehaviour
{
    #region External Functions
    [DllImport("__Internal")]
    private static extern void SendLogToReactNative(string message);

    [DllImport("__Internal")]
    private static extern void SendPostMessage(string message);
    #endregion

    #region Unity Lifecycle
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
    #endregion

    #region Private Methods
#if UNITY_WEBGL && !UNITY_EDITOR
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string formattedMessage = $"[{type}] {logString}";
        SendLogToReactNative(formattedMessage);
    }
#endif
    #endregion

    #region Public API
    internal void SendCustomMessage(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log($"[JS] Sending message to platform: {message}");
        SendPostMessage(message);
#else
        Debug.Log($"[JS] Would send message (editor mode): {message}");
#endif
    }
    #endregion
}
