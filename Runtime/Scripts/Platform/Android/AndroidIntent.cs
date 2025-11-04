using System;
using UnityEngine;

namespace ClassVR {
  /// <summary>
  /// Represents the data from an Android intent
  /// </summary>
  public class AndroidIntent {
    public class ComponentName {
      public string Class;
      public string Package;
    }

    public string Action { get; private set; }
    public int BroadcastQueueHint { get; private set; }
    public string[] Categories { get; private set; }
    public ComponentName Component { get; private set; }
    public int ContentUserHint { get; private set; }
    public string Data { get; private set; }
    public string Extras { get; private set; }
    public int Flags { get; private set; }
    public string Package { get; private set; }
    public string Type { get; private set; }

    private static readonly Lazy<AndroidIntent> lazy = new Lazy<AndroidIntent>(() => new AndroidIntent());
    [Obsolete("Use IntentProvider instead.")]
    public static AndroidIntent Instance {
      get {
        return lazy.Value;
      }
    }

    /// <summary>
    /// Constructs an AndroidIntent from the provided serialized string.
    /// </summary>
    /// <param name="serializedIntent">A JSON serialized string containing the intent data.</param>
    public AndroidIntent(string serializedIntent) {
      DeserializeIntent(serializedIntent);
    }

    /// <summary>
    /// Constructs an AndroidIntent from the latest available intent.
    /// </summary>
    public AndroidIntent() {
      var json = GetSerializedIntent();
      DeserializeIntent(json);
    }

    private string GetSerializedIntent() {
#if !UNITY_EDITOR && UNITY_ANDROID
      var javaAndroidIntent = new AndroidJavaClass("com.classvr.cvr_unity_java.AndroidIntent");
      return javaAndroidIntent.CallStatic<string>("getIntentData");
#else
      return null;
#endif
    }

    void DeserializeIntent(string serializedIntent) {
      if (string.IsNullOrEmpty(serializedIntent)) {
        return;
      }

      var intent = JsonUtility.FromJson<SerializableIntent>(serializedIntent);

      Action = intent.mAction;
      BroadcastQueueHint = intent.mBroadcastQueueHint;
      Categories = intent.mCategories;
      Component = new ComponentName {
        Class = intent.mComponent.mClass,
        Package = intent.mComponent.mPackage
      };
      ContentUserHint = intent.mContentUserHint;
      Data = intent.mData?.uriString;
      Extras = intent.mExtras;
      Flags = intent.mFlags;
      Package = intent.mPackage;
      Type = intent.mType;
    }

    [Serializable]
    class SerializableIntent {
      [Serializable]
      public class SerializableComponent {
        public string mClass;
        public string mPackage;
      }

      [Serializable]
      public class SerializableData {
        public string uriString;
      }

      public string mAction;
      public int mBroadcastQueueHint;
      public string[] mCategories;
      public SerializableComponent mComponent;
      public int mContentUserHint;
      public SerializableData mData;
      public string mExtras;  // Extras is an arbitrary JSON object
      public int mFlags;
      public string mPackage;
      public string mType;
    }
  }
}
