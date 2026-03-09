using Best.SocketIO;
using Best.SocketIO.Events;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SocketIOManager : MonoBehaviour
{
    #region Serialized Fields
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UIController uiController;
    [SerializeField] internal JSFunctCalls JSManager;

    [Header("Testing")]
    [SerializeField] private string testToken;

    [Header("Blocker")]
    [SerializeField] private GameObject RaycastBlocker;

    [Header("Settings")]
    [SerializeField] private float disconnectDelay = 60f;

    [Header("Debug — disable in production builds")]
    [SerializeField] private bool showDebugLogs = false;
    #endregion

    #region Internal Properties
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
    private string savedToken = null;
    #endregion

    #region Private Fields - State
    private bool isConnected = false;
    private bool hasEverConnected = false;
    private bool isExiting = false;
    private bool isWaitingForInitData = false;
    private bool isBeingDestroyed = false;
    private bool hasFocus = true;
    private float focusLostTime = 0f;
    private const float maxBackgroundTime = 120f;
    private bool hasForceDisconnected = false;
    #endregion

    #region Private Fields - Room Tracking
    private string CurrentRoomId = null;
    private bool isPendingHome = false;
    #endregion

    #region Private Fields - Ping/Pong
    private float lastPongTime = 0f;
    private const float pingInterval = 2f;
    private bool waitingForPong = false;
    private int missedPongs = 0;
    private const int MaxMissedPongs = 15;
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
        uiController?.ShowLoadingScreen("Initializing Game...");

        if (!ValidateToken()) return;
        OpenSocket();
    }

    private void OnDestroy()
    {
        isBeingDestroyed = true;
        isExiting = true;
        CleanupRoutines();

        if (manager != null)
        {
            try { manager.Close(); }
            catch (Exception e) { if (showDebugLogs) Debug.LogWarning($"[SOCKET] Close error: {e.Message}"); }
            manager = null;
        }

        gameSocket = null;
    }

    void CloseGame()
    {
        StartCoroutine(CloseSocket());
    }

    private void OnApplicationFocus(bool focus)
    {
        if (isBeingDestroyed) return;

        hasFocus = focus;

        if (!focus)
        {
            focusLostTime = Time.time;
            if (focusCheckRoutine == null && gameObject.activeInHierarchy)
                focusCheckRoutine = StartCoroutine(FocusTimeoutCheck());
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
            ShowErrorAndBlock("Test token is required in editor mode");
            return false;
        }
#endif
        return true;
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
            StartCoroutine(WaitForAuthToken(options));
#else
        options.Auth = (manager, socket) => new { token = testToken };
        savedToken = testToken;
        SetupSocketManager(options);
#endif
    }

    private IEnumerator WaitForAuthToken(SocketOptions options)
    {
        float elapsed = 0f;
        const float timeout = 15f;

        while ((myAuth == null || SocketURI == null) && elapsed < timeout && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isBeingDestroyed) yield break;

        if (myAuth == null)
        {
            ShowErrorAndBlock("Authentication failed. Please refresh the page.");
            yield break;
        }

        if (SocketURI == null)
        {
            ShowErrorAndBlock("Connection configuration failed. Please refresh.");
            yield break;
        }

        options.Auth = (manager, socket) => new { token = myAuth };
        savedToken = myAuth;
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

        gameSocket = string.IsNullOrEmpty(nameSpace)
            ? this.manager.Socket
            : this.manager.GetSocket("/" + nameSpace);

        RegisterEventHandlers();
        manager.Open();

        if (gameObject.activeInHierarchy && !isBeingDestroyed)
            initTimeoutRoutine = StartCoroutine(ConnectionAndInitTimeout());
    }

    private void RegisterEventHandlers()
    {
        gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
        gameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected);
        gameSocket.On<Error>(SocketIOEventTypes.Error, OnError);

        gameSocket.On<string>("game:init", OnInitData);
        gameSocket.On<string>("game:round_start", OnRoundStart);
        gameSocket.On<string>("game:betting_timer", OnBettingTimer);
        gameSocket.On<string>("game:bonus", OnBonus);
        gameSocket.On<string>("game:dice_result", OnDiceResult);
        gameSocket.On<string>("game:bet_placed", OnBetPlaced);
        gameSocket.On<string>("game:cashout", OnCashout);
        gameSocket.On<string>("game:round_end", OnRoundEnd);
        gameSocket.On<string>("game:lobby_count", OnLobbyCount);
        gameSocket.On<string>("game:leaderboard_update", OnLeaderboardUpdate);
        gameSocket.On<string>("room:joined", OnRoomJoined);
        gameSocket.On<string>("room:left", OnRoomLeft);
        gameSocket.On<string>("pong", OnPongReceived);
        gameSocket.On<string>("error", OnInternalError);
        gameSocket.On<string>("force-disconnect", OnForceDisconnect);
    }
    #endregion

    #region Event Handlers - Connection
    private void OnConnected(ConnectResponse resp)
    {
        if (isBeingDestroyed) return;

        if (initTimeoutRoutine != null)
        {
            StopCoroutine(initTimeoutRoutine);
            initTimeoutRoutine = null;
        }

        isConnected = true;
        hasEverConnected = true;
        missedPongs = 0;
        lastPongTime = Time.time;

        if (showDebugLogs) Debug.Log("[SOCKET] Connected");

        StartPingPongChecks();

        if (!IsInitialized)
        {
            isWaitingForInitData = true;
            if (gameObject.activeInHierarchy && !isBeingDestroyed)
                initTimeoutRoutine = StartCoroutine(InitDataTimeout());
        }
    }

    private void OnDisconnected()
    {
        if (isBeingDestroyed || isExiting || hasForceDisconnected) return;

        isConnected = false;
        IsInitialized = false;
        ResetPingRoutine();

        if (showDebugLogs) Debug.LogWarning("[SOCKET] Disconnected");

        if (hasEverConnected && !isExiting)
            uiController?.ShowDisconnectPopup();

        if (disconnectTimerCoroutine == null && gameObject.activeInHierarchy && !isBeingDestroyed)
            disconnectTimerCoroutine = StartCoroutine(DisconnectTimer());
    }

    private void OnError(Error error)
    {
        if (isBeingDestroyed) return;
        // Always log socket-level errors — these are exceptional, not high-frequency
        Debug.LogError($"[SOCKET] Error: {error.message}");

        // If a HOME request was pending and the server errored instead of acking,
        // the ACK will never arrive — recover so the loading screen is not stuck.
        if (isPendingHome)
        {
            isPendingHome = false;
            Debug.LogWarning("[SOCKET] Error received while HOME was pending — forcing leave acknowledgement to unblock loading screen.");
            gameManager?.OnLeaveAcknowledged();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager?.SendCustomMessage("error");
#endif
    }
    #endregion

    #region Event Handlers - Room
    private void OnRoomJoined(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[RESPONSE] room:joined {json}");

        try
        {
            RoomPayload payload = JsonConvert.DeserializeObject<RoomPayload>(json);

            if (!string.IsNullOrEmpty(payload?.roomId))
                CurrentRoomId = payload.roomId;

            gameManager.OnRoomJoinedWithData(payload);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RESPONSE] room:joined parse error: {e.Message}");
        }
    }

    private void OnRoomLeft(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[RESPONSE] room:left {json}");

        try
        {
            RoomPayload payload = JsonConvert.DeserializeObject<RoomPayload>(json);

            if (payload == null) return;
            if (!string.IsNullOrEmpty(payload.roomId) &&
                !string.IsNullOrEmpty(CurrentRoomId) &&
                payload.roomId != CurrentRoomId)
            {
                if (showDebugLogs) Debug.Log($"[RESPONSE] room:left ignored — different room ({payload.roomId} vs {CurrentRoomId})");
                return;
            }

            uiController?.UpdatePlayerCountInLevel(payload.playerCount);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RESPONSE] room:left parse error: {e.Message}");
        }
    }
    #endregion

    #region Event Handlers - Game Events
    private void OnInitData(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[RESPONSE] game:init {json}");

        isWaitingForInitData = false;
        if (initTimeoutRoutine != null)
        {
            StopCoroutine(initTimeoutRoutine);
            initTimeoutRoutine = null;
        }

        try
        {
            SicBoRoot response = JsonConvert.DeserializeObject<SicBoRoot>(json);

            if (response?.gameData != null && response.player != null)
            {
                InitialData = response.gameData;
                PlayerData = response.player;
                IsInitialized = true;

#if UNITY_WEBGL && !UNITY_EDITOR
                JSManager?.SendCustomMessage("OnEnter");
#endif

                uiController?.HideLoadingScreen();
                RaycastBlocker?.SetActive(false);
                gameManager.OnInitDataReceived();
            }
            else
            {
                Debug.LogError($"[RESPONSE] game:init missing fields — gameData:{response?.gameData != null} player:{response?.player != null}");
                ShowErrorAndBlock("Failed to initialize game session.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RESPONSE] game:init parse error: {e.Message}");
            ShowErrorAndBlock("Failed to initialize game session.");
        }
    }

    private void OnRoundStart(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[RESPONSE] game:round_start {json}");
        TryDeserializeAndForward<RoundStartData>(json, gameManager.OnRoundStart, "round_start");
    }

    // High-frequency: fires every second — never log raw JSON in production
    private void OnBettingTimer(string json)
    {
        if (isBeingDestroyed) return;
        TryDeserializeAndForward<TimerData>(json, gameManager.OnBettingTimer, "betting_timer");
    }

    private void OnBonus(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[RESPONSE] game:bonus {json}");
        TryDeserializeAndForward<BonusData>(json, gameManager.OnBonus, "bonus");
    }

    private void OnDiceResult(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[RESPONSE] game:dice_result {json}");
        TryDeserializeAndForward<DiceResultData>(json, gameManager.OnDiceResult, "dice_result");
    }

    // High-frequency: fires on every player bet in the room — never log raw JSON in production
    private void OnBetPlaced(string json)
    {
        if (isBeingDestroyed) return;
        TryDeserializeAndForward<BetPlacedData>(json, gameManager.OnBetPlaced, "bet_placed");
    }

    private void OnCashout(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[RESPONSE] game:cashout {json}");
        TryDeserializeAndForward<CashoutData>(json, gameManager.OnCashout, "cashout");
    }

    private void OnRoundEnd(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[RESPONSE] game:round_end {json}");
        TryDeserializeAndForward<RoundEndPayload>(json, gameManager.OnRoundEnd, "round_end");
    }

    // High-frequency: fires regularly — never log raw JSON in production
    private void OnLobbyCount(string json)
    {
        if (isBeingDestroyed) return;
        TryDeserializeAndForward<LobbyCountData>(json, gameManager.OnLobbyCount, "lobby_count");
    }

    private void OnLeaderboardUpdate(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[RESPONSE] game:leaderboard_update {json}");
        TryDeserializeAndForward<CashoutData>(json, gameManager.OnLeaderboardUpdate, "leaderboard_update");
    }

    private void TryDeserializeAndForward<T>(string json, Action<T> handler, string label)
    {
        try
        {
            T data = JsonConvert.DeserializeObject<T>(json);
            handler(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RESPONSE] game:{label} parse error: {e.Message}");
        }
    }
    #endregion

    #region Event Handlers - System
    private void OnPongReceived(string json)
    {
        if (isBeingDestroyed) return;

        waitingForPong = false;
        lastPongTime = Time.time;

        if (missedPongs >= 2)
            uiController?.CloseReconnectPopup();

        missedPongs = 0;
    }

    private void OnInternalError(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.LogError($"[RESPONSE] error: {json}");

        string message = TryParseErrorMessage(json);

        bool isGameLogicError = !string.IsNullOrEmpty(message) && (
            message.Contains("Limit") ||
            message.Contains("limit") ||
            message.Contains("Insufficient") ||
            message.Contains("insufficient") ||
            message.Contains("not active") ||
            message.Contains("locked") ||
            message.Contains("not found") ||
            message.Contains("Invalid bet") ||
            message.Contains("Betting")
        );

        if (isGameLogicError)
            uiController?.ShowInGamePopup(message);
        else
            ShowErrorAndBlock(string.IsNullOrEmpty(message) ? "An error occurred. Please refresh." : message, showDisconnectAfter: true);
    }

    private string TryParseErrorMessage(string json)
    {
        if (string.IsNullOrEmpty(json)) return string.Empty;
        if (!json.TrimStart().StartsWith("{")) return json;

        try
        {
            var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (obj != null)
            {
                if (obj.TryGetValue("message", out var msg) && msg != null) return msg.ToString();
                if (obj.TryGetValue("error", out var err) && err != null) return err.ToString();
            }
        }
        catch { }

        return json;
    }

    private void OnForceDisconnect(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.LogWarning("[SOCKET] Force disconnect received");

        hasForceDisconnected = true;
        isExiting = true;
        CleanupRoutines();

        uiController?.ShowAnotherDevicePopup();
    }
    #endregion

    #region Internal API - Emit Actions
    internal void JoinLevel(string level)
    {
        if (!CanEmit()) return;
        EmitRequest("JOIN_LEVEL", new JoinLevelPayload { level = level }, OnJoinLevelAck);
    }

    internal void PlaceBet(string betType, string betOption, int chipIndex, string level)
    {
        if (!CanEmit()) return;
        EmitRequest("PLACE_BET", new PlaceBetPayload
        {
            amountIndex = chipIndex,
            betType = betType,
            betOption = betOption
        }, OnPlaceBetAck);
    }

    internal void DoubleBet(string level) => EmitSimpleRequest("DOUBLE_BET", OnDoubleBetAck);
    internal void RepeatBet() => EmitSimpleRequest("REPEAT_BET", OnRepeatBetAck);
    internal void UndoBet() => EmitSimpleRequest("UNDO_BET", OnUndoBetAck);
    internal void CancelBet() => EmitSimpleRequest("CANCEL_BET", OnCancelBetAck);

    internal void RequestHistory(int page)
    {
        if (!CanEmit()) return;
        EmitRequest("BET_HISTORY", new HistoryRequestPayload { page = page }, OnHistoryAck);
    }

    internal void ReturnHome()
    {
        isPendingHome = true;
        EmitSimpleRequest("HOME", OnHomeAck);
    }

    internal IEnumerator CloseSocket()
    {
        isExiting = true;
        RaycastBlocker?.SetActive(true);
        CleanupRoutines();

        if (manager != null)
        {
            try { manager.Close(); }
            catch (Exception e) { if (showDebugLogs) Debug.LogWarning($"[SOCKET] Error closing: {e.Message}"); }
            manager = null;
        }

        yield return new WaitForSeconds(0.5f);

#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager?.SendCustomMessage("OnExit");
#endif

        yield return new WaitForSeconds(0.5f);
        Application.Quit();
    }
    #endregion

    #region Acknowledgement Handlers
    private void OnJoinLevelAck(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[ACK] JOIN_LEVEL {json}");

        try
        {
            SicBoRoot response = JsonConvert.DeserializeObject<SicBoRoot>(json);
            if (response != null && response.success && response.payload != null)
                gameManager.OnRoomJoinedWithData(response.payload);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] JOIN_LEVEL parse error: {e.Message}");
        }
    }

    private void OnPlaceBetAck(string json) => HandleBetAck(json, "PLACE_BET");
    private void OnDoubleBetAck(string json) => HandleBetAck(json, "DOUBLE_BET");
    private void OnRepeatBetAck(string json) => HandleBetAck(json, "REPEAT_BET");
    private void OnUndoBetAck(string json) => HandleBetAck(json, "UNDO_BET");
    private void OnCancelBetAck(string json) => HandleBetAck(json, "CANCEL_BET");

    private void HandleBetAck(string json, string label)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[ACK] {label} {json}");

        try
        {
            BetAckResponse response = JsonConvert.DeserializeObject<BetAckResponse>(json);
            gameManager.OnBetActionResponse(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] {label} parse error: {e.Message}");
            gameManager.OnBetActionResponse(null);
        }
    }

    private void OnHistoryAck(string json)
    {
        if (isBeingDestroyed) return;
        if (showDebugLogs) Debug.Log($"[ACK] BET_HISTORY {json}");

        try
        {
            HistoryResponse response = JsonConvert.DeserializeObject<HistoryResponse>(json);

            if (response != null && response.success && response.payload?.history != null && response.payload.meta != null)
                gameManager.OnHistoryReceived(response.payload.history, response.payload.meta);
            else
                if (showDebugLogs) Debug.LogWarning("[ACK] BET_HISTORY: invalid response or empty payload");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] BET_HISTORY parse error: {e.Message}");
        }
    }

    private void OnHomeAck(string json)
    {
        if (isBeingDestroyed) return;
        isPendingHome = false;
        if (showDebugLogs) Debug.Log($"[ACK] HOME {json}");

        try
        {
            SicBoRoot response = JsonConvert.DeserializeObject<SicBoRoot>(json);

            if (response != null && response.success && response.payload != null)
            {
                if (!string.IsNullOrEmpty(response.payload.roomId))
                    CurrentRoomId = response.payload.roomId;

                if (response.payload.balance >= 0)
                {
                    PlayerData = new Player { balance = response.payload.balance, username = PlayerData?.username };
                    uiController?.UpdateBalance(PlayerData.balance);
                }

                if (response.payload.lobby != null)
                    gameManager.OnLobbyCount(new LobbyCountData { lobby = response.payload.lobby });

                gameManager?.OnLeaveAcknowledged();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] HOME parse error: {e.Message}");
        }
    }
    #endregion

    #region Platform Communication
    internal void ReceiveAuthToken(string jsonData)
    {
        if (isBeingDestroyed) return;

        try
        {
            AuthTokenData data = JsonConvert.DeserializeObject<AuthTokenData>(jsonData);
            SocketURI = data.socketURL;
            myAuth = data.cookie;

            if (string.IsNullOrEmpty(myAuth))
            {
                Debug.LogError("[AUTH] Empty token received");
                ShowErrorAndBlock("Invalid authentication data");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AUTH] Parse error: {e.Message}");
            ShowErrorAndBlock("Authentication data format error");
        }
    }
    #endregion

    #region Coroutines
    private IEnumerator ConnectionAndInitTimeout()
    {
        float elapsed = 0f;
        const float connectionTimeout = 15f;

        while (!isConnected && elapsed < connectionTimeout && !isExiting && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isBeingDestroyed) yield break;

        if (!isConnected && !isExiting)
        {
            Debug.LogError("[SOCKET] Connection timeout");
            ShowErrorAndBlock("Connection failed. Please check your network.", showDisconnectAfter: true);
        }

        initTimeoutRoutine = null;
    }

    private IEnumerator InitDataTimeout()
    {
        float elapsed = 0f;
        const float initTimeout = 10f;

        while (isWaitingForInitData && elapsed < initTimeout && !isExiting && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isBeingDestroyed) yield break;

        if (isWaitingForInitData && !isExiting)
        {
            Debug.LogError("[SOCKET] Init data timeout");
            ShowErrorAndBlock("Failed to receive game data. Please refresh.", showDisconnectAfter: true);
        }

        initTimeoutRoutine = null;
    }

    private IEnumerator PingPongCheck()
    {
        while (!isBeingDestroyed && gameObject.activeInHierarchy)
        {
            yield return new WaitForSeconds(pingInterval);

            if (isBeingDestroyed || isExiting || !isConnected) break;

            if (waitingForPong)
            {
                missedPongs++;

                if (missedPongs == 2)
                    uiController?.ShowReconnectPopup();

                if (missedPongs >= MaxMissedPongs)
                {
                    if (showDebugLogs) Debug.LogError("[PING-PONG] Connection lost - max pongs missed");
                    isConnected = false;
                    uiController?.ShowDisconnectPopup();
                    break;
                }
            }

            waitingForPong = true;
            EmitSimpleEvent("ping");
        }
    }

    private IEnumerator DisconnectTimer()
    {
        float elapsed = 0f;

        while (elapsed < disconnectDelay && !isConnected && !isExiting && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isBeingDestroyed) yield break;

        if (!isConnected && !isExiting)
        {
            Debug.LogError("[SOCKET] Disconnect timeout - forcing exit");
            ShowErrorAndBlock("You have been disconnected. Please refresh.", showDisconnectAfter: true);
        }

        disconnectTimerCoroutine = null;
    }

    private IEnumerator FocusTimeoutCheck()
    {
        while (!hasFocus && !isExiting && !isBeingDestroyed)
        {
            if (Time.time - focusLostTime >= maxBackgroundTime)
            {
                if (showDebugLogs) Debug.LogError("[SOCKET] Background timeout");
                isConnected = false;
                ResetPingRoutine();

                if (manager != null)
                {
                    try { manager.Close(); }
                    catch (Exception e) { if (showDebugLogs) Debug.LogWarning($"[SOCKET] Focus close error: {e.Message}"); }
                }

                ShowErrorAndBlock("Game timed out due to inactivity. Please refresh.", showDisconnectAfter: true);
                focusCheckRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(1f);
        }

        focusCheckRoutine = null;
    }
    #endregion

    #region Private Helpers
    private bool CanEmit() => !isBeingDestroyed && gameSocket != null && gameSocket.IsOpen;

    private void EmitRequest<T>(string requestType, T payload, Action<string> ackCallback)
    {
        if (!CanEmit()) return;

        try
        {
            string json = JsonConvert.SerializeObject(new GameRequest { type = requestType, payload = payload });
            if (showDebugLogs) Debug.Log($"[EMIT] {requestType} {json}");
            gameSocket.ExpectAcknowledgement<string>(ackCallback).Emit("request", json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EMIT] {requestType} error: {e.Message}");
        }
    }

    private void EmitSimpleRequest(string requestType, Action<string> ackCallback)
    {
        EmitRequest(requestType, new EmptyPayload(), ackCallback);
    }

    private void EmitSimpleEvent(string eventName)
    {
        if (!CanEmit()) return;
        gameSocket.Emit(eventName);
    }

    private void ShowErrorAndBlock(string message, bool showDisconnectAfter = false)
    {
        if (isBeingDestroyed) return;
        uiController?.ShowErrorPopup(message, "Error", showDisconnectAfter);
    }

    private void StartPingPongChecks()
    {
        ResetPingRoutine();
        if (gameObject.activeInHierarchy && !isBeingDestroyed)
            PingRoutine = StartCoroutine(PingPongCheck());
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

    private void CleanupRoutines()
    {
        ResetPingRoutine();

        if (initTimeoutRoutine != null) { StopCoroutine(initTimeoutRoutine); initTimeoutRoutine = null; }
        if (disconnectTimerCoroutine != null) { StopCoroutine(disconnectTimerCoroutine); disconnectTimerCoroutine = null; }
        if (focusCheckRoutine != null) { StopCoroutine(focusCheckRoutine); focusCheckRoutine = null; }
    }
    #endregion
}