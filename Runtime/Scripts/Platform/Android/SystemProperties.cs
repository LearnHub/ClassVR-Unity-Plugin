using UnityEngine;

namespace ClassVR {
  /// <summary>
  ///  Utility class for querying Android system properties
  /// </summary>
  public static class SystemProperties {
    /// <summary>
    /// Gets the string stored in persist.sys.locale
    /// </summary>
    /// <returns>The system locale</returns>
    public static string GetSysLocale() {
      var sysPropsClass = new AndroidJavaClass("com.classvr.cvr_unity_java.SystemProperties");
      return sysPropsClass.CallStatic<string>("getSysLocale");
    }
  }
}
