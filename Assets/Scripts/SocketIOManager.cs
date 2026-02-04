using Best.SocketIO;
using Best.SocketIO.Events;
using Newtonsoft.Json;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Handles all Socket.IO communication for Sic Bo multiplayer game
/// PRODUCTION-READY: Includes proper cleanup, timeouts, error handling, and multiplayer features
/// Based on improved Plinko architecture with Andar Bahar multiplayer patterns
/// </summary>
public class SocketIOManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UIController uiController;
    [SerializeField] internal JSFunctCalls JSManager;

    [Header("Testing (Editor Only)")]
    [SerializeField] private string testToken;

    [Header("Blocker")]
    [SerializeField] private GameObject RaycastBlocker;

    [Header("Settings")]
    [SerializeField] private float disconnectDelay = 60f;
    #endregion

    #region Public Properties
    internal SicBoGameData InitialData { get; private set; }
    internal Player PlayerData { get; private set; }
    internal bool IsInitialized { get; private set; }
    #endregion

    #region Private Fields - Connection
    private SocketManager manager;
    private Socket gameSocket;
    private string SocketURI = null;
    private const string TestSocketURI = "https://devrealtime.dingdinghouse.com/";
    private string nameSpace = "playground-multiplayer";
    private string myAuth = null;
    #endregion

    #region Private Fields - State
    private bool isConnected = false;
    private bool hasEverConnected = false;
    private bool isExiting = false;
    private bool isWaitingForInitData = false;
    private bool isBeingDestroyed = false;
    private bool hasFocus = true;
    #endregion

    #region Private Fields - Ping/Pong
    private float lastPongTime = 0f;
    private const float pingInterval = 2f;
    private bool waitingForPong = false;
    private int missedPongs = 0;
    private const int MaxMissedPongs = 5;
    #endregion

    #region Private Fields - Timers
    private float focusLostTime = 0f;
    private const float maxBackgroundTime = 120f;
    #endregion

    #region Private Fields - Coroutines
    private Coroutine PingRoutine;
    private Coroutine initTimeoutRoutine;
    private Coroutine disconnectTimerCoroutine;
    private Coroutine focusCheckRoutine;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        Application.runInBackground = true;
        IsInitialized = false;
        isBeingDestroyed = false;
    }

    private void Start()
    {
        if (!ValidateToken()) return;
        OpenSocket();
    }

    private void OnDestroy()
    {
        Debug.Log("[SOCKET] Destroying");

        isBeingDestroyed = true;
        isExiting = true;

        CleanupRoutines();

        if (manager != null)
        {
            try
            {
                manager.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SOCKET] Error closing: {e.Message}");
            }
            manager = null;
        }

        gameSocket = null;
    }

    private void OnApplicationFocus(bool focus)
    {
        if (isBeingDestroyed) return;

        hasFocus = focus;

        if (!focus)
        {
            focusLostTime = Time.time;

            if (focusCheckRoutine == null && gameObject.activeInHierarchy)
            {
                focusCheckRoutine = StartCoroutine(FocusTimeoutCheck());
            }
        }
        else
        {
            if (focusCheckRoutine != null)
            {
                StopCoroutine(focusCheckRoutine);
                focusCheckRoutine = null;
            }
        }
    }
    #endregion

    #region Validation
    private bool ValidateToken()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(testToken) || testToken.Length < 10)
        {
            Debug.LogError("[VALIDATION] Invalid test token");
            ShowErrorAndBlock("Test token is required in editor mode");
            return false;
        }
        return true;
#else
        return true;
#endif
    }
    #endregion

    #region Socket Connection
    private void OpenSocket()
    {
        if (isBeingDestroyed) return;

        RaycastBlocker?.SetActive(true);

        SocketOptions options = new SocketOptions
        {
            AutoConnect = false,
            Reconnection = false,
            Timeout = TimeSpan.FromSeconds(5),
            ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket
        };

#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("authToken");
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(WaitForAuthToken(options));
        }
#else
        options.Auth = (manager, socket) => new { token = testToken };
        SetupSocketManager(options);
