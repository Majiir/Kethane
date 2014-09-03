using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
{
    public class ResourceData
    {
        private IResourceGenerator generator;
        private ConfigNode generatorNode;

        private Dictionary<CelestialBody, BodyResourceData> bodies = new Dictionary<CelestialBody, BodyResourceData>();

        protected ResourceData(IResourceGenerator generator, ConfigNode generatorNode, IDictionary<CelestialBody, BodyResourceData> bodies)
        {
            this.generator = generator;
            this.generatorNode = generatorNode;
            this.bodies = new Dictionary<CelestialBody, BodyResourceData>(bodies);
        }

        public BodyResourceData this[CelestialBody body]
        {
            get { return bodies[body]; }
        }

        public void ResetBodyData(CelestialBody body)
        {
            bodies[body] = BodyResourceData.Load(generator, body, null);
        }

        public static ResourceData Load(ResourceDefinition resource, ConfigNode resourceNode)
        {
            var bodyResources = new Dictionary<CelestialBody, BodyResourceData>();

            var generatorNode = resourceNode.GetNode("Generator") ?? resource.Generator;
            var generator = createGenerator(generatorNode.CreateCopy());
            if (generator == null)
            {
                Debug.LogWarning("[Kethane] Defaulting to empty generator for " + resource.Resource);
                generator = new EmptyResourceGenerator();
            }

            var bodyNodes = resourceNode.GetNodes("Body");

            foreach (var body in FlightGlobals.Bodies)
            {
                var bodyNode = bodyNodes.SingleOrDefault(n => n.GetValue("Name") == body.name) ?? new ConfigNode();
                bodyResources[body] = BodyResourceData.Load(generator, body, bodyNode);
            }

            return new ResourceData(generator, generatorNode, bodyResources);
        }

        public void Save(ConfigNode resourceNode)
        {
            resourceNode.AddNode(generatorNode);

            foreach (var body in bodies)
            {
                var bodyNode = new ConfigNode("Body");
                bodyNode.AddValue("Name", body.Key.name);
                body.Value.Save(bodyNode);
                resourceNode.AddNode(bodyNode);
            }
        }

        private static IResourceGenerator createGenerator(ConfigNode generatorNode)
        {
            var name = generatorNode.GetValue("name");
            if (name == null) { Debug.LogError("[Kethane] Could not find generator name"); return null; }

            System.Reflection.ConstructorInfo constructor = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    constructor = assembly.GetTypes()
                        .Where(t => t.Name == name)
                        .Where(t => t.GetInterfaces().Contains(typeof(IResourceGenerator)))
                        .Select(t => t.GetConstructor(new Type[] { typeof(ConfigNode) }))
                        .FirstOrDefault(c => c != null);

                    if (constructor != null) { break; }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Kethane] Error inspecting assembly '" + assembly.GetName().Name + "': \n" + e);
                }
            }

            if (constructor == null) { Debug.LogError("[Kethane] Could not find appropriate constructor for " + name); return null; }

            try
            {
                return (IResourceGenerator)constructor.Invoke(new object[] { generatorNode });
            }
            catch (Exception e)
            {
                Debug.LogError("[Kethane] Could not instantiate " + name + ":\n" + e);
                return null;
            }
        }
    }
}
