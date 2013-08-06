using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    internal class KethaneController
    {
        public static bool ScanningSound
        {
            get { return Misc.Parse(SettingsManager.GetValue("ScanningSound"), true); }
            set { SettingsManager.SetValue("ScanningSound", value); }
        }

        private static SortedDictionary<String, ResourceDefinition> resourceDefinitions = null;

        public static ResourceDefinition[] ResourceDefinitions
        {
            get
            {
                loadResourceDefinitions();
                return resourceDefinitions.Values.ToArray();
            }
        }

        public static string SelectedResource { get; set; }

        static KethaneController()
        {
            SelectedResource = "Kethane";
        }

        private static void loadResourceDefinitions()
        {
            if (resourceDefinitions != null) { return; }
            resourceDefinitions = new SortedDictionary<String, ResourceDefinition>();
            foreach (var definitionNode in GameDatabase.Instance.GetConfigNodes("KethaneResource"))
            {
                try
                {
                    var definition = new ResourceDefinition(definitionNode);
                    if (!PartResourceLibrary.Instance.resourceDefinitions.Contains(definition.Resource))
                    {
                        Debug.LogWarning(String.Format("[Kethane] {0} is an unknown resource, ignoring", definition.Resource));
                    }
                    else if (!resourceDefinitions.ContainsKey(definition.Resource))
                    {
                        resourceDefinitions[definition.Resource] = definition;
                    }
                    else
                    {
                        Debug.LogWarning(String.Format("[Kethane] Duplicate definition for {0}, ignoring", definition.Resource));
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(String.Format("[Kethane] Error loading resource definition:\n\n{0}", e));
                }
            }
            Debug.Log(String.Format("[Kethane] Loaded {0} resource definitions", resourceDefinitions.Count));
        }
    }
}
