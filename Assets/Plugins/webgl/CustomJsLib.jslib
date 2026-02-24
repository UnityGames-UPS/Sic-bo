mergeInto(LibraryManager.library, {
    SendLogToReactNative: function (messagePtr) {
        var message = UTF8ToString(messagePtr);
        // console.log('jslib fun : ' + message);
        if (window.ReactNativeWebView) {
          window.ReactNativeWebView.postMessage(message);
        } 
    },

    SendPostMessage: function(messagePtr) {
      var message = UTF8ToString(messagePtr);
      // console.log('SendReactPostMessage, message sent: ' + message);
      if(window.ReactNativeWebView){
        if(message == "authToken"){
          window.ReactNativeWebView.postMessage("if message is authtoken");
          var injectedObjectJson = window.ReactNativeWebView.injectedObjectJson();
          var injectedObj = JSON.parse(injectedObjectJson);

          window.ReactNativeWebView.postMessage('Injected obj : ' + injectedObjectJson);
          
          var combinedData = JSON.stringify({
              socketURL: injectedObj.socketURL.trim(),
              cookie: injectedObj.token.trim(),
              nameSpace: injectedObj.nameSpace ? injectedObj.nameSpace.trim() : ""
          });

          if (typeof SendMessage === 'function') {
            SendMessage('SocketManager', 'ReceiveAuthToken', combinedData);
          }
        }
        window.ReactNativeWebView.postMessage(message);
      }
      else if(window.parent){
        if(window.parent.dispatchReactUnityEvent){
          console.log("Inside window parent");
          window.parent.dispatchReactUnityEvent(message); 
        }
      }
    },

    RequestFullscreen: function () {
      var el = document.documentElement;
      var req = el.requestFullscreen
             || el.webkitRequestFullscreen
             || el.mozRequestFullScreen
             || el.msRequestFullscreen;
      if (req) {
        req.call(el).catch(function(err) {
          console.warn('RequestFullscreen failed: ' + err);
        });
      }
    },

    ExitFullscreen: function () {
      var exit = document.exitFullscreen
              || document.webkitExitFullscreen
              || document.mozCancelFullScreen
              || document.msExitFullscreen;
      if (exit) {
        exit.call(document).catch(function(err) {
          console.warn('ExitFullscreen failed: ' + err);
        });
      }
    },

RegisterFullscreenChangeListener: function(gameObjectNamePtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    console.log('[JS] Registering fullscreen listener');
    
    window._unityFullscreenCallback = function() {
        var isFS = !!(document.fullscreenElement || document.webkitFullscreenElement || 
                      document.mozFullScreenElement || document.msFullscreenElement);
        console.log('[JS] Fullscreen:', isFS);
        
        try {
            (window.unityInstance || window.gameInstance).SendMessage(
                gameObjectName, 'OnFullscreenChanged', isFS ? '1' : '0'
            );
        } catch (err) { console.error('[JS] Error:', err); }
    };
    
    document.removeEventListener('fullscreenchange', window._unityFullscreenCallback);
    document.removeEventListener('webkitfullscreenchange', window._unityFullscreenCallback);
    document.addEventListener('fullscreenchange', window._unityFullscreenCallback);
    document.addEventListener('webkitfullscreenchange', window._unityFullscreenCallback);
    
    setTimeout(window._unityFullscreenCallback, 100);
}
});
