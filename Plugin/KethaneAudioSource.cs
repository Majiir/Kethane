using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
{
    public class KethaneAudioSource : PartModule
    {
        [KSPField(isPersistant = false)]
        public String Clip;

        public float Volume
        {
            get { return source.volume; }
            set { source.volume = value; }
        }

        private AudioSource source;

        public override void OnStart(StartState state)
        {
            if (state == StartState.Editor) { return; }

            source = gameObject.AddComponent<AudioSource>();
            if ((source.clip = GameDatabase.Instance.GetAudioClip(Clip)) == null)
            {
                Debug.LogError("[Kethane] Clip not found: " + Clip);
                return;
            }

            // TODO: Initialize properties from config node
        }
    }
}