#endif
    }

    private IEnumerator WaitForAuthToken(SocketOptions options)
    {
        float timeout = 15f;
        float elapsed = 0f;

        while (myAuth == null && elapsed < timeout && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isBeingDestroyed) yield break;

        if (myAuth == null)
        {
            Debug.LogError("[AUTH] Token timeout");
            ShowErrorAndBlock("Authentication failed. Please refresh the page.");
            yield break;
        }

        elapsed = 0f;
        while (SocketURI == null && elapsed < timeout && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isBeingDestroyed) yield break;

        if (SocketURI == null)
        {
            Debug.LogError("[AUTH] URI timeout");
            ShowErrorAndBlock("Connection configuration failed. Please refresh.");
            yield break;
        }

        options.Auth = (manager, socket) => new { token = myAuth };
        SetupSocketManager(options);
    }

    private void SetupSocketManager(SocketOptions options)
    {
        if (isBeingDestroyed) return;

#if UNITY_EDITOR
        this.manager = new SocketManager(new Uri(TestSocketURI), options);
#else
        this.manager = new SocketManager(new Uri(SocketURI), options);
#endif

        gameSocket = string.IsNullOrEmpty(nameSpace) ?
            this.manager.Socket :
            this.manager.GetSocket("/" + nameSpace);

        RegisterEventHandlers();
        manager.Open();

        if (gameObject.activeInHierarchy && !isBeingDestroyed)
        {
            initTimeoutRoutine = StartCoroutine(ConnectionAndInitTimeout());
        }
    }

    private void RegisterEventHandlers()
    {
        // Connection events
        gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
        gameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected);
        gameSocket.On<Error>(SocketIOEventTypes.Error, OnError);

        // Game events
        gameSocket.On<string>("game:init", OnInitData);
        gameSocket.On<string>("game:round_start", OnRoundStart);
        gameSocket.On<string>("game:betting_timer", OnBettingTimer);
        gameSocket.On<string>("game:bonus", OnBonus);
        gameSocket.On<string>("game:dice_result", OnDiceResult);
        gameSocket.On<string>("game:bet_placed", OnBetPlaced);
        gameSocket.On<string>("game:cashout", OnCashout);
        gameSocket.On<string>("game:lobby_count", OnLobbyCount);
        gameSocket.On<string>("game:round_end", OnRoundEnd);

        // Room events
        gameSocket.On<string>("room:joined", OnRoomJoined);
        gameSocket.On<string>("room:left", OnRoomLeft);

        // Request responses
        gameSocket.On<string>("request", OnRequest);

        // System events
        gameSocket.On<string>("pong", OnPongReceived);
        gameSocket.On<string>("internalError", OnInternalError);
        gameSocket.On<string>("alert", OnAlert);
        gameSocket.On<string>("AnotherDevice", OnAnotherDevice);
    }

    private IEnumerator ConnectionAndInitTimeout()
    {
        float connectionTimeout = 15f;
        float initTimeout = 10f;
        float elapsed = 0f;

        // Wait for connection
        while (!isConnected && elapsed < connectionTimeout && !isExiting && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isBeingDestroyed) yield break;

        if (!isConnected)
        {
            Debug.LogError("[CONNECT] Connection timeout");
            ShowErrorAndBlock("Failed to connect. Please check your connection.");
            yield break;
        }

        // Wait for init data
        isWaitingForInitData = true;
        elapsed = 0f;

        while (isWaitingForInitData && elapsed < initTimeout && !isExiting && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isBeingDestroyed) yield break;

        if (isWaitingForInitData)
        {
            Debug.LogError("[INIT] Data timeout");
            ShowErrorAndBlock("Failed to load game data. Please refresh.");
        }

        initTimeoutRoutine = null;
    }
    #endregion

    #region Connection Events
    private void OnConnected(ConnectResponse resp)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[CONNECT] ✅ Connected to server");

        if (hasEverConnected)
        {
            uiController?.CloseReconnectPopup();
        }

        isConnected = true;
        hasEverConnected = true;
        waitingForPong = false;
        missedPongs = 0;
        lastPongTime = Time.time;

        // Stop disconnect timer if running
        if (disconnectTimerCoroutine != null)
        {
            StopCoroutine(disconnectTimerCoroutine);
            disconnectTimerCoroutine = null;
        }

        SendPing();
    }

    private void OnDisconnected()
    {
        if (isBeingDestroyed) return;

        Debug.LogWarning("[DISCONNECT] ⚠️ Disconnected from server");
        isConnected = false;
        ResetPingRoutine();

        uiController?.ShowDisconnectPopup();

        // Start disconnect timer
        if (disconnectTimerCoroutine == null && gameObject.activeInHierarchy && !isExiting)
        {
            disconnectTimerCoroutine = StartCoroutine(DisconnectTimer());
        }
    }

    private void OnError(Error err)
    {
        if (isBeingDestroyed) return;

        Debug.LogError($"[ERROR] Socket error: {err}");

#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager?.SendCustomMessage("error");
#endif
    }

    private void OnPongReceived(string data)
    {
        if (isBeingDestroyed) return;

        waitingForPong = false;
        missedPongs = 0;
        lastPongTime = Time.time;
    }

    private IEnumerator DisconnectTimer()
    {
        float elapsed = 0f;

        while (elapsed < disconnectDelay && !isExiting && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(1f);
        }

        if (!isConnected && !isExiting && !isBeingDestroyed)
        {
            Debug.LogError("[DISCONNECT] Timeout reached");
            ShowErrorAndBlock("Connection lost. Please refresh the page.");
        }

        disconnectTimerCoroutine = null;
    }
    #endregion

    #region Ping/Pong System
    private void SendPing()
    {
        if (isBeingDestroyed || !isConnected) return;

        ResetPingRoutine();

        if (gameObject.activeInHierarchy)
        {
            PingRoutine = StartCoroutine(PingCheck());
        }
    }

    private IEnumerator PingCheck()
    {
        while (isConnected && !isExiting && !isBeingDestroyed)
        {
            if (isBeingDestroyed) yield break;

            Debug.Log($"[PING] waitingForPong: {waitingForPong}, missedPongs: {missedPongs}");

            if (missedPongs == 0)
            {
                uiController?.CloseReconnectPopup();
            }

            if (waitingForPong)
            {
                if (missedPongs == 2)
                {
                    uiController?.ShowReconnectPopup();
                }

                missedPongs++;
                Debug.LogWarning($"[PING] Missed #{missedPongs}/{MaxMissedPongs}");

                if (missedPongs >= MaxMissedPongs)
                {
                    Debug.LogError("[PING] 5 consecutive misses");
                    isConnected = false;
                    uiController?.ShowDisconnectPopup();
                    yield break;
                }
            }

            waitingForPong = true;
            lastPongTime = Time.time;
            SendDataWithNamespace("ping");

            yield return new WaitForSeconds(pingInterval);
        }

        PingRoutine = null;
    }

    private void ResetPingRoutine()
    {
        if (PingRoutine != null)
        {
            StopCoroutine(PingRoutine);
            PingRoutine = null;
        }
        waitingForPong = false;
        missedPongs = 0;
    }
    #endregion

    #region Background Handling
    private IEnumerator FocusTimeoutCheck()
    {
        while (!hasFocus && !isExiting && !isBeingDestroyed)
        {
            float timeInBackground = Time.time - focusLostTime;

            if (timeInBackground >= maxBackgroundTime)
            {
                Debug.LogWarning("[FOCUS] Max background time exceeded");
                ShowErrorAndBlock("Session expired due to inactivity. Please refresh.");
                yield break;
            }

            yield return new WaitForSeconds(1f);
        }

        focusCheckRoutine = null;
    }
    #endregion

    #region Game Events
    private void OnInitData(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[INIT] Data received");

        isWaitingForInitData = false;

        if (initTimeoutRoutine != null)
        {
            StopCoroutine(initTimeoutRoutine);
            initTimeoutRoutine = null;
        }

        try
        {
            SicBoRoot root = JsonConvert.DeserializeObject<SicBoRoot>(jsonData);

            if (root == null)
            {
                Debug.LogError("[INIT] Null response");
                return;
            }

            InitialData = root.gameData;
            PlayerData = root.player;

            if (!IsInitialized)
            {
                gameManager?.OnInitDataReceived();
                IsInitialized = true;

#if UNITY_WEBGL && !UNITY_EDITOR
                JSManager?.SendCustomMessage("OnEnter");
#endif

                RaycastBlocker?.SetActive(false);
                Debug.Log("[INIT] Game ready");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[INIT] Parse error: {e.Message}\n{e.StackTrace}");
            ShowErrorAndBlock("Failed to initialize game data");
        }
    }

    private void OnRoomJoined(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[ROOM] Joined: {jsonData}");

        try
        {
            // Room join data is simple, no special handling needed
            // Game will handle state through game:init or request response
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Join error: {e.Message}");
        }
    }

    private void OnRoomLeft(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[ROOM] Left: {jsonData}");
    }

    private void OnRequest(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[REQUEST] Response: {jsonData}");

        try
        {
            SicBoRoot response = JsonConvert.DeserializeObject<SicBoRoot>(jsonData);

            if (response == null) return;

            // Handle different request responses
            if (!response.success)
            {
                // Show error message if provided
                if (response.payload?.message != null)
                {
                    uiController?.ShowNotification(response.payload.message);
                }
                return;
            }

            // Success responses handled by GameManager based on payload
            // Could include: room join, bet placed, etc.
        }
        catch (Exception e)
        {
            Debug.LogError($"[REQUEST] Error: {e.Message}");
        }
    }

    private void OnRoundStart(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[ROUND] Start");

        try
        {
            RoundStartData data = JsonConvert.DeserializeObject<RoundStartData>(jsonData);
            gameManager?.OnRoundStart(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROUND] Start error: {e.Message}");
        }
    }

    private void OnBettingTimer(string jsonData)
    {
        if (isBeingDestroyed) return;

        try
        {
            TimerData data = JsonConvert.DeserializeObject<TimerData>(jsonData);
            gameManager?.OnBettingTimer(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TIMER] Error: {e.Message}");
        }
    }

    private void OnBonus(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[BONUS] Received");

        try
        {
            BonusData data = JsonConvert.DeserializeObject<BonusData>(jsonData);
            gameManager?.OnBonus(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BONUS] Error: {e.Message}");
        }
    }

    private void OnDiceResult(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[DICE] Result received");

        try
        {
            DiceResultData data = JsonConvert.DeserializeObject<DiceResultData>(jsonData);
            gameManager?.OnDiceResult(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DICE] Error: {e.Message}");
        }
    }

    private void OnBetPlaced(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[BET] Placed by player");

        try
        {
            BetPlacedData data = JsonConvert.DeserializeObject<BetPlacedData>(jsonData);
            gameManager?.OnBetPlaced(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BET] Error: {e.Message}");
        }
    }

    private void OnCashout(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[CASHOUT] Received");

        try
        {
            CashoutData data = JsonConvert.DeserializeObject<CashoutData>(jsonData);
            gameManager?.OnCashout(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CASHOUT] Error: {e.Message}");
        }
    }

    private void OnLobbyCount(string jsonData)
    {
        if (isBeingDestroyed) return;

        try
        {
            LobbyCountData data = JsonConvert.DeserializeObject<LobbyCountData>(jsonData);
            gameManager?.OnLobbyCount(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LOBBY] Error: {e.Message}");
        }
    }

    private void OnRoundEnd(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[ROUND] End");

        try
        {
            RoundEndData data = JsonConvert.DeserializeObject<RoundEndData>(jsonData);
            gameManager?.OnRoundEnd(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROUND] End error: {e.Message}");
        }
    }

    private void OnInternalError(string data)
    {
        if (isBeingDestroyed) return;

        Debug.LogError($"[ERROR] Internal: {data}");
        uiController?.ShowNotification("Server error occurred");
    }

    private void OnAlert(string data)
    {
        if (isBeingDestroyed) return;

        uiController?.ShowNotification(data);
    }

    private void OnAnotherDevice(string data)
    {
        if (isBeingDestroyed) return;

        Debug.LogWarning("[DEVICE] Another device detected");
        uiController?.ShowAnotherDevicePopup();
    }
    #endregion

    #region Public API - Client Actions
    internal void JoinLevel(string level)
    {
        if (isBeingDestroyed) return;

        var payload = new { level };
        string json = JsonConvert.SerializeObject(payload);
        SendDataWithNamespace("JOIN_LEVEL", json);

        Debug.Log($"[JOIN] Level: {level}");
    }

    internal void PlaceBet(string betType, string betOption, int amountIndex, string level)
    {
        if (isBeingDestroyed) return;

        var payload = new { betType, betOption, amountIndex, level };
        string json = JsonConvert.SerializeObject(payload);
        SendDataWithNamespace("PLACE_BET", json);

        Debug.Log($"[BET] Placing: {betOption} at index {amountIndex}");
    }

    internal void CancelBet()
    {
        if (isBeingDestroyed) return;

        SendDataWithNamespace("CANCEL_BET", "{}");
        Debug.Log("[BET] Canceling all");
    }

    internal void DoubleBet(string betId)
    {
        if (isBeingDestroyed) return;

        var payload = new { betId };
        string json = JsonConvert.SerializeObject(payload);
        SendDataWithNamespace("DOUBLE_BET", json);

        Debug.Log("[BET] Doubling");
    }

    internal void RepeatBet()
    {
        if (isBeingDestroyed) return;

        SendDataWithNamespace("REPEAT_BET", "{}");
        Debug.Log("[BET] Repeating");
    }

    internal void UndoBet()
    {
        if (isBeingDestroyed) return;

        SendDataWithNamespace("UNDO_BET", "{}");
        Debug.Log("[BET] Undoing last");
    }

    internal void RequestHistory(int page)
    {
        if (isBeingDestroyed) return;

        var payload = new { page };
        string json = JsonConvert.SerializeObject(payload);
        SendDataWithNamespace("BET_HISTORY", json);

        Debug.Log($"[HISTORY] Requesting page {page}");
    }

    internal void ReturnHome()
    {
        if (isBeingDestroyed) return;

        SendDataWithNamespace("HOME", "{}");
        Debug.Log("[HOME] Returning");
    }

    internal void ReceiveAuthToken(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[AUTH] Received token data");

        try
        {
            AuthTokenData data = JsonUtility.FromJson<AuthTokenData>(jsonData);
            SocketURI = data.socketURL;
            myAuth = data.cookie;

            if (!string.IsNullOrEmpty(data.nameSpace))
            {
                nameSpace = data.nameSpace;
            }

            if (string.IsNullOrEmpty(myAuth))
            {
                Debug.LogError("[AUTH] Empty token");
                ShowErrorAndBlock("Invalid authentication data");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AUTH] Parse error: {e.Message}");
            ShowErrorAndBlock("Authentication data format error");
        }
    }

    internal IEnumerator CloseSocket()
    {
        isExiting = true;
        Debug.Log("[SOCKET] Closing");

        RaycastBlocker?.SetActive(true);
        CleanupRoutines();

        if (manager != null)
        {
            try
            {
                manager.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SOCKET] Error closing manager: {e.Message}");
            }
            manager = null;
        }

        yield return new WaitForSeconds(0.5f);

#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("[PLATFORM] Sending OnExit");
        JSManager?.SendCustomMessage("OnExit");
#endif
    }
    #endregion

    #region Private Helpers
    private void SendDataWithNamespace(string eventName, string json = null)
    {
        if (isBeingDestroyed) return;

        if (gameSocket != null && gameSocket.IsOpen)
        {
            if (json != null)
            {
                gameSocket.Emit(eventName, json);
                Debug.Log($"[EMIT] {eventName}: {json}");
            }
            else
            {
                gameSocket.Emit(eventName);
                Debug.Log($"[EMIT] {eventName}");
            }
        }
        else
        {
            Debug.LogWarning($"[EMIT] Socket not connected for '{eventName}'");
        }
    }

    private void ShowErrorAndBlock(string message)
    {
        if (isBeingDestroyed) return;

        uiController?.ShowErrorPopup(message);
        RaycastBlocker?.SetActive(true);
    }

    private void CleanupRoutines()
    {
        ResetPingRoutine();

        if (initTimeoutRoutine != null)
        {
            StopCoroutine(initTimeoutRoutine);
            initTimeoutRoutine = null;
        }

        if (disconnectTimerCoroutine != null)
        {
            StopCoroutine(disconnectTimerCoroutine);
            disconnectTimerCoroutine = null;
        }

        if (focusCheckRoutine != null)
        {
            StopCoroutine(focusCheckRoutine);
            focusCheckRoutine = null;
        }
    }
    #endregion
}
