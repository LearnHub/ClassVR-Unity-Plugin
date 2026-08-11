using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClassVR.Platform.Android {
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
    public int Flags { get; private set; }
    public string Package { get; private set; }
    public string Type { get; private set; }

    /// <summary>
    /// The intent's extras, as string key/value pairs. Empty rather than null when the intent
    /// carries none.
    /// </summary>
    /// <remarks>
    /// String and JSON-native primitive values are included as strings, while Parcelables and nested Bundles are skipped.
    /// A value may be empty. Values are never null.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Extras { get; private set; } = NoExtras;

    private static readonly IReadOnlyDictionary<string, string> NoExtras =
      new Dictionary<string, string>();

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
      Extras = BuildExtras(intent.mExtras);
      Flags = intent.mFlags;
      Package = intent.mPackage;
      Type = intent.mType;
    }

    /// <summary>
    /// Gets the value of a named extra.
    /// </summary>
    /// <param name="key">The extra's key, for example <c>"ContentUriExtra"</c>.</param>
    /// <param name="value">
    /// The extra's value, which may be empty. Never null when this returns true.
    /// </param>
    /// <returns>
    /// True if the intent carries the extra, whatever its value. Presence is reported
    /// independently of value, matching <c>Intent.hasExtra</c>.
    /// Callers needing a usable value should check it is non-empty.
    /// </returns>
    public bool TryGetExtra(string key, out string value) {
      value = null;
      return !string.IsNullOrEmpty(key) && Extras.TryGetValue(key, out value);
    }

    // Turns the serialized pairs into a lookup. The bridge sends an array rather than a JSON
    // object because JsonUtility cannot deserialize arbitrary keys.
    private static IReadOnlyDictionary<string, string> BuildExtras(
        SerializableIntent.SerializableExtra[] extras) {
      if (extras == null || extras.Length == 0) {
        return NoExtras;
      }

      var lookup = new Dictionary<string, string>(extras.Length);
      foreach (var extra in extras) {
        if (extra == null || string.IsNullOrEmpty(extra.mKey)) {
          continue;
        }

        // Indexed assignment rather than Add: a duplicate key would throw.
        // A value-less key is kept and normalised to empty so values are never null.
        lookup[extra.mKey] = extra.mValue ?? string.Empty;
      }

      return lookup;
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

      [Serializable]
      public class SerializableExtra {
        public string mKey;
        public string mValue;
      }

      public string mAction;
      public int mBroadcastQueueHint;
      public string[] mCategories;
      public SerializableComponent mComponent;
      public int mContentUserHint;
      public SerializableData mData;
      public SerializableExtra[] mExtras;
      public int mFlags;
      public string mPackage;
      public string mType;
    }
  }
}
