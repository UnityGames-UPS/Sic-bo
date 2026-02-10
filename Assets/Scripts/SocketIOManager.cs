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
    private string savedToken = null;
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
            catch (Exception e) { Debug.LogWarning($"[SOCKET] Close error: {e.Message}"); }
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
        savedToken = testToken;
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
        gameSocket.On<string>("room:joined", OnRoomJoined);
        gameSocket.On<string>("pong", OnPongReceived);
        gameSocket.On<string>("error", OnInternalError);
        gameSocket.On<string>("force-disconnect", OnForceDisconnect);
    }
    #endregion

    #region Event Handlers - Connection
    private void OnConnected(ConnectResponse resp)
    {
        if (isBeingDestroyed) return;

        isConnected = true;
        hasEverConnected = true;
        missedPongs = 0;
        lastPongTime = Time.time;

        Debug.Log("[SOCKET] Connected");

        StartPingPongChecks();
    }

    private void OnDisconnected()
    {
        if (isBeingDestroyed || isExiting) return;

        isConnected = false;
        ResetPingRoutine();

        Debug.LogWarning("[SOCKET] Disconnected");

        if (hasEverConnected && !isExiting)
        {
            uiController?.ShowDisconnectPopup();
        }
    }

    private void OnError(Error error)
    {
        if (isBeingDestroyed) return;
        Debug.LogError($"[SOCKET] Error: {error.message}");
    }
    #endregion

    #region Event Handlers - Room
    private void OnRoomJoined(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] room:joined {json}");

        try
        {
            RoomPayload payload = JsonConvert.DeserializeObject<RoomPayload>(json);
            gameManager.OnRoomJoinedWithData(payload);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROOM] Parse error: {e.Message}");
        }
    }
    #endregion

    #region Event Handlers - Game Events
    private void OnInitData(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] game:init {json}");

        try
        {
            SicBoRoot response = JsonConvert.DeserializeObject<SicBoRoot>(json);

            if (response?.gameData != null && response.player != null)
            {
                InitialData = response.gameData;
                PlayerData = response.player;
                IsInitialized = true;

                if (initTimeoutRoutine != null)
                {
                    StopCoroutine(initTimeoutRoutine);
                    initTimeoutRoutine = null;
                }

                RaycastBlocker?.SetActive(false);
                gameManager.OnInitDataReceived();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[INIT] Parse error: {e.Message}");
        }
    }

    private void OnRoundStart(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] game:round_start {json}");

        try
        {
            RoundStartData data = JsonConvert.DeserializeObject<RoundStartData>(json);
            gameManager.OnRoundStart(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROUND] Parse error: {e.Message}");
        }
    }

    private void OnBettingTimer(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] game:betting_timer {json}");

        try
        {
            TimerData data = JsonConvert.DeserializeObject<TimerData>(json);
            gameManager.OnBettingTimer(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TIMER] Parse error: {e.Message}");
        }
    }

    private void OnBonus(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] game:bonus {json}");

        try
        {
            BonusData data = JsonConvert.DeserializeObject<BonusData>(json);
            gameManager.OnBonus(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BONUS] Parse error: {e.Message}");
        }
    }

    private void OnDiceResult(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] game:dice_result {json}");

        try
        {
            DiceResultData data = JsonConvert.DeserializeObject<DiceResultData>(json);
            gameManager.OnDiceResult(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DICE] Parse error: {e.Message}");
        }
    }

    private void OnBetPlaced(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] game:bet_placed {json}");

        try
        {
            BetPlacedData data = JsonConvert.DeserializeObject<BetPlacedData>(json);
            gameManager.OnBetPlaced(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BET] Parse error: {e.Message}");
        }
    }

    private void OnCashout(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] game:cashout {json}");

        try
        {
            CashoutData data = JsonConvert.DeserializeObject<CashoutData>(json);
            gameManager.OnCashout(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CASHOUT] Parse error: {e.Message}");
        }
    }

    private void OnRoundEnd(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] game:round_end {json}");

        try
        {
            RoundEndPayload data = JsonConvert.DeserializeObject<RoundEndPayload>(json);
            gameManager.OnRoundEnd(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROUND_END] Parse error: {e.Message}");
        }
    }

    private void OnLobbyCount(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[RESPONSE] game:lobby_count {json}");

        try
        {
            LobbyCountData data = JsonConvert.DeserializeObject<LobbyCountData>(json);
            gameManager.OnLobbyCount(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LOBBY] Parse error: {e.Message}");
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
        {
            uiController?.CloseReconnectPopup();
            Debug.Log("[PING-PONG] Connection restored");
        }

        missedPongs = 0;
        Debug.Log("[PING-PONG] Pong received");
    }

    private void OnInternalError(string json)
    {
        if (isBeingDestroyed) return;
        Debug.LogError($"[ERROR] {json}");
        ShowErrorAndBlock("An error occurred. Please refresh.");
    }

    private void OnForceDisconnect(string json)
    {
        if (isBeingDestroyed) return;
        Debug.LogWarning("[FORCE-DC] Another device connected");
        uiController?.ShowAnotherDevicePopup();
    }
    #endregion

    #region Public API - Emit Actions
    internal void JoinLevel(string level)
    {
        if (isBeingDestroyed || gameSocket == null || !gameSocket.IsOpen) return;

        try
        {
            var request = new GameRequest
            {
                type = "JOIN_LEVEL",
                payload = new JoinLevelPayload { level = level }
            };

            string json = JsonConvert.SerializeObject(request);
            Debug.Log($"[EMIT] request JOIN_LEVEL {json}");
            gameSocket.ExpectAcknowledgement<string>(OnJoinLevelAck).Emit("request", json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[JOIN] Error: {e.Message}");
        }
    }

    internal void PlaceBet(string betType, string betOption, int chipIndex, string level)
    {
        if (isBeingDestroyed || gameSocket == null || !gameSocket.IsOpen) return;

        try
        {
            var request = new GameRequest
            {
                type = "PLACE_BET",
                payload = new PlaceBetPayload
                {
                    amountIndex = chipIndex,
                    betType = betType,
                    betOption = betOption
                }
            };

            string json = JsonConvert.SerializeObject(request);
            Debug.Log($"[EMIT] request PLACE_BET {json}");
            gameSocket.ExpectAcknowledgement<string>(OnPlaceBetAck).Emit("request", json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[BET] Error: {e.Message}");
        }
    }

    internal void DoubleBet(string level)
    {
        EmitSimpleRequest("DOUBLE_BET", OnDoubleBetAck);
    }

    internal void RepeatBet()
    {
        EmitSimpleRequest("REPEAT_BET", OnRepeatBetAck);
    }

    internal void UndoBet()
    {
        EmitSimpleRequest("UNDO_BET", OnUndoBetAck);
    }

    internal void CancelBet()
    {
        EmitSimpleRequest("CANCEL_BET", OnCancelBetAck);
    }

    internal void RequestHistory(int page)
    {
        if (isBeingDestroyed || gameSocket == null || !gameSocket.IsOpen) return;

        try
        {
            var request = new GameRequest
            {
                type = "BET_HISTORY",
                payload = new HistoryRequestPayload { page = page }
            };

            string json = JsonConvert.SerializeObject(request);
            Debug.Log($"[EMIT] request BET_HISTORY {json}");
            gameSocket.ExpectAcknowledgement<string>(OnHistoryAck).Emit("request", json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[HISTORY] Error: {e.Message}");
        }
    }

    internal void ReturnHome()
    {
        EmitSimpleRequest("HOME", OnHomeAck);
    }

    internal IEnumerator CloseSocket()
    {
        isExiting = true;
        CleanupRoutines();

        if (manager != null && gameSocket != null && gameSocket.IsOpen)
        {
            try
            {
                gameSocket.Disconnect();
                manager.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CLOSE] Error during close: {e.Message}");
            }
        }

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

        Debug.Log($"[ACK] request {json}");

        try
        {
            RoomPayload payload = JsonConvert.DeserializeObject<RoomPayload>(json);

            if (payload != null)
            {
                gameManager.OnRoomJoinedWithData(payload);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] Join level parse error: {e.Message}");
        }
    }

    private void OnPlaceBetAck(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[ACK] request {json}");

        try
        {
            BetAckResponse response = JsonConvert.DeserializeObject<BetAckResponse>(json);

            if (response != null && response.success && response.payload != null)
            {
                PlayerData.balance = response.payload.balance;
                gameManager.OnBalanceUpdated(response.payload.balance);
            }
            else if (response != null && !response.success)
            {
                string errorMsg = response.payload?.message ?? "Bet placement failed";
                uiController?.ShowErrorPopup(errorMsg);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] Bet parse error: {e.Message}");
        }
    }

    private void OnDoubleBetAck(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[ACK] request {json}");

        try
        {
            BetAckResponse response = JsonConvert.DeserializeObject<BetAckResponse>(json);
            gameManager.OnBetActionResponse(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] Double parse error: {e.Message}");
            gameManager.OnBetActionResponse(null);
        }
    }

    private void OnRepeatBetAck(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[ACK] request {json}");

        try
        {
            BetAckResponse response = JsonConvert.DeserializeObject<BetAckResponse>(json);
            gameManager.OnBetActionResponse(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] Repeat parse error: {e.Message}");
            gameManager.OnBetActionResponse(null);
        }
    }

    private void OnUndoBetAck(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[ACK] request {json}");

        try
        {
            BetAckResponse response = JsonConvert.DeserializeObject<BetAckResponse>(json);
            gameManager.OnBetActionResponse(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] Undo parse error: {e.Message}");
            gameManager.OnBetActionResponse(null);
        }
    }

    private void OnHistoryAck(string json)
    {
        if (isBeingDestroyed) return;
        Debug.Log($"[ACK] request {json}");
        try
        {
            Debug.Log($"111");
            HistoryResponse response = JsonConvert.DeserializeObject<HistoryResponse>(json);

            if (response != null && response.success && response.payload != null)
            {
                if (response.payload.history != null && response.payload.meta != null)
                {
                    gameManager.OnHistoryReceived(response.payload.history, response.payload.meta);
                    Debug.Log($"Sent to data");
                }
                else
                {
                    Debug.LogWarning("[ACK] History data is null in payload");
                }
            }
            else
            {
                Debug.LogWarning("[ACK] Invalid history response or success=false");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] History parse error: {e.Message}\nStack: {e.StackTrace}");
        }
    }

    private void OnCancelBetAck(string json)
    {
        if (isBeingDestroyed) return;

        Debug.Log($"[ACK] request {json}");

        try
        {
            BetAckResponse response = JsonConvert.DeserializeObject<BetAckResponse>(json);
            gameManager.OnBetActionResponse(response);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ACK] Cancel parse error: {e.Message}");
            gameManager.OnBetActionResponse(null);
        }
    }

    private void OnHomeAck(string json)
    {
        if (isBeingDestroyed) return;
        Debug.Log($"[ACK] request {json}");
    }
    #endregion

    #region Platform Communication
    internal void ReceiveAuthToken(string jsonData)
    {
        if (isBeingDestroyed) return;

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
        float timeout = 30f;
        float elapsed = 0f;

        while (!IsInitialized && elapsed < timeout && !isBeingDestroyed)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isBeingDestroyed) yield break;

        if (!IsInitialized)
        {
            ShowErrorAndBlock("Connection timeout. Please refresh.");
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
                Debug.Log($"[PING-PONG] Missed pong {missedPongs}/{MaxMissedPongs}");

                if (missedPongs == 2)
                {
                    uiController?.ShowReconnectPopup();
                }

                if (missedPongs >= MaxMissedPongs)
                {
                    Debug.Log("[PING-PONG] Connection lost");
                    uiController?.ShowDisconnectPopup();
                    break;
                }
            }

            waitingForPong = true;
            EmitSimpleEvent("ping");
            Debug.Log("[PING-PONG] Ping sent");
        }
    }

    private IEnumerator FocusTimeoutCheck()
    {
        while (!hasFocus && !isBeingDestroyed)
        {
            yield return new WaitForSeconds(1f);
        }

        focusCheckRoutine = null;
    }
    #endregion

    #region Private Helpers
    private void EmitSimpleRequest(string requestType, Action<string> ackCallback)
    {
        if (isBeingDestroyed || gameSocket == null || !gameSocket.IsOpen) return;

        try
        {
            var request = new GameRequest
            {
                type = requestType,
                payload = new EmptyPayload()
            };

            string json = JsonConvert.SerializeObject(request);
            Debug.Log($"[EMIT] request {requestType} {json}");
            gameSocket.ExpectAcknowledgement<string>(ackCallback).Emit("request", json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EMIT] {requestType} error: {e.Message}");
        }
    }

    private void EmitSimpleEvent(string eventName)
    {
        if (isBeingDestroyed || gameSocket == null || !gameSocket.IsOpen) return;
        gameSocket.Emit(eventName);
    }

    private void ShowErrorAndBlock(string message)
    {
        if (isBeingDestroyed) return;
        uiController?.ShowErrorPopup(message);
        RaycastBlocker?.SetActive(true);
    }

    private void StartPingPongChecks()
    {
        ResetPingRoutine();
        if (gameObject.activeInHierarchy && !isBeingDestroyed)
        {
            PingRoutine = StartCoroutine(PingPongCheck());
            Debug.Log("[PING-PONG] Monitoring started");
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

#region Request/Response Models
[Serializable]
public class GameRequest
{
    public string type;
    public object payload;
}

[Serializable]
public class EmptyPayload
{
}

[Serializable]
public class JoinLevelPayload
{
    public string level;
}

[Serializable]
public class PlaceBetPayload
{
    public int amountIndex;
    public string betType;
    public string betOption;
}

[Serializable]
public class HistoryRequestPayload
{
    public int page;
}

[Serializable]
public class BetAckResponse
{
    public bool success;
    public BetAckPayload payload;
}

[Serializable]
public class BetAckPayload
{
    public string message;
    public double balance;
    public double totalBet;
    public List<BetInfo> bets;
    public double refundAmount;
    public BetInfo bet;
}
#endregion