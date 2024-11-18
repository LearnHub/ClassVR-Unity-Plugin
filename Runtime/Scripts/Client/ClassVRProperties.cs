using System;
using UnityEngine;

namespace ClassVR
{
    public class ConnectInfo
    {
        public int Id;
        public DateTime LastModified;
    }

    public enum ClientChannel
    {
        Unset,
        Alpha,
        Beta,
        Release
    }

    /// <summary>
    /// Utility class for getting device info from the ClassVR client
    /// </summary>
    public class ClassVRProperties
    {
        public string DisplayName { get; private set; }
        public string Id { get; private set; }
        public string DeviceSecret { get; private set; }
        public ClientChannel Channel { get; private set; } = ClientChannel.Unset;
        public bool TiltToSpin { get; private set; } = true;
        // Safest defaults are just to use the current time so cached data will be invalidated
        public DateTime LastModified { get; private set; } = DateTime.Now;
        public ConnectInfo OrganizationInfo { get; private set; }

        const int CLASSVR_UNENROLLED_DEVICES = 18579;

        private AndroidJavaObject _contentResolver;
        private AndroidJavaObject _queryUri;

        /// <summary>
        /// Constructs and queries the ClassVR client to initialize properties.
        /// </summary>
        public ClassVRProperties()
        {
            // Get reference to ContentResolver
            var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var context = activity.Call<AndroidJavaObject>("getApplicationContext");
            _contentResolver = context.Call<AndroidJavaObject>("getContentResolver");

            // Prepare query URI
            var uri = new AndroidJavaClass("android.net.Uri");
            _queryUri = uri.CallStatic<AndroidJavaObject>("parse", "content://com.classvr.client.PropertyContentProvider/device");

            Refresh();
        }

        /// <summary>
        /// Queries the ClassVR client and updates properties.
        /// </summary>
        public void Refresh()
        {
            Debug.Log("Refreshing ClassVR client properties");
            try
            {
                var cursor = _contentResolver.Call<AndroidJavaObject>("query", _queryUri, null, null, null, null);
                if (cursor == null)
                {
                    Debug.LogWarning("ClassVR content provider returned null. Defaulting to unenrolled");
                    OrganizationInfo = new ConnectInfo
                    {
                        Id = CLASSVR_UNENROLLED_DEVICES,
                        LastModified = LastModified
                    };
                    return;
                }

                if (!cursor.Call<bool>("moveToFirst"))
                {
                    Debug.LogError("Expected at least one row");
                    return;
                }

                Id = GetStringColumnFromCursor(cursor, "id");
                if(!string.IsNullOrEmpty(Id))
                {
                    Debug.LogFormat("Found ClassVR device id '{0}'", Id);
                }

                DeviceSecret = GetStringColumnFromCursor(cursor, "devicesecret");
                if (!string.IsNullOrEmpty(DeviceSecret))
                {
                    Debug.LogFormat("Found ClassVR device secret");
                }

                DisplayName = GetStringColumnFromCursor(cursor, "name");
                if (!string.IsNullOrEmpty(DisplayName))
                {
                    Debug.LogFormat("Found ClassVR device name '{0}'", DisplayName);
                }

                var channelNameStr = GetStringColumnFromCursor(cursor, "channel");
                if (!string.IsNullOrEmpty(channelNameStr))
                {
                    Channel = Enum.Parse<ClientChannel>(channelNameStr);
                    Debug.LogFormat("Found ClassVR channel '{0}'", Channel.ToString());
                }

                var tiltToSpinInt = GetIntColumnFromCursor(cursor, "tilttospin");
                if (tiltToSpinInt != null)
                {
                    TiltToSpin = tiltToSpinInt.Value != 0;
                    Debug.LogFormat("Found ClassVR tiltToSpin '{0}'", TiltToSpin.ToString());
                }

                var lastModifiedStr = GetStringColumnFromCursor(cursor, "lastmodified");
                if (!string.IsNullOrEmpty(lastModifiedStr))
                {
                    LastModified = DateTime.Parse(lastModifiedStr);
                    Debug.LogFormat("Found lastmodified '{0}'", LastModified.ToString());
                }

                // Enrolled organization, needs both ID and LastModified columns from cursor
                var organizationIdInt = GetIntColumnFromCursor(cursor, "organizationid");
                if (organizationIdInt != null)
                {
                    Debug.LogFormat("Found organization id '{0}'", organizationIdInt.Value.ToString());

                    // Check for last modified date
                    var organizationLastModifiedStr = GetStringColumnFromCursor(cursor, "organizationlastmodified");
                    if(!string.IsNullOrEmpty(organizationLastModifiedStr))
                    {
                        var organizationLastModified = DateTime.Parse(organizationLastModifiedStr);
                        Debug.LogFormat("Found organizationLastModified '{0}'", organizationLastModified.ToString());

                        OrganizationInfo = new ConnectInfo
                        {
                            Id = organizationIdInt.Value,
                            LastModified = organizationLastModified
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ClassVR content provider threw exception");
                Debug.LogException(ex);
            }
        }

        string GetStringColumnFromCursor(AndroidJavaObject cursor, string columnName)
        {
            var column = cursor.Call<int>("getColumnIndex", columnName);
            if (column >= 0 && !cursor.Call<bool>("isNull", column))
            {
                return cursor.Call<string>("getString", column);
            }

            Debug.LogErrorFormat("Could not retrieve column '{0}'", columnName);
            return null;
        }

        int? GetIntColumnFromCursor(AndroidJavaObject cursor, string columnName)
        {
            var column = cursor.Call<int>("getColumnIndex", columnName);
            if (column >= 0 && !cursor.Call<bool>("isNull", column))
            {
                return cursor.Call<int>("getInt", column);
            }

            Debug.LogErrorFormat("Could not retrieve column '{0}'", columnName);
            return null;
        }
    }
}
