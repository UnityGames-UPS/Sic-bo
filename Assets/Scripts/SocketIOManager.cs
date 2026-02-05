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

        // Room events
        gameSocket.On<string>("room:joined", OnRoomJoined);

        gameSocket.On<string>("request", OnRequest);

        // System events
        gameSocket.On<string>("pong", OnPongReceived);
        gameSocket.On<string>("error", OnInternalError);
        gameSocket.On<string>("force-disconnect", OnForceDisconnect);

        Debug.Log("[EVENTS] All handlers registered");
    }
    #endregion

    #region Event Handlers - Connection
    private void OnConnected(ConnectResponse resp)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[CONNECTED] Socket ID: {resp.sid}");

        isConnected = true;
        hasEverConnected = true;
        isWaitingForInitData = true;

        RaycastBlocker?.SetActive(false);
        uiController?.CloseReconnectPopup();

        StartPingPongRoutine();
    }

    private void OnDisconnected()
    {
        if (isBeingDestroyed || isExiting) return;

        Debug.LogWarning("[DISCONNECTED] Connection lost");

        isConnected = false;
        ResetPingRoutine();

        if (hasEverConnected)
        {
            uiController?.ShowDisconnectPopup();
        }
        else
        {
            ShowErrorAndBlock("Failed to connect to server");
        }
    }

    private void OnError(Error error)
    {
        if (isBeingDestroyed) return;

        Debug.LogError($"[ERROR] Socket error: {error.message}");
    }

    private void OnInternalError(string data)
    {
        if (isBeingDestroyed) return;

        Debug.LogError($"[ERROR] Internal: {data}");
        try
        {
            var errorObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(data);
            if (errorObj != null && errorObj.ContainsKey("code"))
            {
                string code = errorObj["code"].ToString();
                // Don't show popup for REQUEST_ERROR
                if (code != "REQUEST_ERROR")
                {
                    ShowErrorAndBlock($"Server error: {code}");
                }
            }
        }
        catch
        {
            // If we can't parse it, it might be critical
            Debug.LogError($"[ERROR] Could not parse error: {data}");
        }
    }

    private void OnForceDisconnect(string data)
    {
        if (isBeingDestroyed) return;

        Debug.LogWarning("[FORCE_DISCONNECT] Removed by admin or another device");
        uiController?.ShowAnotherDevicePopup();

        StartCoroutine(CloseSocket());
    }
    #endregion

    #region Event Handlers - Game Init
    private void OnInitData(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[RECEIVED] game:init");
        if (enableVerboseLogging) Debug.Log($"[game:init]: {jsonData}");

        try
        {
            SicBoRoot root = JsonConvert.DeserializeObject<SicBoRoot>(jsonData);

            if (root == null)
            {
                Debug.LogError("[INIT] Failed to parse init data");
                return;
            }

            InitialData = root.gameData;
            PlayerData = root.player;

            if (InitialData != null && PlayerData != null)
            {
                IsInitialized = true;
                isWaitingForInitData = false;

                Debug.Log($"[INIT] Complete - Player: {PlayerData.username}, Balance: {PlayerData.balance}");

                gameManager.OnInitDataReceived();

#if UNITY_WEBGL && !UNITY_EDITOR
                JSManager?.SendCustomMessage("OnEnter");
#endif
            }
            else
            {
                Debug.LogError("[INIT] Missing game data or player data");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[INIT] Parse error: {e.Message}\n{e.StackTrace}");
        }
    }
    #endregion

    #region Event Handlers - Room
    private void OnRoomJoined(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[RECEIVED] room:joined");
        if (enableVerboseLogging) Debug.Log($"[room:joined]: {jsonData}");
    }
    #endregion

    #region Event Handlers - Round
    private void OnRoundStart(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[RECEIVED] game:round_start");
        if (enableVerboseLogging) Debug.Log($"[game:round_start]: {jsonData}");

        try
        {
            RoundStartData data = JsonConvert.DeserializeObject<RoundStartData>(jsonData);
            if (data != null)
            {
                gameManager.OnRoundStart(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROUND_START] Parse error: {e.Message}");
        }
    }

    private void OnBettingTimer(string jsonData)
    {
        if (isBeingDestroyed) return;

        try
        {
            TimerData data = JsonConvert.DeserializeObject<TimerData>(jsonData);
            if (data != null)
            {
                gameManager.OnBettingTimer(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TIMER] Parse error: {e.Message}");
        }
    }

    private void OnBonus(string jsonData)
    {
        if (isBeingDestroyed) return;

        if (enableVerboseLogging) Debug.Log($"[game:bonus]: {jsonData}");

        try
        {
            BonusData data = JsonConvert.DeserializeObject<BonusData>(jsonData);
            if (data != null)
            {
                gameManager.OnBonus(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[BONUS] Parse error: {e.Message}");
        }
    }

    private void OnDiceResult(string jsonData)
    {
        if (isBeingDestroyed) return;

        if (enableVerboseLogging) Debug.Log($"[game:dice_result]: {jsonData}");

        try
        {
            DiceResultData data = JsonConvert.DeserializeObject<DiceResultData>(jsonData);
            if (data != null)
            {
                gameManager.OnDiceResult(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DICE_RESULT] Parse error: {e.Message}");
        }
    }

    private void OnBetPlaced(string jsonData)
    {
        if (isBeingDestroyed) return;

        try
        {
            BetPlacedData data = JsonConvert.DeserializeObject<BetPlacedData>(jsonData);
            if (data != null)
            {
                gameManager.OnBetPlaced(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[BET_PLACED] Parse error: {e.Message}");
        }
    }

    private void OnCashout(string jsonData)
    {
        if (isBeingDestroyed) return;

        if (enableVerboseLogging) Debug.Log($"[game:cashout]: {jsonData}");

        try
        {
            CashoutData data = JsonConvert.DeserializeObject<CashoutData>(jsonData);
            if (data != null)
            {
                gameManager.OnCashout(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CASHOUT] Parse error: {e.Message}");
        }
    }

    private void OnLobbyCount(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[RECEIVED] game:lobby_count");
        if (enableVerboseLogging) Debug.Log($"[game:lobby_count]: {jsonData}");

        try
        {
            LobbyCountData data = JsonConvert.DeserializeObject<LobbyCountData>(jsonData);
            if (data != null)
            {
                gameManager.OnLobbyCount(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LOBBY_COUNT] Parse error: {e.Message}");
        }
    }
    #endregion

    #region Event Handlers - Request Response
    private void OnRequest(string jsonData)
    {
        if (isBeingDestroyed) return;

        Debug.Log("[RECEIVED] request");
        if (enableVerboseLogging) Debug.Log($"[request]: {jsonData}");

        try
        {
            SicBoRoot root = JsonConvert.DeserializeObject<SicBoRoot>(jsonData);

            if (root == null || !root.success)
            {
                Debug.LogWarning("[REQUEST] Request failed or null response");
                return;
            }

            if (root.payload == null)
            {
                Debug.LogWarning("[REQUEST] Null payload");
                return;
            }

            // Handle different response types based on payload content

            // Room join response
            if (!string.IsNullOrEmpty(root.payload.level))
            {
                Debug.Log($"[REQUEST] JOIN_LEVEL response: {root.payload.level}");
                gameManager.OnRoomJoinedWithData(root.payload);

                // If round state exists, start round immediately
                if (root.payload.roundState != null)
                {
                    var roundData = new RoundStartData
                    {
                        roundId = root.payload.roundState.roundId,
                        startedAt = root.payload.roundState.startedAt,
                        bettingEndTime = root.payload.roundState.bettingEndTime,
                        serverTime = root.payload.roundState.serverTime,
                        playerCount = root.payload.playerCount
                    };
                    gameManager.OnRoundStart(roundData);
                }
            }
            // Bet response
            else if (root.payload.balance > 0 && !string.IsNullOrEmpty(root.payload.betId))
            {
                Debug.Log($"[REQUEST] PLACE_BET response: balance={root.payload.balance}");
                gameManager.OnBalanceUpdated(root.payload.balance);
            }
            // History response
            else if (root.payload.history != null)
            {
                Debug.Log($"[REQUEST] GET_HISTORY response");
                gameManager.OnHistoryReceived(root.payload.history, root.payload.meta);
            }
            // Home response
            else if (root.payload.lobby != null)
            {
                Debug.Log($"[REQUEST] HOME response");
                // Home response handled - just logged
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[REQUEST] Parse error: {e.Message}");
        }
    }
    #endregion

    #region Ping/Pong
    private void StartPingPongRoutine()
    {
        ResetPingRoutine();
        if (gameObject.activeInHierarchy)
        {
            PingRoutine = StartCoroutine(PingPongCheck());
        }
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

    private IEnumerator PingPongCheck()
    {
        while (!isBeingDestroyed && isConnected)
        {
            yield return new WaitForSeconds(pingInterval);

            if (!isConnected || isBeingDestroyed) yield break;

            if (waitingForPong)
            {
                missedPongs++;
                Debug.LogWarning($"[PING] Missed pong #{missedPongs}");

                if (missedPongs >= MaxMissedPongs)
                {
                    Debug.LogError("[PING] Connection lost - too many missed pongs");
                    OnDisconnected();
                    yield break;
                }
            }

            SendDataWithNamespace("ping");
            waitingForPong = true;
        }
    }

    private void OnPongReceived(string data)
    {
        if (isBeingDestroyed) return;

        lastPongTime = Time.time;
        waitingForPong = false;
        missedPongs = 0;
    }
    #endregion

    #region Timeouts
    private IEnumerator ConnectionAndInitTimeout()
    {
        float elapsed = 0f;
        float timeout = 30f;

        while (elapsed < timeout && !isBeingDestroyed)
        {
            if (IsInitialized)
            {
                Debug.Log("[TIMEOUT] Init successful");
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isBeingDestroyed && !IsInitialized)
        {
            Debug.LogError("[TIMEOUT] Init timeout");
            ShowErrorAndBlock("Connection timeout. Please refresh.");
        }
    }

    private IEnumerator FocusTimeoutCheck()
    {
        while (!hasFocus && !isBeingDestroyed)
        {
            float timeInBackground = Time.time - focusLostTime;

            if (timeInBackground >= maxBackgroundTime)
            {
                Debug.LogWarning("[FOCUS] Max background time exceeded");
                OnDisconnected();
                yield break;
            }

            yield return new WaitForSeconds(5f);
        }
    }
    #endregion

    #region Public API - Client Action
    internal void JoinLevel(string level)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[JOIN] Sending JOIN_LEVEL for: {level}");

        var requestData = new
        {
            type = "JOIN_LEVEL",
            payload = new { level = level }
        };

        string jsonPayload = JsonConvert.SerializeObject(requestData);
        if (enableVerboseLogging) Debug.Log($"[JOIN] Request: {jsonPayload}");

        SendDataWithNamespace("request", jsonPayload);
    }

    internal void PlaceBet(string betType, string betOption, int chipIndex, string currentRoom)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[BET] Placing: {betOption} with chip {chipIndex}");

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
        SendDataWithNamespace("request", jsonPayload);
    }
    
    internal void CancelBet()
    {
        SendSimpleRequest("CANCEL_BET");
    }

    internal void DoubleBet(string currentRoom)
    {
        SendSimpleRequest("DOUBLE_BET");
    }

    internal void RepeatBet()
    {
        SendSimpleRequest("REPEAT_BET");
    }

    internal void UndoBet()
    {
        SendSimpleRequest("UNDO_BET");
    }

    internal void RequestHistory(int page)
    {
        if (isBeingDestroyed) return;

        var requestData = new
        {
            type = "GET_HISTORY",
            payload = new { page = page }
        };

        string jsonPayload = JsonConvert.SerializeObject(requestData);
        SendDataWithNamespace("request", jsonPayload);
    }

    internal void ReturnHome()
    {
        SendSimpleRequest("HOME");
    }

    private void SendSimpleRequest(string requestType)
    {
        if (isBeingDestroyed) return;

        var requestData = new
        {
            type = requestType,
            payload = new { }
        };

        string jsonPayload = JsonConvert.SerializeObject(requestData);
        SendDataWithNamespace("request", jsonPayload);
    }
    #endregion

    #region Platform Communication
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