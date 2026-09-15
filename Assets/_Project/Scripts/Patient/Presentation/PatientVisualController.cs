using EmergencyVR.Core;
using EmergencyVR.Medical;
using UnityEngine;

namespace EmergencyVR.Patient.Presentation
{
    [DefaultExecutionOrder(100)]
    public sealed class PatientVisualController : MonoBehaviour
    {
        PatientController patient;
        PatientRigAdapter rig;
        Transform lookTarget;
        Transform originalBodyCollider,originalHeadCollider;
        Quaternion originalBodyOrientation;
        Vector3 chestRest,abdomenRest,headRestPosition,poseRestPosition,jawRestPosition,leftLidRest,rightLidRest;
        Quaternion headRest,poseRest;
        PatientVisualState visual;
        float compression,awareness,excursion,eyeClosure,elapsed,breathingClock,airwayTilt,shockUntil,painWeight,fearWeight,distressWeight;
        Quaternion headOffset=Quaternion.identity,animatedHeadBaseline;
        bool wroteImportedHead;
        PatientPosture? startingPose;
        readonly System.Collections.Generic.Dictionary<Renderer,MaterialPropertyBlock> originalSkinProperties=new System.Collections.Generic.Dictionary<Renderer,MaterialPropertyBlock>();
        MaterialPropertyBlock skinProperties;
#if VITALVR_ANIMATION
        AnimatorControllerParameter[] animatorParameters;
#endif
        float[] originalFaceWeights;
        public PatientRigAdapter Rig => rig;
        public PatientVisualState VisualState => visual;
        public PatientPosture EffectivePosture => startingPose??visual.Posture;
        public bool NeedsHumanAsset => rig==null || rig.provisionalAsset;
        public Transform ChestAnchor => rig==null?null:rig.chestAnchor;
        public Transform AedRightPadAnchor => rig==null?null:rig.aedRightPadAnchor;
        public Transform AedLeftPadAnchor => rig==null?null:rig.aedLeftPadAnchor;
        public Transform UpperArmAnchor => rig==null?null:rig.upperArmAnchor;
        public Transform FingerAnchor => rig==null?null:rig.fingerAnchor;
        public Transform ThighAnchor => rig==null?null:rig.thighAnchor;
        public Transform WoundAnchor => rig==null?null:rig.woundAnchor;
        public Transform ChinAnchor => rig==null?null:rig.chinAnchor;
        public float CompressionDepthMetres => compression;
        public bool IsPatientContact(Vector3 worldPosition,float margin=.055f) => rig!=null && rig.IsPatientContact(worldPosition,margin);
        public float BreathingExcursionMetres => excursion;
        public Vector3 ChestRestPosition => ChestAnchor==null ? transform.position :
            ChestAnchor.position-(rig.chestMotion==null?Vector3.zero:rig.chestMotion.parent.TransformVector(rig.chestMotion.localPosition-chestRest));

