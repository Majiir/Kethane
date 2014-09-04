using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kethane.PartModules
{
    internal static class PartExtensions
    {
        public static List<PartResource> GetConnectedResources(this Part part, String resourceName)
        {
            var resourceDef = PartResourceLibrary.Instance.GetDefinition(resourceName);
            var resources = new List<PartResource>();
            part.GetConnectedResources(resourceDef.id, resourceDef.resourceFlowMode, resources);
            return resources;
        }

        public static AnimationState[] SetUpAnimation(this Part part, string animationName)
        {
            var states = new List<AnimationState>();
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(animationName);
                states.Add(animationState);
            }
            return states.ToArray();
        }
    }
}
