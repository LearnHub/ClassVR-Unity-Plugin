using System;
using UnityEngine;

namespace ClassVR {
  public class IntentProvider {
    /// <summary>
    /// Subscribe to receive new intents whenever one is available.
    /// May be called immediately if an intent is cached.
    /// </summary>
    public static event Action<AndroidIntent> IntentReceived {
      add {
        // Add to the collection of subscribers
        _intentReceived = (Action<AndroidIntent>)Delegate.Combine(_intentReceived, value);

        // Check if an intent is cached and inform the new subscriber if so
        // Use the private field instead of the property as the property will construct an intent if not cached
        if (_latestIntent != null) {
          value?.Invoke(_latestIntent);
        }
      }
      remove {
        // Remove from the collection of subscribers
        _intentReceived = (Action<AndroidIntent>)Delegate.Remove(_intentReceived, value);
      }
    }

    /// <summary>
    /// The latest available intent. Attempts to retrieve if not already cached.
    /// </summary>
    public static AndroidIntent LatestIntent {
      get {
        // If an intent hasn't been cached, construct a new one
        // This will contain the latest intent available from the Java plugin
        if (_latestIntent == null) {
          _latestIntent = new AndroidIntent();
        }
        return _latestIntent;
      }
      private set {
        _latestIntent = value;
        // Invoke the event if the new intent isn't null
        if (_latestIntent != null) {
          _intentReceived?.Invoke(_latestIntent);
        }
      }
    }

    private static event Action<AndroidIntent> _intentReceived;
    private static AndroidIntent _latestIntent;

    // Proxy class that implements an interface defined in Java, enabling callbacks on new intents
    class IntentCallbackInterfaceImpl : AndroidJavaProxy {
      public IntentCallbackInterfaceImpl() : base("com.classvr.cvr_unity_java.IntentCallbackInterface") { }

      // This method is called from Java with a serialized intent whenever a new intent is available
      public virtual void onIntentReceived(string intent) {
        // Deserialize and cache the intent
        LatestIntent = new AndroidIntent(intent);
      }
    }

    // This runs after Awake() and OnEnable()
    [RuntimeInitializeOnLoadMethod]
    static void OnRuntimeInitialized() {
      // Register a callback which will be called whenever a new intent is available
#if !UNITY_EDITOR && UNITY_ANDROID
      RegisterIntentCallback();
#endif
    }

    static void RegisterIntentCallback() {
      // Register our callback implementation with the Java plugin
      var pluginClass = new AndroidJavaClass("com.classvr.cvr_unity_java.AndroidIntent");
      pluginClass.CallStatic("setIntentCallback", new IntentCallbackInterfaceImpl());
    }
  }
}
