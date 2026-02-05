using Best.SocketIO;
using Best.SocketIO.Events;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles all Socket.IO communication for Sic Bo multiplayer game
/// MERGED VERSION: Stable old structure + new request/response pattern with Dictionary error handling
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

    [Header("Debug")]
    [SerializeField] private bool enableVerboseLogging = true;
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
        Debug.Log($"[SOCKET] Connecting to: {TestSocketURI}");
#else
        this.manager = new SocketManager(new Uri(SocketURI), options);
        Debug.Log($"[SOCKET] Connecting to: {SocketURI}");
#endif

        gameSocket = string.IsNullOrEmpty(nameSpace) ?
            this.manager.Socket :
            this.manager.GetSocket("/" + nameSpace);

        Debug.Log($"[SOCKET] Using namespace: /{nameSpace}");

        RegisterEventHandlers();
        manager.Open();

        if (gameObject.activeInHierarchy && !isBeingDestroyed)
        {
            initTimeoutRoutine = StartCoroutine(ConnectionAndInitTimeout());
        }
    }

    private void RegisterEventHandlers()
    {
        Debug.Log("[EVENTS] ========== REGISTERING EVENT HANDLERS ==========");

        // Connection events
        gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
        gameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected);

        // ✅ FIX: Use Dictionary type for error handler
        gameSocket.On<Dictionary<string, object>>(SocketIOEventTypes.Error, OnErrorDict);

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

        // ✅ NEW: Request response handler (Dictionary type for flexibility)
        gameSocket.On<Dictionary<string, object>>("request", OnRequest);

        // System events
        gameSocket.On<string>("pong", OnPongReceived);
        gameSocket.On<Dictionary<string, object>>("error", OnInternalErrorDict);
        gameSocket.On<string>("alert", OnAlert);
        gameSocket.On<string>("another-device", OnAnotherDevice);

        Debug.Log("[EVENTS] All handlers registered successfully");
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
        Debug.Log($"[CONNECT] Socket ID: {gameSocket?.Id ?? "NULL"}");

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

        if (isExiting || !hasEverConnected || !gameObject.activeInHierarchy)
        {
            return;
        }

        // Start disconnect timer
        if (disconnectTimerCoroutine == null)
        {
            disconnectTimerCoroutine = StartCoroutine(DisconnectTimer());
        }
    }

    /// <summary>
    /// ✅ FIX: Handle Socket.IO Error event as Dictionary
    /// </summary>
    private void OnErrorDict(Dictionary<string, object> errorData)
    {
        if (isBeingDestroyed) return;

        try
        {
            string errorJson = JsonConvert.SerializeObject(errorData);
            Debug.LogError($"[ERROR] Socket error: {errorJson}");

            string errorMessage = "Unknown error";
            if (errorData.ContainsKey("message"))
            {
                errorMessage = errorData["message"]?.ToString() ?? "Unknown error";
            }
            else if (errorData.ContainsKey("error"))
            {
                errorMessage = errorData["error"]?.ToString() ?? "Unknown error";
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            JSManager?.SendCustomMessage("error");
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"[ERROR] Failed to process error data: {e.Message}");
        }
    }

    private void OnPongReceived(string data)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[PONG] Received");
        waitingForPong = false;
        missedPongs = 0;
        lastPongTime = Time.time;
    }

    private IEnumerator DisconnectTimer()
    {
        Debug.Log($"[DISCONNECT] Starting {disconnectDelay}s timer");

        uiController?.ShowReconnectPopup();
        float elapsed = 0f;

        while (elapsed < disconnectDelay && !isConnected && !isExiting && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(1f);
        }

        disconnectTimerCoroutine = null;

        if (isBeingDestroyed) yield break;

        if (!isConnected && !isExiting)
        {
            Debug.LogError("[DISCONNECT] Timeout reached");
            uiController?.ShowDisconnectPopup();
        }
        else
        {
            Debug.Log("[DISCONNECT] Reconnected before timeout");
            uiController?.CloseReconnectPopup();
        }
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

        Debug.Log($"[🎯 RECEIVED] game:init");
        Debug.Log($"[game:init]: \"{jsonData}\"");

        isWaitingForInitData = false;

        if (initTimeoutRoutine != null)
        {
            StopCoroutine(initTimeoutRoutine);
            initTimeoutRoutine = null;
        }

        try
        {
            SicBoRoot root = JsonConvert.DeserializeObject<SicBoRoot>(jsonData);

            if (root == null || root.gameData == null || root.player == null)
            {
                Debug.LogError("[INIT] Invalid data structure");
                ShowErrorAndBlock("Invalid game data received");
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

        Debug.Log($"[🎯 RECEIVED] room:joined");
        Debug.Log($"[room:joined]: \"{jsonData}\"");
    }

    /// <summary>
    /// ✅ NEW: Handle "request" response from server with new structure
    /// Server responds to our request events (JOIN_LEVEL, PLACE_BET, etc.) with this
    /// </summary>
    private void OnRequest(Dictionary<string, object> data)
    {
        if (isBeingDestroyed) return;

        try
        {
            Debug.Log($"[🎯 RECEIVED] request");

            // Convert dictionary to JSON string for logging
            string json = JsonConvert.SerializeObject(data);
            Debug.Log($"[request]: {json}");

            // Check if it's a successful response
            if (data.ContainsKey("success") && (bool)data["success"])
            {
                // Get the payload
                if (data.ContainsKey("payload"))
                {
                    var payloadJson = JsonConvert.SerializeObject(data["payload"]);
                    var payload = JsonConvert.DeserializeObject<RoomPayload>(payloadJson);

                    // Route to appropriate handler based on payload content
                    if (payload != null)
                    {
                        // JOIN_LEVEL response
                        if (!string.IsNullOrEmpty(payload.level))
                        {
                            Debug.Log($"[REQUEST] Join response for level: {payload.level}");
                            gameManager?.OnRoomJoinedWithData(payload);
                        }
                        // PLACE_BET response
                        else if (!string.IsNullOrEmpty(payload.betId))
                        {
                            Debug.Log($"[REQUEST] Bet confirmed: {payload.betId}");
                            gameManager?.OnBalanceUpdated(payload.balance);
                        }
                        // GET_HISTORY response
                        else if (payload.history != null)
                        {
                            Debug.Log($"[REQUEST] History received");
                            gameManager?.OnHistoryReceived(payload.history, payload.meta);
                        }
                        // HOME response
                        else if (payload.lobby != null)
                        {
                            Debug.Log($"[REQUEST] Returned to lobby");
                            // Handle lobby data if needed
                        }
                    }
                }
            }
            else
            {
                // Error response
                string message = data.ContainsKey("message") ? data["message"].ToString() : "Request failed";
                Debug.LogWarning($"[REQUEST] Error: {message}");
                uiController?.ShowNotification(message);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[REQUEST] Error processing response: {ex.Message}");
        }
    }

    private void OnRoundStart(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[🎯 RECEIVED] game:round_start");
        Debug.Log($"[game:round_start]: \"{jsonData}\"");

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

        Debug.Log($"[game:betting_timer]: \"{jsonData}\"");

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

        Debug.Log($"[🎯 RECEIVED] game:bonus");
        Debug.Log($"[game:bonus]: \"{jsonData}\"");

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

        Debug.Log($"[🎯 RECEIVED] game:dice_result");
        Debug.Log($"[game:dice_result]: \"{jsonData}\"");

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

        Debug.Log($"[🎯 RECEIVED] game:bet_placed");
        Debug.Log($"[game:bet_placed]: \"{jsonData}\"");

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

        Debug.Log($"[🎯 RECEIVED] game:cashout");
        Debug.Log($"[game:cashout]: \"{jsonData}\"");

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

        Debug.Log($"[🎯 RECEIVED] game:lobby_count");
        Debug.Log($"[game:lobby_count]: \"{jsonData}\"");

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

        Debug.Log($"[🎯 RECEIVED] game:round_end");
        Debug.Log($"[game:round_end]: \"{jsonData}\"");

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

    /// <summary>
    /// ✅ FIX: Handle internal "error" event as Dictionary
    /// </summary>
    private void OnInternalErrorDict(Dictionary<string, object> errorData)
    {
        if (isBeingDestroyed) return;

        try
        {
            string errorJson = JsonConvert.SerializeObject(errorData);
            Debug.LogError($"[ERROR] Internal: {errorJson}");

            string message = "Server error occurred";
            if (errorData.ContainsKey("message"))
            {
                message = errorData["message"]?.ToString() ?? message;
            }

            uiController?.ShowNotification(message);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ERROR] Failed to process internal error: {e.Message}");
            uiController?.ShowNotification("Server error occurred");
        }
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
    /// <summary>
    /// ✅ NEW: Join level with new request structure
    /// </summary>
    internal void JoinLevel(string level)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[JOIN] ===== SENDING JOIN_LEVEL =====");
        Debug.Log($"[JOIN] Level: {level}");

        var requestData = new
        {
            type = "JOIN_LEVEL",
            payload = new { level = level }
        };

        string jsonPayload = JsonConvert.SerializeObject(requestData);
        Debug.Log($"[JOIN] Request Data: {jsonPayload}");

        SendDataWithNamespace("request", jsonPayload);

        Debug.Log($"[JOIN] ===== JOIN_LEVEL SENT =====");
    }

    /// <summary>
    /// ✅ NEW: Place bet with new request structure
    /// </summary>
    internal void PlaceBet(string betType, string betOption, int chipIndex, string currentRoom)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[BET] ===== PLACING BET =====");

        var requestData = new
        {
            type = "PLACE_BET",
            payload = new
            {
                amountIndex = chipIndex,
                betType = betType,
                betOption = betOption
            }
        };

        string jsonPayload = JsonConvert.SerializeObject(requestData);
        Debug.Log($"[BET] Request: {jsonPayload}");

        SendDataWithNamespace("request", jsonPayload);
    }

    /// <summary>
    /// ✅ NEW: Cancel bet with new request structure
    /// </summary>
    internal void CancelBet()
    {
        if (isBeingDestroyed) return;

        Debug.Log("[CANCEL] Sending cancel all bets request");

        var requestData = new
        {
            type = "CANCEL_BET",
            payload = new { }
        };

        SendDataWithNamespace("request", JsonConvert.SerializeObject(requestData));
    }

    /// <summary>
    /// ✅ NEW: Double bet with new request structure
    /// </summary>
    internal void DoubleBet(string currentRoom)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[DOUBLE] Sending double bet request");

        var requestData = new
        {
            type = "DOUBLE_BET",
            payload = new { }
        };

        SendDataWithNamespace("request", JsonConvert.SerializeObject(requestData));
    }

    /// <summary>
    /// ✅ NEW: Repeat bet with new request structure
    /// </summary>
    internal void RepeatBet()
    {
        if (isBeingDestroyed) return;

        Debug.Log("[REPEAT] Sending repeat bet request");

        var requestData = new
        {
            type = "REPEAT_BET",
            payload = new { }
        };

        SendDataWithNamespace("request", JsonConvert.SerializeObject(requestData));
    }

    /// <summary>
    /// ✅ NEW: Undo bet with new request structure
    /// </summary>
    internal void UndoBet()
    {
        if (isBeingDestroyed) return;

        Debug.Log("[UNDO] Sending undo bet request");

        var requestData = new
        {
            type = "UNDO_BET",
            payload = new { }
        };

        SendDataWithNamespace("request", JsonConvert.SerializeObject(requestData));
    }

    /// <summary>
    /// ✅ NEW: Request history with new request structure
    /// </summary>
    internal void RequestHistory(int page)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[HISTORY] Requesting page {page}");

        var requestData = new
        {
            type = "GET_HISTORY",
            payload = new { page = page }
        };

        SendDataWithNamespace("request", JsonConvert.SerializeObject(requestData));
    }

    /// <summary>
    /// ✅ NEW: Return home with new request structure
    /// </summary>
    internal void ReturnHome()
    {
        if (isBeingDestroyed) return;

        Debug.Log("[HOME] Returning to lobby");

        var requestData = new
        {
            type = "HOME",
            payload = new { }
        };

        SendDataWithNamespace("request", JsonConvert.SerializeObject(requestData));
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
                if (enableVerboseLogging)
                {
                    Debug.Log($"[EMIT] {eventName}: {json}");
                }
            }
            else
            {
                gameSocket.Emit(eventName);
                if (enableVerboseLogging)
                {
                    Debug.Log($"[EMIT] {eventName}");
                }
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