        public static PatientVisualController Install(PatientController patient)
        {
            if(patient==null) throw new System.ArgumentNullException(nameof(patient));
            var visual=patient.GetComponent<PatientVisualController>();
            if(visual!=null) return visual;
            visual=patient.gameObject.AddComponent<PatientVisualController>();visual.Initialize(patient);return visual;
        }
        void Initialize(PatientController target)
        {
            patient=target;skinProperties=new MaterialPropertyBlock();
            var existing=patient.GetComponentInChildren<PatientRigAdapter>();
            if(existing!=null) Bind(existing);
            else
            {
                var oldBody=patient.transform.Find("Body");var oldHead=patient.transform.Find("Head");
                Material source=oldBody!=null?oldBody.GetComponent<Renderer>()?.sharedMaterial:null;
                var centre=oldBody!=null?oldBody.localPosition:Vector3.zero;
                var proxy=PatientAnatomicalProxy.Create(patient.transform,centre,source);Bind(proxy);
                // Keep original XRSimpleInteractable and its registered colliders working.
                if(oldBody!=null)
                {
                    originalBodyCollider=oldBody;originalBodyOrientation=Quaternion.Inverse(patient.transform.rotation)*oldBody.rotation;
                    var renderer=oldBody.GetComponent<Renderer>();if(renderer!=null) renderer.enabled=false;
                    var collider=oldBody.GetComponent<CapsuleCollider>();if(collider!=null) collider.radius=.28f;
                }
                if(oldHead!=null)
                {
                    originalHeadCollider=oldHead;
                    var renderer=oldHead.GetComponent<Renderer>();if(renderer!=null) renderer.enabled=false;
                    var collider=oldHead.GetComponent<SphereCollider>();if(collider!=null) collider.radius=.31f;
                    oldHead.localPosition=centre+new Vector3(0,.012f,.66f);
                }
            }
            patient.OnStateChanged.AddListener(OnPatientStateChanged);
            OnPatientStateChanged(patient.State);
        }
        public void Bind(PatientRigAdapter replacement)
        {
            if(replacement==null) throw new System.ArgumentNullException(nameof(replacement));
            if(rig!=null && rig!=replacement) { RestorePresentation();rig.gameObject.SetActive(false); }
            rig=replacement;rig.gameObject.SetActive(true);rig.BindHumanoidBones();
            if(rig.controlledRagdoll!=null && rig.controlledRagdoll.rig==null) rig.controlledRagdoll.rig=rig;
            originalSkinProperties.Clear();
            foreach(var renderer in rig.skinRenderers)if(renderer!=null) {var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);originalSkinProperties[renderer]=block;}
            originalFaceWeights=rig.HasFacialBlendShapes?new float[rig.face.sharedMesh.blendShapeCount]:System.Array.Empty<float>();
            for(int i=0;i<originalFaceWeights.Length;i++)originalFaceWeights[i]=rig.face.GetBlendShapeWeight(i);
            if(rig.chestMotion!=null) chestRest=rig.chestMotion.localPosition;
            if(rig.abdomenMotion!=null) abdomenRest=rig.abdomenMotion.localPosition;
            if(rig.head!=null) { headRest=rig.head.localRotation;headRestPosition=rig.head.localPosition; }
            if(rig.poseRoot!=null) { poseRest=rig.poseRoot.localRotation;poseRestPosition=rig.poseRoot.localPosition; }
            if(rig.jaw!=null) jawRestPosition=rig.jaw.localPosition;
            if(rig.leftLid!=null) leftLidRest=rig.leftLid.localPosition;
            if(rig.rightLid!=null) rightLidRest=rig.rightLid.localPosition;
#if VITALVR_ANIMATION
            animatorParameters=rig.animator!=null && rig.animator.runtimeAnimatorController!=null ? rig.animator.parameters : System.Array.Empty<AnimatorControllerParameter>();
#endif
            compression=0;excursion=0;
        }
        public void SetLookTarget(Transform target) { lookTarget=target; }
        public void SetStartingPose(PatientPosture posture) { startingPose=posture;ApplyAnimatorState(); }
        public void ClearStartingPose() { startingPose=null;ApplyAnimatorState(); }
        public void SetAirwayTilt(float normalized) { airwayTilt=float.IsNaN(normalized)?0:Mathf.Clamp01(normalized); }
        public void TriggerShockReaction() { shockUntil=elapsed+.22f; }
        public void SetCompressionDepth(float metres)
        {
            // Mechanical visual bound only. Clinical thresholds belong to CPR configuration.
            compression=float.IsNaN(metres)||float.IsInfinity(metres)?0:Mathf.Clamp(metres,0,.08f);
        }
        public void SetState(PatientSnapshot state)
        {
            var previous=visual;visual=PatientVisualState.FromSnapshot(state);
            if(previous.Consciousness!=PatientConsciousness.Unresponsive && visual.Consciousness==PatientConsciousness.Unresponsive)
            {
                bool wasUpright=startingPose==PatientPosture.Standing || startingPose==PatientPosture.Seated || previous.Posture==PatientPosture.Standing;
                startingPose=null;
                if(wasUpright && rig!=null && rig.controlledRagdoll!=null)rig.controlledRagdoll.BeginFall();
            }
            ApplyAnimatorState();
        }
        void OnPatientStateChanged(PatientState legacy)
        {
            var snapshot=patient.MedicalState;
            if(snapshot==null)
            {
                snapshot=new PatientSnapshot {
                    consciousness=legacy==PatientState.UnconsciousBreathing || legacy==PatientState.UnconsciousNotBreathing?"Unresponsive":"Conscious",
                    respiration=legacy==PatientState.UnconsciousNotBreathing?"absent":"normal",
                    respiratoryRate=legacy==PatientState.UnconsciousNotBreathing?0:16,
                    flags=legacy==PatientState.Recovering?new[]{"recovering"}:System.Array.Empty<string>() };
            }
            SetState(snapshot);
        }
        void ApplyAnimatorState()
        {
#if VITALVR_ANIMATION
            if(rig==null || rig.animator==null || animatorParameters==null) return;
            foreach(var parameter in animatorParameters)
            {
                if(parameter.type==AnimatorControllerParameterType.Int)
                {
                    if(parameter.name=="PatientConsciousness") rig.animator.SetInteger(parameter.nameHash,(int)visual.Consciousness);
                    if(parameter.name=="PatientPosture") rig.animator.SetInteger(parameter.nameHash,(int)EffectivePosture);
                    if(parameter.name=="PatientExpression") rig.animator.SetInteger(parameter.nameHash,(int)visual.Expression);
                }
                if(parameter.type==AnimatorControllerParameterType.Bool && parameter.name=="PatientSeizure") rig.animator.SetBool(parameter.nameHash,visual.Seizure);
            }
#endif
        }
        void Update()
        {
            // Remove last frame's additive offset before Animator evaluation, preserving authored poses.
            if(rig!=null && !rig.provisionalAsset && rig.head!=null && wroteImportedHead)
                rig.head.localRotation=animatedHeadBaseline;
            wroteImportedHead=false;
        }
        void LateUpdate()
        {
            if(rig==null) return;
            if(rig.controlledRagdoll!=null && rig.controlledRagdoll.OwnsPose) return;
            elapsed+=Time.deltaTime;float blend=1-Mathf.Exp(-Time.deltaTime*6);
            awareness=Mathf.Lerp(awareness,PatientBreathingAnimator.Awareness(visual.Consciousness),blend);
            breathingClock+=Time.deltaTime*visual.RespiratoryRate/60;
            var breath=visual.RespiratoryRate<=0?0:PatientBreathingAnimator.Excursion(visual.Breathing,60,breathingClock);
            excursion=Mathf.Lerp(excursion,breath,blend);
            float reaction=elapsed<shockUntil?Mathf.Sin((shockUntil-elapsed)/.22f*Mathf.PI)*.003f:0;
            if(rig.chestMotion!=null) rig.chestMotion.localPosition=chestRest+rig.chestMotionAxis.normalized*(excursion-compression+reaction);
            if(rig.abdomenMotion!=null) rig.abdomenMotion.localPosition=abdomenRest+rig.chestMotionAxis.normalized*excursion*.32f;
            if(rig.provisionalAsset && rig.poseRoot!=null)
            {
                // Assisted pose transition, NOT a physically validated ragdoll/collapse animation.
                var posture=EffectivePosture;
                var rotation=posture==PatientPosture.Recovery?Quaternion.Euler(0,0,65):
                    posture==PatientPosture.Seated?Quaternion.Euler(-70,0,0):posture==PatientPosture.Standing?Quaternion.Euler(-90,0,0):Quaternion.identity;
                rig.poseRoot.localRotation=Quaternion.Slerp(rig.poseRoot.localRotation,poseRest*rotation,blend*.45f);
                rig.poseRoot.localPosition=Vector3.Lerp(rig.poseRoot.localPosition,poseRestPosition+
                    Vector3.up*(posture==PatientPosture.Recovery?.075f:posture==PatientPosture.Standing?.90f:posture==PatientPosture.Seated?.57f:0),blend*.45f);
                for(int leg=0;leg<rig.upperLegs.Length;leg++)
                {
                    float thigh=posture==PatientPosture.Seated?70:posture==PatientPosture.Recovery && leg==0?28:0;
                    float knee=posture==PatientPosture.Seated?-90:posture==PatientPosture.Recovery && leg==0?-45:0;
                    if(rig.upperLegs[leg]!=null)rig.upperLegs[leg].localRotation=Quaternion.Slerp(rig.upperLegs[leg].localRotation,Quaternion.Euler(thigh,0,0),blend*.45f);
                    if(leg<rig.lowerLegs.Length && rig.lowerLegs[leg]!=null)rig.lowerLegs[leg].localRotation=Quaternion.Slerp(rig.lowerLegs[leg].localRotation,Quaternion.Euler(knee,0,0),blend*.45f);
                    if(leg<rig.feet.Length && rig.feet[leg]!=null)rig.feet[leg].localRotation=Quaternion.Slerp(rig.feet[leg].localRotation,Quaternion.Euler(posture==PatientPosture.Seated?90:0,0,0),blend*.45f);
                }
            }
            AnimateAttention(blend);AnimateFace(blend);AnimateSkin();
            if(rig.provisionalAsset && rig.poseRoot!=null)
            {
                // Registered XR targets follow the assisted pose instead of remaining in empty space.
                if(originalBodyCollider!=null) originalBodyCollider.SetPositionAndRotation(rig.poseRoot.position,rig.poseRoot.rotation*originalBodyOrientation);
                if(originalHeadCollider!=null && rig.head!=null) originalHeadCollider.position=rig.head.position;
            }
        }
        void AnimateAttention(float blend)
        {
            if(rig.head==null) return;
            if(lookTarget==null && Camera.main!=null) lookTarget=Camera.main.transform;
            float turn=0,nod=0;
            if(lookTarget!=null && awareness>.001f)
            {
                var direction=rig.head.parent.InverseTransformDirection(lookTarget.position-rig.head.position);
                if(direction.sqrMagnitude<16)
                {
                    turn=-Mathf.Clamp(Mathf.Atan2(direction.x,Mathf.Max(.01f,direction.y))*Mathf.Rad2Deg,-18,18);
                    nod=Mathf.Clamp(Mathf.Atan2(direction.z,Mathf.Max(.01f,direction.y))*Mathf.Rad2Deg,-12,12);
                }
            }
            if(visual.Consciousness==PatientConsciousness.Confused) turn+=Mathf.Sin(elapsed*.44f)*4;
            var angles=Vector3.Scale(new Vector3(nod*awareness-airwayTilt*15,0,turn*awareness),rig.headRotationAxes);
            headOffset=Quaternion.Slerp(headOffset,Quaternion.Euler(angles),blend*.45f);
            animatedHeadBaseline=rig.provisionalAsset?headRest:rig.head.localRotation;
            rig.head.localRotation=animatedHeadBaseline*headOffset;
            wroteImportedHead=!rig.provisionalAsset;
            // Imported animations own body/head position; the proxy uses the fixed anatomical pivot.
            if(rig.provisionalAsset) rig.head.localPosition=headRestPosition;
            if(rig.provisionalAsset)
            {
                var eyes=Quaternion.Euler(nod*awareness*.18f,0,turn*awareness*.2f);
                if(rig.leftEye!=null) rig.leftEye.localRotation=eyes;
                if(rig.rightEye!=null) rig.rightEye.localRotation=eyes;
            }
        }
        void AnimateFace(float blend)
        {
            var phase=Mathf.Repeat(elapsed,4.7f);
            var blink=phase<.19f?Mathf.Sin(phase/.19f*Mathf.PI):0;
            float closure=visual.Consciousness==PatientConsciousness.Unresponsive?1:visual.Consciousness==PatientConsciousness.Drowsy?.7f:blink;
            eyeClosure=Mathf.Lerp(eyeClosure,closure,1-Mathf.Exp(-Time.deltaTime*22));
            rig.SetBlendShape(rig.blinkLeft,eyeClosure*100);rig.SetBlendShape(rig.blinkRight,eyeClosure*100);
            bool effort=visual.Breathing==PatientBreathingMode.Labored || visual.Breathing==PatientBreathingMode.Agonal;
            rig.SetBlendShape(rig.jawOpen,effort?Mathf.Clamp01(excursion/.011f)*14:0);
            painWeight=Mathf.Lerp(painWeight,visual.Expression==PatientExpression.Pain?25:0,blend);
            fearWeight=Mathf.Lerp(fearWeight,visual.Expression==PatientExpression.Fear?20:0,blend);
            distressWeight=Mathf.Lerp(distressWeight,visual.Expression==PatientExpression.Distress?22:0,blend);
            rig.SetBlendShape(rig.pain,painWeight);rig.SetBlendShape(rig.fear,fearWeight);rig.SetBlendShape(rig.distress,distressWeight);
            if(!rig.provisionalAsset) return;
            void Lid(Transform lid,Vector3 rest)
            {
                if(lid==null)return;lid.localPosition=rest+(Vector3.back*.009f+Vector3.up*.003f)*eyeClosure;
                lid.localScale=new Vector3(1,1,1+eyeClosure*1.1f);
            }
            Lid(rig.leftLid,leftLidRest);Lid(rig.rightLid,rightLidRest);
            if(rig.jaw!=null) rig.jaw.localPosition=jawRestPosition+Vector3.back*(effort?excursion*.35f:0);
        }
        Color displayedSkin;
        void AnimateSkin()
        {
            if(skinProperties==null)skinProperties=new MaterialPropertyBlock();
            var color=Color.Lerp(rig.skinColor,new Color(.67f,.59f,.51f),visual.Pallor);
            color=Color.Lerp(color,new Color(.37f,.40f,.48f),visual.Cyanosis);
            color=Color.Lerp(color,new Color(.66f,.31f,.27f),visual.Flushing);
            if(displayedSkin==default) displayedSkin=rig.skinColor;
            displayedSkin=Color.Lerp(displayedSkin,color,1-Mathf.Exp(-Time.deltaTime*1.4f));
            skinProperties.SetColor("_BaseColor",displayedSkin);
            foreach(var renderer in rig.skinRenderers) if(renderer!=null) renderer.SetPropertyBlock(skinProperties);
        }
        void RestorePresentation()
        {
            if(rig==null)return;
            if(wroteImportedHead && rig.head!=null)rig.head.localRotation=animatedHeadBaseline;
            else if(rig.provisionalAsset && rig.head!=null)rig.head.localRotation=headRest;
            wroteImportedHead=false;headOffset=Quaternion.identity;
            if(rig.chestMotion!=null)rig.chestMotion.localPosition=chestRest;
            if(rig.abdomenMotion!=null)rig.abdomenMotion.localPosition=abdomenRest;
            if(rig.provisionalAsset && rig.jaw!=null)rig.jaw.localPosition=jawRestPosition;
            if(rig.provisionalAsset && rig.leftLid!=null) {rig.leftLid.localPosition=leftLidRest;rig.leftLid.localScale=Vector3.one;}
            if(rig.provisionalAsset && rig.rightLid!=null) {rig.rightLid.localPosition=rightLidRest;rig.rightLid.localScale=Vector3.one;}
            foreach(var pair in originalSkinProperties)if(pair.Key!=null)pair.Key.SetPropertyBlock(pair.Value);
            if(rig.HasFacialBlendShapes && originalFaceWeights!=null)for(int i=0;i<originalFaceWeights.Length;i++)rig.face.SetBlendShapeWeight(i,originalFaceWeights[i]);
            compression=0;excursion=0;airwayTilt=0;
        }
        void OnDisable() { RestorePresentation(); }
        void OnDestroy() { if(patient!=null) patient.OnStateChanged.RemoveListener(OnPatientStateChanged); }
    }
}
