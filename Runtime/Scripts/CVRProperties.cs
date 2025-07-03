using System;
using UnityEngine;

namespace ClassVR {
  /// <summary>
  /// Utility class for getting device info from the ClassVR client
  /// </summary>
  public sealed class CVRProperties {
    public class ConnectInfo {
      public int Id;
      public DateTime LastModified;
    }

    public enum ClientChannel {
      Unset,
      Alpha,
      Beta,
      Release
    }

    public string DisplayName { get; private set; }
    public string Id { get; private set; }
    public string DeviceSecret { get; private set; }
    public ClientChannel Channel { get; private set; } = ClientChannel.Unset;
    public bool TiltToSpin { get; private set; } = true;
    // Safest defaults are just to use the current time so cached data will be invalidated
    public DateTime LastModified { get; private set; } = DateTime.Now;
    public ConnectInfo OrganizationInfo { get; private set; }
    public string DeviceJWT { get; private set; }

    const int CLASSVR_UNENROLLED_DEVICES = 18579;

    // Singleton pattern with lazy evaluation
    private static readonly Lazy<CVRProperties> lazy = new Lazy<CVRProperties>(() => new CVRProperties());
    public static CVRProperties Instance {
      get { return lazy.Value; }
    }

    private AndroidJavaClass _javaCVRProperties;

    /// <summary>
    /// Constructs and queries the ClassVR client to initialize properties.
    /// </summary>
    private CVRProperties() {
#if (!UNITY_EDITOR && UNITY_ANDROID)
      // Get reference to the Java class used to request serialized properties
      _javaCVRProperties = new AndroidJavaClass("com.classvr.cvr_unity_java.CVRProperties");
#endif
      Refresh();
    }

    /// <summary>
    /// Queries the ClassVR client and updates properties.
    /// </summary>
    public void Refresh() {
      Debug.Log("Refreshing ClassVR client properties");

#if (!UNITY_EDITOR && UNITY_ANDROID)
      // Request json serialized data from Java plugin
      var json = _javaCVRProperties.CallStatic<string>("getClassVRProperties");
      // Deserialize using Unity's JsonUtility
      var props = JsonUtility.FromJson<SerializableCVRProperties>(json);

      // Copy deserialized values into properties

      Id = props.Id;
      if (!string.IsNullOrEmpty(Id)) {
        Debug.LogFormat("Found ClassVR device id '{0}'", Id);
      }

      DeviceSecret = props.DeviceSecret;
      if (!string.IsNullOrEmpty(DeviceSecret)) {
        Debug.LogFormat("Found ClassVR device secret");
      }

      DisplayName = props.DisplayName;
      if (!string.IsNullOrEmpty(DisplayName)) {
        Debug.LogFormat("Found ClassVR device name '{0}'", DisplayName);
      }

      DeviceJWT = props.DeviceJWT;
      if (!string.IsNullOrEmpty(DeviceJWT)) {
        Debug.LogFormat("Found ClassVR device JWT '{0}'", DeviceJWT);
      }

      Channel = props.Channel;
      Debug.LogFormat("Found ClassVR channel '{0}'", Channel.ToString());

      TiltToSpin = props.TiltToSpin;
      Debug.LogFormat("Found ClassVR tiltToSpin '{0}'", TiltToSpin.ToString());

      LastModified = props.LastModifiedDate;
      Debug.LogFormat("Found lastmodified '{0}'", LastModified.ToString());

      // Enrolled organization, will be set to unenrolled if the Java Plugin failed to get data from the ContentProvider
      OrganizationInfo = props.OrgInfo;
      Debug.LogFormat("Found organization id '{0}'", OrganizationInfo.Id.ToString());
      Debug.LogFormat("Found organizationLastModified '{0}'", OrganizationInfo.LastModified.ToString());
#endif
    }
  }

  // Serializable CVRProperties, used for deserializing from json provided by Java plugin
  // Accounts for the quirks of Unity's JsonUtility, such as not handling DateTime
  [Serializable]
  class SerializableCVRProperties : ISerializationCallbackReceiver {
    [Serializable]
    private class SerializableConnectInfo {
      public int Id;
      public string LastModified;
    }

    public string DisplayName;
    public string Id;
    public string DeviceSecret;
    public CVRProperties.ClientChannel Channel;
    public bool TiltToSpin;
    public DateTime LastModifiedDate;
    public CVRProperties.ConnectInfo OrgInfo;
    public string DeviceJWT;

    [SerializeField]
    private string LastModified;
    [SerializeField]
    private SerializableConnectInfo OrganizationInfo;

    public void OnAfterDeserialize() {
      DateTime.TryParse(LastModified, out LastModifiedDate);

      OrgInfo = new CVRProperties.ConnectInfo {
        Id = OrganizationInfo.Id
      };
      DateTime.TryParse(OrganizationInfo.LastModified, out OrgInfo.LastModified);
    }

    public void OnBeforeSerialize() {
      LastModified = LastModifiedDate.ToString("O");

      OrganizationInfo = new SerializableConnectInfo {
        Id = OrgInfo.Id,
        LastModified = OrgInfo.LastModified.ToString("O")
      };
    }
  }
}
