using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class KethaneController
    {
        private static IEnumerable<ResourceDefinition> resourceDefinitions = null;

        public static IEnumerable<ResourceDefinition> ResourceDefinitions
        {
            get
            {
                if (resourceDefinitions == null)
                {
                    resourceDefinitions = loadResourceDefinitions();
                }
                return resourceDefinitions;
            }
        }

        private static IEnumerable<ResourceDefinition> loadResourceDefinitions()
        {
            var defs = new SortedDictionary<String, ResourceDefinition>();

            foreach (var definition in GameDatabase.Instance.GetConfigNodes("KethaneResource").Select(TryLoadResourceDefinition).Where(d => d != null))
            {
                if (!PartResourceLibrary.Instance.resourceDefinitions.Contains(definition.Resource))
                {
                    Debug.LogWarning(String.Format("[Kethane] {0} is an unknown resource, ignoring", definition.Resource));
                }
                else if (defs.ContainsKey(definition.Resource))
                {
                    Debug.LogWarning(String.Format("[Kethane] Duplicate definition for {0}, ignoring", definition.Resource));
                }
                else
                {
                    defs[definition.Resource] = definition;   
                }
            }

            Debug.Log(String.Format("[Kethane] Loaded {0} resource definitions", defs.Count));
            return new ReadOnlyCollection<ResourceDefinition>(defs.Values.ToArray());
        }

        private static ResourceDefinition TryLoadResourceDefinition(ConfigNode node)
        {
            try
            {
                return new ResourceDefinition(node);
            }
            catch (Exception e)
            {
                Debug.LogError(String.Format("[Kethane] Error loading resource definition:\n\n{0}", e));
                return null;
            }
        }
    }
}
