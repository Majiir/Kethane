using UnityEngine;

namespace Kethane
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal class SettingsManager : MonoBehaviour
    {
        private static ConfigNode node;

        public static string GetValue(string key)
        {
            load();
            return node.GetValue(key);
        }

        public static void SetValue(string key, object value)
        {
            load();
            if (node.HasValue(key))
            {
                node.RemoveValue(key);
            }
            node.AddValue(key, value);
        }

        public static void Save()
        {
            load();
            Debug.LogWarning("[Kethane] Saving settings");
            node.Save(settingsFile);
        }

        private static void load()
        {
            if (node != null) { return; }
            Debug.LogWarning("[Kethane] Loading settings");
            node = ConfigNode.Load(settingsFile) ?? new ConfigNode();
        }

        private static string settingsFile
        {
            get { return KSPUtil.ApplicationRootPath + "GameData/Kethane/settings.cfg"; }
        }

        public void Awake()
        {
            MonoBehaviour.DontDestroyOnLoad(this);
        }

        public void Destroy()
        {
            Save();
        }
    }
}
