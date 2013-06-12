using System;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    public class KethaneDrillAnimator : PartModule, IExtractorAnimator
    {
        [KSPField(isPersistant = false)]
        public string DeployAnimation;

        [KSPField(isPersistant = false)]
        public string DrillAnimation;

        [KSPField(isPersistant = false)]
        public string HeadTransform;

        [KSPField(isPersistant = false)]
        public string TailTransform;

        private AnimationState[] deployStates;
        private AnimationState[] drillStates;

        private Transform headTransform;
        private Transform tailTransform;

        private GameObject gasParticles;
        private GameObject sparkParticles;

        public KethaneDrillAnimator()
        {
            CurrentState = ExtractorState.Retracted;
        }

        public override void OnStart(PartModule.StartState state)
        {
            deployStates = Misc.SetUpAnimation(DeployAnimation, this.part);
            drillStates = Misc.SetUpAnimation(DrillAnimation, this.part);

            headTransform = this.part.FindModelTransform(HeadTransform);
            tailTransform = this.part.FindModelTransform(TailTransform);

            foreach (var drillState in drillStates)
            {
                drillState.enabled = false;
                drillState.wrapMode = WrapMode.Loop;
            }

            if (state == StartState.Editor) { return; }
            if (FlightGlobals.fetch == null) { return; }

            #region Gas particles

            gasParticles = new GameObject();
            gasParticles.transform.parent = headTransform;
            gasParticles.transform.localPosition = Vector3.zero;
            gasParticles.transform.localRotation = Quaternion.identity;

            var gasEmitter = (ParticleEmitter)gasParticles.AddComponent("EllipsoidParticleEmitter");
            var gasRenderer = (ParticleRenderer)gasParticles.AddComponent<ParticleRenderer>();
            var gasAnimator = (ParticleAnimator)gasParticles.AddComponent<ParticleAnimator>();

            gasEmitter.emit = false;
            gasEmitter.useWorldSpace = true;
            gasEmitter.localVelocity = Vector3.up * 6;
            gasEmitter.rndVelocity = Vector3.one * 5;
            gasEmitter.minEmission = 0;
            gasEmitter.maxEmission = 90;
            gasEmitter.minEnergy = 1f;
            gasEmitter.minSize = 1.5f;
            gasEmitter.maxSize = 2.5f;

            gasEmitter.rndRotation = true;
            gasEmitter.angularVelocity = 5;
            gasEmitter.rndAngularVelocity = 15;

            var gasMaterial = new Material(Shader.Find("Particles/Additive"));
            gasMaterial.mainTexture = new Texture2D(512, 512);
            ((Texture2D)gasMaterial.mainTexture).LoadImage(KSP.IO.File.ReadAllBytes<KethaneController>("smoke.jpg"));

            gasRenderer.materials = new Material[] { gasMaterial };
            gasAnimator.doesAnimateColor = true;
            gasAnimator.colorAnimation = new Color[] {
                new Color(0.02f, 0.025f, 0.02f),
                new Color(0.015f, 0.018f, 0.015f),
                new Color(0.004f, 0.0045f, 0.004f),
                new Color(0.001f, 0.0011f, 0.001f),
                new Color(0.0f, 0.0f, 0.0f),
            };

            #endregion

            #region Spark particles

            sparkParticles = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sparkParticles.transform.parent = gasParticles.transform;
            sparkParticles.transform.localPosition = Vector3.zero;
            sparkParticles.transform.localRotation = Quaternion.identity;
            sparkParticles.transform.localScale = new Vector3(1, 0, 1) * 0.275f;
            sparkParticles.renderer.material.shader = Shader.Find("Transparent/Diffuse");
            sparkParticles.renderer.material.color = new Color(0, 0, 0, 0);
            sparkParticles.collider.enabled = false;

            var sparkEmitter = (ParticleEmitter)sparkParticles.AddComponent("MeshParticleEmitter");
            var sparkRenderer = (ParticleRenderer)sparkParticles.AddComponent<ParticleRenderer>();
            var sparkAnimator = (ParticleAnimator)sparkParticles.AddComponent<ParticleAnimator>();

            sparkEmitter.emit = false;
            sparkEmitter.useWorldSpace = true;
            sparkEmitter.localVelocity = Vector3.up * 7f;
            sparkEmitter.rndVelocity = Vector3.one * 1.5f + Vector3.up * 2f;
            sparkEmitter.minEmission = 5000;
            sparkEmitter.maxEmission = 10000;
            sparkEmitter.minEnergy = 0.1f;
            sparkEmitter.maxEnergy = 0.3f;
            sparkEmitter.minSize = 0.01f;
            sparkEmitter.maxSize = 0.02f;

            sparkRenderer.lengthScale = 1f;
            sparkRenderer.velocityScale = 0.08f;
            sparkRenderer.particleRenderMode = ParticleRenderMode.Stretch;

            var sparkMaterial = new Material(Shader.Find("Particles/Additive"));
            sparkMaterial.mainTexture = new Texture2D(250, 250);
            ((Texture2D)sparkMaterial.mainTexture).LoadImage(KSP.IO.File.ReadAllBytes<KethaneController>("mote.png"));

            sparkRenderer.materials = new Material[] { sparkMaterial };
            sparkAnimator.doesAnimateColor = true;
            sparkAnimator.colorAnimation = new Color[] {
                Color.white * 0.8f,
                Color.white * 0.8f,
                Color.white * 0.8f,
                XKCDColors.BurntOrange * 0.6f + Color.white * 0.2f,
                Color.black,
            };

            #endregion
        }

        public ExtractorState CurrentState { get; private set; }

        public void Deploy()
        {
            if (CurrentState != ExtractorState.Retracted) { return; }
            CurrentState = ExtractorState.Deploying;
            foreach (var state in deployStates)
            {
                state.speed = 1;
            }
        }

        public void Retract()
        {
            if (CurrentState != ExtractorState.Deployed) { return; }
            CurrentState = ExtractorState.Retracting;
            foreach (var state in drillStates)
            {
                state.enabled = false;
                state.normalizedTime = 0;
                state.speed = 0;
            }
            foreach (var state in deployStates)
            {
                state.speed = -1;
            }
        }

        public override void OnUpdate()
        {
            var deployState = deployStates.First();
            deployState.normalizedTime = Mathf.Clamp01(deployState.normalizedTime);

            if (CurrentState == ExtractorState.Deploying && deployState.normalizedTime == 1)
            {
                CurrentState = ExtractorState.Deployed;
                foreach (var state in drillStates)
                {
                    state.enabled = true;
                    state.normalizedTime = 0;
                    state.speed = 1;
                }
            }
            else if (CurrentState == ExtractorState.Retracting && deployState.normalizedTime == 0)
            {
                CurrentState = ExtractorState.Retracted;
            }

            var gasEmitter = gasParticles.GetComponent<ParticleEmitter>();

            if (CurrentState != ExtractorState.Retracted)
            {
                RaycastHit hitInfo;
                var hit = raycastGround(out hitInfo);

                sparkParticles.renderer.enabled = sparkParticles.GetComponent<ParticleEmitter>().emit = hit;
                if (hit)
                {
                    sparkParticles.GetComponent<ParticleAnimator>().force = Quaternion.FromToRotation(Vector3.forward, Vector3.up) * gasParticles.transform.InverseTransformDirection(FlightGlobals.getGeeForceAtPosition(headTransform.position));
                    gasParticles.transform.position = hitInfo.point;
                }

                if (CurrentState == ExtractorState.Deployed)
                {
                    gasEmitter.emit = hit && KethaneController.GetInstance(this.vessel).GetDepositUnder() != null;
                    if (gasEmitter.emit)
                    {
                        var gasAnimator = gasParticles.GetComponent<ParticleAnimator>();
                        var pressure = FlightGlobals.getStaticPressure(headTransform.position);
                        gasAnimator.force = Quaternion.FromToRotation(Vector3.forward, Vector3.up) * gasParticles.transform.InverseTransformDirection(FlightGlobals.getGeeForceAtPosition(headTransform.position)) * (float)pressure * -0.4f;
                        gasEmitter.maxEnergy = 10f + 10f * (float)pressure;
                        gasAnimator.sizeGrow = 0.25f + (float)Math.Exp(-2.7f * pressure);
                        gasAnimator.rndForce = Vector3.one * 8 * (float)pressure;
                        gasAnimator.damping = (float)Math.Exp(-2.3f * pressure);
                    }
                }
                else
                {
                    gasEmitter.emit = false;
                }
            }
            else
            {
                sparkParticles.renderer.enabled = sparkParticles.GetComponent<ParticleEmitter>().emit = gasEmitter.emit = false;
            }
        }

        public bool CanExtract
        {
            get { return raycastGround(); }
        }

        private bool raycastGround()
        {
            RaycastHit hitInfo;
            return raycastGround(out hitInfo);
        }

        private bool raycastGround(out RaycastHit hitInfo)
        {
            var mask = 1 << FlightGlobals.getMainBody().gameObject.layer;
            var direction = headTransform.position - tailTransform.position;
            return Physics.Raycast(tailTransform.position, direction, out hitInfo, direction.magnitude, mask);
        }
    }
}
