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

    RegisterFullscreenChangeListener: function (gameObjectNamePtr) {
      var gameObjectName = UTF8ToString(gameObjectNamePtr);
      var handled = false;

      var onFullscreenChange = function () {
        var isFullscreen = !!(
          document.fullscreenElement ||
          document.webkitFullscreenElement ||
          document.mozFullScreenElement ||
          document.msFullscreenElement
        );
        var value = isFullscreen ? '1' : '0';
        if (typeof SendMessage === 'function') {
          SendMessage(gameObjectName, 'OnFullscreenChanged', value);
        }
      };

      document.addEventListener('fullscreenchange',       onFullscreenChange);
      document.addEventListener('webkitfullscreenchange', onFullscreenChange);
      document.addEventListener('mozfullscreenchange',    onFullscreenChange);
      document.addEventListener('MSFullscreenChange',     onFullscreenChange);
    }
});
