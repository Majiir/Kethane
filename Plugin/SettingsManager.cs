
namespace Kethane
{
    internal static class SettingsManager
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
            node.Save(settingsFile);
        }

        private static void load()
        {
            if (node != null) { return; }
            node = ConfigNode.Load(settingsFile) ?? new ConfigNode();
        }

        private static string settingsFile
        {
            get { return KSPUtil.ApplicationRootPath + "GameData/Kethane/settings.cfg"; }
        }
    }
}
