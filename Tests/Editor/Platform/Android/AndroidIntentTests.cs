using ClassVR.Platform.Android;
using NUnit.Framework;

namespace ClassVR.Platform.Android.Tests {
  /// <summary>
  /// EditMode unit tests for <see cref="AndroidIntent"/> deserialization.
  /// <para>
  /// These feed the deserializing constructor the JSON shape the Java bridge emits, so they
  /// exercise the real wire contract without a device or JNI. The extras arrive as an array of
  /// key/value pairs rather than a JSON object because Unity's JsonUtility cannot deserialize
  /// arbitrary keys.
  /// </para>
  /// </summary>
  public class AndroidIntentTests {
    // A real ContentUriExtra value as the ClassVR client supplies it.
    private const string ContentUriExtraKey = "ContentUriExtra";
    private const string ContentUri =
      "content://avnfs.com/AOP6OOHckt7YWoO9321hD4sQABwnXszytVTYdFwu5ds" +
      "?size=17&type=application%2Fx.sample.saveload&name=SampleSaveLoad-967.saveload";

    // mComponent is always emitted: AndroidIntent.DeserializeIntent dereferences it unguarded,
    // so a payload without it would throw before reaching the code under test.
    private static string IntentJson(string extrasJson) {
      return "{\"mAction\":\"android.intent.action.VIEW\"," +
             "\"mComponent\":{\"mClass\":\"TestActivity\",\"mPackage\":\"com.avantis.tests\"}," +
             "\"mData\":{\"uriString\":\"file:///mnt/sdcard/AVNCloud/avnfs.com/abc\"}," +
             "\"mType\":\"application/x.sample.saveload\"" +
             (extrasJson == null ? "" : ",\"mExtras\":" + extrasJson) +
             "}";
    }

    private static string Pair(string key, string value) {
      return "{\"mKey\":\"" + key + "\",\"mValue\":\"" + value + "\"}";
    }

    [Test]
    public void Extras_PairArray_IsExposedAsLookup() {
      var intent = new AndroidIntent(IntentJson("[" + Pair("first", "1") + "," + Pair("second", "2") + "]"));

      Assert.AreEqual(2, intent.Extras.Count);
      Assert.AreEqual("1", intent.Extras["first"]);
      Assert.AreEqual("2", intent.Extras["second"]);
    }

    [Test]
    public void Extras_NoExtrasInPayload_IsEmptyNotNull() {
      var intent = new AndroidIntent(IntentJson(null));

      Assert.IsNotNull(intent.Extras);
      Assert.AreEqual(0, intent.Extras.Count);
    }

    [Test]
    public void Extras_EmptyPayload_IsEmptyNotNull() {
      // DeserializeIntent returns early, so nothing assigns Extras — it must still be usable
      var intent = new AndroidIntent("");

      Assert.IsNotNull(intent.Extras);
      Assert.AreEqual(0, intent.Extras.Count);
    }

    [Test]
    public void Extras_DuplicateKeys_KeepsLastWithoutThrowing() {
      // Bundle keys are unique in practice, but this runs on the launch path and must not throw
      var json = IntentJson("[" + Pair("dupe", "first") + "," + Pair("dupe", "second") + "]");

      AndroidIntent intent = null;
      Assert.DoesNotThrow(() => intent = new AndroidIntent(json));
      Assert.AreEqual("second", intent.Extras["dupe"]);
    }

    [Test]
    public void TryGetExtra_KnownKey_ReturnsValue() {
      var intent = new AndroidIntent(IntentJson("[" + Pair(ContentUriExtraKey, ContentUri) + "]"));

      Assert.IsTrue(intent.TryGetExtra(ContentUriExtraKey, out string value));
      Assert.AreEqual(ContentUri, value);
    }

    [Test]
    public void TryGetExtra_UnknownKey_ReturnsFalseAndNull() {
      var intent = new AndroidIntent(IntentJson("[" + Pair(ContentUriExtraKey, ContentUri) + "]"));

      Assert.IsFalse(intent.TryGetExtra("NotPresent", out string value));
      Assert.IsNull(value);
    }

    // A key carrying no value is a valid flag — `am --esn <KEY>` sends exactly that, and
    // Intent.hasExtra reports presence independently of value. Presence must survive the round
    // trip rather than being mistaken for absence.

    [Test]
    public void TryGetExtra_ValuelessKey_ReturnsTrueWithEmptyValue() {
      var intent = new AndroidIntent(IntentJson("[" + Pair("SomeFlag", "") + "]"));

      Assert.IsTrue(intent.TryGetExtra("SomeFlag", out string value));
      Assert.AreEqual(string.Empty, value);
    }

    [Test]
    public void Extras_NullValue_IsPresentAndNormalisedToEmpty() {
      // The bridge emits null for a value-less extra; values must never surface as null
      var json = IntentJson("[{\"mKey\":\"SomeFlag\",\"mValue\":null}]");

      var intent = new AndroidIntent(json);

      Assert.IsTrue(intent.Extras.ContainsKey("SomeFlag"));
      Assert.AreEqual(string.Empty, intent.Extras["SomeFlag"]);
    }

    [TestCase(null)]
    [TestCase("")]
    public void TryGetExtra_NoKey_ReturnsFalse(string key) {
      var intent = new AndroidIntent(IntentJson("[" + Pair(ContentUriExtraKey, ContentUri) + "]"));

      Assert.IsFalse(intent.TryGetExtra(key, out string value));
      Assert.IsNull(value);
    }

    [Test]
    public void Extras_DoesNotDisturbOtherFields() {
      var intent = new AndroidIntent(IntentJson("[" + Pair(ContentUriExtraKey, ContentUri) + "]"));

      Assert.AreEqual("android.intent.action.VIEW", intent.Action);
      Assert.AreEqual("file:///mnt/sdcard/AVNCloud/avnfs.com/abc", intent.Data);
      Assert.AreEqual("application/x.sample.saveload", intent.Type);
    }
  }
}
