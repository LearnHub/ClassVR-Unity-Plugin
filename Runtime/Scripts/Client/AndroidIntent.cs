using System;
using UnityEngine;

namespace ClassVR
{
    public sealed class AndroidIntent
    {
        public class ComponentName
        {
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
        public static AndroidIntent Instance { get { return lazy.Value; } }

        private AndroidJavaClass _javaAndroidIntent;

        private AndroidIntent()
        {
#if (!UNITY_EDITOR && UNITY_ANDROID)
            _javaAndroidIntent = new AndroidJavaClass("com.classvr.cvr_unity_java.AndroidIntent");

            var json = _javaAndroidIntent.CallStatic<string>("getIntentData");

            var intent = JsonUtility.FromJson<SerializableIntent>(json);

            Action = intent.mAction;
            BroadcastQueueHint = intent.mBroadcastQueueHint;
            Categories = intent.mCategories;
            Component = new ComponentName
            {
                Class = intent.mComponent.mClass,
                Package = intent.mComponent.mPackage
            };
            ContentUserHint = intent.mContentUserHint;
            Data = intent.mData;
            Extras = intent.mExtras;
            Flags = intent.mFlags;
            Package = intent.mPackage;
            Type = intent.mType;
#endif
        }

        [Serializable]
        class SerializableIntent
        {
            [Serializable]
            public class SerializableComponent
            {
                public string mClass;
                public string mPackage;
            }

            public string mAction;
            public int mBroadcastQueueHint;
            public string[] mCategories;
            public SerializableComponent mComponent;
            public int mContentUserHint;
            public string mData;
            public string mExtras;  // Extras is an arbitrary JSON object
            public int mFlags;
            public string mPackage;
            public string mType;
        }
    }
}