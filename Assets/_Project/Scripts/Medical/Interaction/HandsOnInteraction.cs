using System;
using System.Collections.Generic;
using System.Linq;
using EmergencyVR.Desktop;
using EmergencyVR.Patient.Presentation;
using EmergencyVR.UI;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace EmergencyVR.Medical.Interaction
{
    /// <summary>
    /// Hands-on interaction: the learner acts ON the patient and the equipment instead of picking menu entries.
    /// Body zones (shoulders, chest, head, back) and device controls (AED lid, power and shock buttons, pad liners,
    /// instrument clips) are highlighted when aimed at (Windows) or touched with a controller (Quest); clicking or
    /// holding — trigger in VR — performs the action. Every zone submits exactly the same action id as the
    /// existing menu button or key, so the clinical engine, scoring and reports are unchanged. The menu stays
    /// available as a fallback. The patient then answers in the world: a short reaction and a floating
    /// observation of what the rescuer perceives (not a diagnosis).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class HandsOnInteraction : MonoBehaviour
    {
        public sealed class ZoneAction
        {
            public string Label;
            /// <summary>Seconds the click / trigger must be held; 0 acts on press.</summary>
            public float HoldSeconds;
            /// <summary>Maximum distance from the learner's eyes on Windows (VR requires touching the zone).</summary>
            public float Reach = 1.6f;
            /// <summary>Null for information-only zones (for example how to start CPR in VR).</summary>
            public Action Perform;
            /// <summary>Quest: done by shaking the hand on the zone (tapping/shaking the shoulders), trigger optional.</summary>
            public bool VrShake;
            /// <summary>Quest: done by bringing the head close to the patient's face (look, listen, feel).</summary>
            public bool VrHeadClose;
            /// <summary>Windows: while held, the view leans down to the patient's face looking along the chest.</summary>
            public bool DesktopLean;
            /// <summary>Quest: done by keeping the hand still on the zone (for example two fingers on the wrist).</summary>
            public bool VrDwell;
        }

        public sealed class Zone
        {
            public string Id;
            public bool OnPatient;
            public float Radius;
            public Func<Vector3?> Position;
            public Func<ZoneAction> Evaluate;
            ZoneAction cached; float cachedAt = -1;
            /// <summary>Action available now; re-evaluated at most 10 times per second (Quest garbage collector).</summary>
            public ZoneAction Current()
            {
                if (Time.unscaledTime - cachedAt > .1f) { cached = Evaluate(); cachedAt = Time.unscaledTime; }
                return cached;
            }
            public void Invalidate() => cachedAt = -1;
        }

        const float BubbleSeconds = 4f, VrTouchMargin = .07f, HintDistance = 2.6f;
        static readonly string[] V2Actions = { "AssessResponsiveness", "ObserveBreathing", "ReassessPatient" };

        MedicalProcedureRig rig;
        readonly List<Zone> zones = new List<Zone>();
        readonly HashSet<string> submitted = new HashSet<string>();
        object attempt;
        MedicalPhysicalTool[] tools = Array.Empty<MedicalPhysicalTool>();
        DesktopDemoController desktop;
        TrainingExperience experience;
        XROrigin origin;
        float lookupAt, cooldownUntil;

        Zone hovered;
        ZoneAction hoveredAction;
        Vector3 hoveredPoint;
        bool tooFar, holding, waitRelease;
        float held;
        XRNode? hoverHand;

        XRNode? shakeHand; Vector3 shakeLast; float shakeTravel;
        Material accent, warning, progress, dim, placed;
        LineRenderer focusRing, progressArc, placementRing;
        readonly List<LineRenderer> hintRings = new List<LineRenderer>();
        TextMesh bubble, bubbleShadow, label, labelShadow;
        float bubbleUntil;
        Func<Vector3?> bubbleAnchor;

        /// <summary>Text of the current hovered action (Windows prompt, tests).</summary>
        public string Prompt { get; private set; } = "";
        /// <summary>True while a zone is aimed at or touched: the click belongs to that zone, not to other world input.</summary>
        public bool HasFocus => hoveredAction != null;
        public float HoldProgress => hoveredAction == null || hoveredAction.HoldSeconds <= 0 ? 0 : Mathf.Clamp01(held / hoveredAction.HoldSeconds);
        public string LastObservation { get; private set; } = "";
        /// <summary>Zone actions completed in this run (used by the first-steps coach).</summary>
        public int PerformedCount { get; private set; }
        public IReadOnlyList<Zone> Zones => zones;

        /// <summary>Safety switch for demos: start the player with -vital-classic-controls to use only menus and keys.</summary>
        public static bool Disabled => System.Environment.GetCommandLineArgs().Contains("-vital-classic-controls");

        public void Initialize(MedicalProcedureRig owner)
        {
            rig = owner;
            if (Disabled) { enabled = false; return; }
            BuildVisuals();
            DefineZones();
        }

        // ---------------------------------------------------------------- state helpers

        bool V2 => rig.Manager?.MedicalSession?.Capabilities.usesObservedPatientData == true;
        Case01PatientPresentation Body => rig.Review == null ? null : rig.Review.GetComponent<Case01PatientPresentation>();
        string PatientName => rig.Review?.Selected?.medical?.patientIdentity?.displayName ?? "El paciente";
        bool IsDesktop => desktop != null;

        bool Has(string id)
        {
            if (V2) return Array.IndexOf(V2Actions, id) >= 0;
            var ids = rig.Review?.Selected?.medical == null ? null : rig.Review.ActionIds;
            return ids != null && Array.IndexOf(ids, id) >= 0;
        }

        bool Done(string id)
        {
            if (submitted.Contains(id)) return true;
            var completed = rig.Manager?.MedicalSession?.Completed;
            return completed != null && Array.IndexOf(completed, id) >= 0;
        }

        bool Active
        {
            get
            {
                var manager = rig == null ? null : rig.Manager;
                if (manager == null || !manager.IsRunning || manager.IsPaused || !manager.AcceptsInput || manager.MedicalSession == null) return false;
                if (rig.Review.Selected.medical == null) return false;
                if (experience != null && (experience.BlocksWorldInput || experience.Page != ExperiencePage.Training)) return false;
                if (desktop != null && desktop.WorldInputBlocked) return false;
                return true;
            }
        }

        Camera Viewer => desktop != null ? desktop.View : origin != null && origin.Camera != null ? origin.Camera : Camera.main;

        // ---------------------------------------------------------------- anatomy points

        Vector3? HeadPoint()
        {
            var body = Body;
            if (body != null && body.IsActive) return body.HeadPosition;
            var chin = rig.Visuals == null ? null : rig.Visuals.ChinAnchor;
            return chin == null ? (Vector3?)null : chin.position;
        }

        Vector3? ChestPoint()
        {
            var body = Body;
            if (body != null && body.IsActive) return Vector3.Lerp(body.PelvisPosition, body.HeadPosition, .6f);
            var chest = rig.Visuals == null ? null : rig.Visuals.ChestAnchor;
            return chest == null ? (Vector3?)null : chest.position;
        }

        Vector3? ShoulderPoint()
        {
            var body = Body;
            if (body != null && body.IsActive) return Vector3.Lerp(body.PelvisPosition, body.HeadPosition, .76f);
            var chest = ChestPoint(); var head = HeadPoint();
            if (chest == null || head == null) return null;
            return Vector3.Lerp(chest.Value, head.Value, .55f);
        }

        Vector3? WristPoint()
        {
            var finger = rig.Visuals == null ? null : rig.Visuals.FingerAnchor;
            if (finger == null) return null;
            // Radial pulse: a few centimetres from the fingertip anchor towards the forearm.
            var upper = rig.Visuals.UpperArmAnchor;
            return upper == null ? finger.position : Vector3.MoveTowards(finger.position, upper.position, .09f);
        }

        /// <summary>Palpating the pulse is an examination: it shows what the fingers feel and is not scored.</summary>
        ZoneAction PulseAction()
        {
            var session = rig.Manager?.MedicalSession; if (session == null) return null;
            return new ZoneAction { Label = "Tomar el pulso radial: dos dedos en la muñeca", HoldSeconds = 4f, Reach = 1.3f, VrDwell = true,
                Perform = () =>
                {
                    var p = rig.Manager.MedicalSession.Patient;
                    string text = p.circulation == "pulseless" || p.heartRate <= 0 ? "No se palpa pulso radial." :
                        "Pulso radial: " + p.heartRate.ToString("0") + " lpm, " + (p.heartRate > 100 ? "rápido" : p.heartRate < 60 ? "lento" : "rítmico") +
                        (p.systolic > 0 && p.systolic < 90 ? ", débil." : ", lleno.");
                    Bubble(WristPoint, text); Pulse(.1f, .04f);
                } };
        }

        Vector3? BackPoint()
        {
            if (rig.Visuals == null || rig.Choking == null || !rig.Choking.IsChokingCase) return null;
            return rig.Visuals.ChestRestPosition - rig.Choking.Anterior * .16f;
        }

        Vector3? DevicePoint(Vector3 local)
        {
            var device = rig.AED == null ? null : rig.AED.Device;
            if (device == null || !device.gameObject.activeInHierarchy || !rig.AllowsEquipment(MedicalToolKind.AED)) return null;
            return device.TransformPoint(local);
        }

        // ---------------------------------------------------------------- zones

        void DefineZones()
        {
            zones.Add(new Zone { Id = "shoulders", OnPatient = true, Radius = .12f, Position = ShoulderPoint, Evaluate = ShoulderAction });
            zones.Add(new Zone { Id = "chest", OnPatient = true, Radius = .13f, Position = ChestPoint, Evaluate = ChestAction });
            zones.Add(new Zone { Id = "head", OnPatient = true, Radius = .1f, Position = HeadPoint, Evaluate = HeadAction });
            zones.Add(new Zone { Id = "back", OnPatient = true, Radius = .13f, Position = BackPoint, Evaluate = BackAction });
            zones.Add(new Zone { Id = "pulse", OnPatient = true, Radius = .07f, Position = WristPoint, Evaluate = PulseAction });
            zones.Add(new Zone { Id = "aed-lid", Radius = .07f, Position = () => DevicePoint(new Vector3(0, .075f, .06f)), Evaluate = () => AEDAction("lid") });
            zones.Add(new Zone { Id = "aed-power", Radius = .035f, Position = () => DevicePoint(new Vector3(.09f, .066f, -.005f)), Evaluate = () => AEDAction("power") });
            zones.Add(new Zone { Id = "aed-shock", Radius = .04f, Position = () => DevicePoint(new Vector3(.09f, .07f, -.06f)), Evaluate = () => AEDAction("shock") });
            // Measuring instruments prepare themselves when picked up; only the AED pads keep an explicit step (peel the liner).
            foreach (var kind in new[] { MedicalToolKind.RightPad, MedicalToolKind.LeftPad })
            {
                var k = kind;
                zones.Add(new Zone { Id = "tool-" + k, Radius = .06f, Position = () => ToolPoint(k), Evaluate = () => ToolAction(k) });
            }
        }

        ZoneAction ShoulderAction()
        {
            string first = V2 ? "AssessResponsiveness" : "CheckResponsiveness";
            string again = V2 ? "ReassessPatient" : "Reassess";
            if (Has(first) && !Done(first))
                return new ZoneAction { Label = "Tocar sus hombros y preguntar «¿Me oye?»", HoldSeconds = 1f, Reach = 1.6f, VrShake = true,
                    Perform = () => { rig.Visuals?.TriggerTouchReaction(); Submit(first, Responsiveness); } };
            if (Has(again))
                return new ZoneAction { Label = "Reevaluar: hablarle y comprobar cómo está", HoldSeconds = 1.5f, Reach = 1.6f, VrShake = true,
                    Perform = () => { rig.Visuals?.TriggerTouchReaction(); Submit(again, () => Responsiveness() + "\n" + Breathing()); } };
            if (Has(first))
                return new ZoneAction { Label = "Volver a comprobar si responde", HoldSeconds = 1f, Reach = 1.6f, VrShake = true,
                    Perform = () => { rig.Visuals?.TriggerTouchReaction(); Feedback(Responsiveness); } };
            return null;
        }

        ZoneAction ChestAction()
        {
            string breathing = V2 ? "ObserveBreathing" : "CheckBreathing";
            bool breathingChecked = !Has(breathing) || Done(breathing);
            if (Has(breathing) && !breathingChecked)
                return new ZoneAction { Label = V2 ? "Observar cómo respira: pecho y abdomen" : "Ver, oír y sentir si respira (acerca la cara)",
                    HoldSeconds = V2 ? 3f : 5f, Reach = V2 ? 1.6f : 1.3f, VrHeadClose = !V2 && (!Has("OpenAirway") || Done("OpenAirway")), DesktopLean = !V2, Perform = () => Submit(breathing, Breathing) };
            if (!V2 && rig.CPR != null && Has("StartCPR") && rig.CPR.CanCompress)
            {
                if (IsDesktop)
                    return new ZoneAction { Label = "Empezar compresiones en el centro del pecho", HoldSeconds = 0, Reach = 2.1f,
                        Perform = () =>
                        {
                            rig.CPR.SetWindowsEngaged(!desktop.HasHeldTool);
                            if (rig.CPR.WindowsEngaged) Bubble(ChestPoint, "Compresiones: arrastra hacia abajo sobre el pecho y suelta.\nC para salir.");
                        } };
                return new ZoneAction { Label = "RCP: las dos manos en el centro del pecho y aprieta ambos grips" };
            }
            if (Has(breathing))
                return new ZoneAction { Label = "Volver a comprobar la respiración", HoldSeconds = 3f, Reach = 1.4f, VrHeadClose = !V2, DesktopLean = !V2, Perform = () => Feedback(Breathing) };
            return null;
        }

        ZoneAction HeadAction()
        {
            if (V2)
            {
                if (!IsDesktop || experience == null) return null;
                return new ZoneAction { Label = "Hablar con " + PatientName, HoldSeconds = 0, Reach = 2.3f, Perform = () => experience.OpenPatientDialogue() };
            }
            if (Has("OpenAirway") && !Done("OpenAirway"))
                return new ZoneAction { Label = "Abrir la vía aérea: frente atrás y mentón arriba", HoldSeconds = 1f, Reach = 1.4f,
                    Perform = () =>
                    {
                        if (rig.SubmitNext("OpenAirway"))
                        {
                            submitted.Add("OpenAirway"); rig.Visuals?.SetAirwayTilt(1);
                            Bubble(HeadPoint, "Vía aérea abierta.\nAhora comprueba si respira.");
                        }
                        else Bubble(HeadPoint, "Ahora no corresponde.\nRevisa los pasos anteriores.");
                    } };
            if (Has("RescueBreaths") && (!Has("OpenAirway") || Done("OpenAirway")) && rig.Manager.MedicalSession.Patient.consciousness == "Unresponsive")
                return new ZoneAction { Label = "Dar 2 ventilaciones de rescate", HoldSeconds = 2f, Reach = 1.2f,
                    Perform = () => Submit("RescueBreaths", () => "2 ventilaciones de rescate.\nVuelve a las compresiones.") };
            if (Has("EncourageCough") && !Done("EncourageCough"))
                return new ZoneAction { Label = "Animarle a seguir tosiendo", HoldSeconds = 0, Reach = 1.8f,
                    Perform = () => Submit("EncourageCough", () => PatientName + " sigue tosiendo con fuerza.") };
            return null;
        }

        ZoneAction BackAction()
        {
            var choking = rig.Choking;
            if (choking == null || !choking.IsChokingCase || !choking.CanAct) return null;
            if (!IsDesktop)
                return new ZoneAction { Label = "Desde atrás: mantén los dos grips con las manos en las zonas marcadas" };
            return new ZoneAction { Label = "Colocarse para golpes en la espalda y compresiones abdominales", HoldSeconds = 0, Reach = 1.8f,
                Perform = () =>
                {
                    var eye = Viewer == null ? Vector3.zero : Viewer.transform.position;
                    if (!choking.InReach(eye)) { Bubble(BackPoint, "Colócate detrás de " + PatientName + "."); return; }
                    choking.SetWindowsEngaged(true);
                } };
        }

        ZoneAction AEDAction(string control)
        {
            if (V2 || rig.AED == null || rig.AED.Device == null) return null;
            var phase = rig.AED.State.Phase;
            if (control == "lid" && phase == AEDPhase.Closed)
                return new ZoneAction { Label = "Abrir la tapa del DEA", HoldSeconds = 0, Reach = 1.6f, Perform = UseAED };
            if (control == "power" && phase == AEDPhase.Open)
                return new ZoneAction { Label = "Encender el DEA", HoldSeconds = 0, Reach = 1.6f, Perform = UseAED };
            if (control == "shock" && (phase == AEDPhase.PadsReady || phase == AEDPhase.ContinueCPR))
                return new ZoneAction { Label = "Analizar el ritmo: nadie toca al paciente", HoldSeconds = .6f, Reach = 1.8f, Perform = UseAED };
            if (control == "shock" && phase == AEDPhase.ShockAdvised)
                return new ZoneAction { Label = "DESCARGA: comprueba que nadie toca al paciente", HoldSeconds = .8f, Reach = 1.8f, Perform = UseAED };
            return null;
        }

        void UseAED()
        {
            rig.AED.Use();
            var text = rig.AED.Display == null ? "" : rig.AED.Display.text;
            if (!string.IsNullOrEmpty(text)) Bubble(() => DevicePoint(new Vector3(0, .12f, -.06f)), text.Replace("\n", " · "));
        }

        Vector3? ToolPoint(MedicalToolKind kind)
        {
            var tool = Tool(kind);
            if (tool == null || !tool.gameObject.activeInHierarchy || tool.IsAttached || tool.IsHeld || !rig.AllowsEquipment(kind)) return null;
            return tool.transform.position + Vector3.up * .02f;
        }

        ZoneAction ToolAction(MedicalToolKind kind)
        {
            if (V2 && !MedicalProcedureRig.IsExamInstrument(kind)) return null;
            var tool = Tool(kind);
            if (tool == null || tool.IsPrepared || tool.IsAttached) return null;
            bool pad = kind == MedicalToolKind.RightPad || kind == MedicalToolKind.LeftPad;
            if (pad && (rig.AED.State.Phase == AEDPhase.Closed || rig.AED.State.Phase == AEDPhase.Open)) return null;
            string text = pad ? "Retirar el plástico del parche" : kind == MedicalToolKind.Oximeter ? "Abrir la pinza del pulsioxímetro" :
                kind == MedicalToolKind.Glucose ? "Preparar una tira en el glucómetro" : "Preparar el manguito del tensiómetro";
            return new ZoneAction { Label = text, HoldSeconds = 0, Reach = 1.6f, Perform = () =>
            {
                tool.Use();
                if (tool.IsPrepared) Bubble(() => tool.PlacementTarget == null ? (Vector3?)tool.transform.position : tool.PlacementTarget.position,
                    "Listo. Cógelo y llévalo a la zona marcada en " + PatientName + ".");
            } };
        }

        MedicalPhysicalTool Tool(MedicalToolKind kind)
        {
            foreach (var tool in tools) if (tool != null && tool.Kind == kind) return tool;
            return null;
        }

        // ---------------------------------------------------------------- perception text

        string Responsiveness()
        {
            var p = rig.Manager.MedicalSession.Patient;
            switch (p.consciousness)
            {
                case "Unresponsive": return PatientName + " no responde a la voz ni al contacto.";
                case "Confused": return PatientName + " responde, pero está confuso.";
                case "Drowsy": return PatientName + " responde con dificultad, adormilado.";
                default: return PatientName + " responde y te mira.";
            }
        }

        string Breathing()
        {
            var p = rig.Manager.MedicalSession.Patient;
            if (p.respiratoryRate <= 0 || p.respiration == "absent") return "No respira.";
            switch (p.respiration)
            {
                case "agonal": return "No respira con normalidad: solo boqueadas.";
                case "fast": return "Respira rápido.";
                case "slow": return "Respira lento.";
                case "normal": return "Respira con normalidad.";
                default: return "Respiración: " + p.respiration + ".";
            }
        }

        void Submit(string id, Func<string> perception)
        {
            rig.Review.Submit(id);
            submitted.Add(id);
            Feedback(V2 ? null : perception);
        }

        void Feedback(Func<string> perception)
        {
            string text = perception?.Invoke();
            if (string.IsNullOrEmpty(text))
            {
                var raw = rig.Manager.Feedback;
                text = experience != null ? experience.DisplayClinicalText(raw) : raw;
            }
            Bubble(HeadPoint, text);
            Pulse(.12f, .04f);
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            if (rig == null) return;
            if (Time.unscaledTime >= lookupAt)
            {
                desktop = FindFirstObjectByType<DesktopDemoController>();
                experience = FindFirstObjectByType<TrainingExperience>();
                if (desktop == null && origin == null) origin = FindFirstObjectByType<XROrigin>();
                tools = rig.GetComponentsInChildren<MedicalPhysicalTool>(true);
                lookupAt = Time.unscaledTime + .5f;
            }
            var session = rig.Manager?.MedicalSession;
            if (!ReferenceEquals(session, attempt)) { attempt = session; submitted.Clear(); }

            if (!Active || HandsBusy) { ClearHover(); return; }
            if (IsDesktop) UpdateDesktop(); else UpdateVR();
        }

        bool HandsBusy => rig.CPR != null && rig.CPR.WindowsEngaged || rig.Choking != null && rig.Choking.WindowsEngaged;

        void UpdateDesktop()
        {
            var mouse = Mouse.current; var view = desktop.View;
            if (mouse == null || view == null) { ClearHover(); return; }
            // While an action is being held the zone stays locked, even if the view leans or the mouse drifts.
            if (holding && hovered != null && mouse.leftButton.isPressed)
            {
                var locked = hovered.Position();
                if (locked != null) hoveredPoint = locked.Value;
                Drive(false, true);
                Lean(view);
                return;
            }
            bool overUi = desktop.PointerOverInterface && !desktop.FreeLook;
            var ray = desktop.AimRay(mouse.position.ReadValue());
            Zone best = null; ZoneAction bestAction = null; Vector3 bestPoint = default; float bestScore = float.MaxValue;
            if (!overUi)
                foreach (var zone in zones)
                {
                    var point = zone.Position(); if (point == null) continue;
                    var action = zone.Current(); if (action == null) continue;
                    float along = Vector3.Dot(point.Value - ray.origin, ray.direction);
                    if (along <= 0 || along > 4f) continue;
                    float miss = Vector3.Distance(ray.origin + ray.direction * along, point.Value);
                    if (miss > zone.Radius) continue;
                    float score = miss / zone.Radius;
                    if (score < bestScore) { bestScore = score; best = zone; bestAction = action; bestPoint = point.Value; }
                }
            SetHover(best, bestAction, bestPoint, null);
            if (hovered == null) return;
            tooFar = Vector3.Distance(view.transform.position, hoveredPoint) > hoveredAction.Reach;
            Drive(mouse.leftButton.wasPressedThisFrame, mouse.leftButton.isPressed);
            Lean(view);
        }

        /// <summary>Look, listen and feel on Windows: the eyes go down beside the patient's mouth, looking along the chest.</summary>
        void Lean(Camera view)
        {
            if (!holding || hoveredAction == null || !hoveredAction.DesktopLean) return;
            var head = HeadPoint(); var chest = ChestPoint();
            if (head == null || chest == null) return;
            var side = view.transform.position - head.Value; side.y = 0;
            side = side.sqrMagnitude < 1e-4f ? Vector3.zero : side.normalized;
            var eye = head.Value + Vector3.up * .26f + side * .14f;
            desktop.SetActionLean(eye, chest.Value, Mathf.SmoothStep(0, 1, Mathf.Clamp01(held / .6f)));
        }

        void UpdateVR()
        {
            if (origin == null) { ClearHover(); return; }
            // Look, listen and feel: bringing the head close to the patient's face performs the breathing check.
            var eyes = origin.Camera != null ? origin.Camera.transform.position : origin.transform.position;
            var face = HeadPoint();
            if (face != null && Vector3.Distance(eyes, face.Value) < .5f)
                foreach (var zone in zones)
                {
                    var action = zone.Current(); var point = zone.Position();
                    if (action == null || !action.VrHeadClose || point == null) continue;
                    SetHover(zone, action, point.Value, null);
                    tooFar = false;
                    Drive(!holding && !waitRelease, true);
                    return;
                }
            Zone best = null; ZoneAction bestAction = null; Vector3 bestPoint = default; float bestScore = float.MaxValue; XRNode bestHand = XRNode.RightHand;
            bool anyPressed = false, anyStarted = false;
            foreach (var node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                if (!ReadHand(node, out var hand, out _)) continue;
                foreach (var zone in zones)
                {
                    var point = zone.Position(); if (point == null) continue;
                    var action = zone.Current(); if (action == null) continue;
                    float distance = Vector3.Distance(hand, point.Value);
                    if (distance > zone.Radius + VrTouchMargin) continue;
                    float score = distance / (zone.Radius + VrTouchMargin);
                    if (score < bestScore) { bestScore = score; best = zone; bestAction = action; bestPoint = point.Value; bestHand = node; }
                }
            }
            SetHover(best, bestAction, bestPoint, best == null ? (XRNode?)null : bestHand);
            if (hovered == null) return;
            tooFar = false;
            if (ReadHand(bestHand, out var handNow, out var value))
            {
                // Shaking/tapping the shoulder counts as pressing: accumulate the hand's travel while on the zone.
                if (bestAction.VrShake)
                {
                    if (shakeHand == bestHand) shakeTravel += Vector3.Distance(handNow, shakeLast);
                    shakeHand = bestHand; shakeLast = handNow;
                    shakeTravel *= Mathf.Exp(-Time.deltaTime * 1.5f);
                }
                else shakeTravel = 0;
                anyPressed = value > .6f || shakeTravel > .16f || (bestAction.VrDwell && HeldTool() == null);
                anyStarted = anyPressed && !holding && !waitRelease;
            }
            Drive(anyStarted, anyPressed);
        }

        bool ReadHand(XRNode node, out Vector3 position, out float trigger)
        {
            trigger = 0;
            if (QuestLookSimulation.TryGetControllerPose(node, out position, out _, out var tracked))
            {
                QuestLookSimulation.TryGetGripTrigger(node, out _, out trigger);
                return tracked;
            }
            var device = InputDevices.GetDeviceAtXRNode(node);
            bool valid = device.TryGetFeatureValue(CommonUsages.devicePosition, out position) &&
                device.TryGetFeatureValue(CommonUsages.isTracked, out var isTracked) && isTracked;
            device.TryGetFeatureValue(CommonUsages.trigger, out trigger);
            var frame = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : origin.transform;
            position = frame.TransformPoint(position);
            return valid;
        }

        void SetHover(Zone zone, ZoneAction action, Vector3 point, XRNode? hand)
        {
            bool changed = zone != hovered || (action?.Label != hoveredAction?.Label);
            // A new zone starts fresh: a press left over from the previous zone (or a dwell/shake that just
            // completed) must not block it.
            if (zone != hovered) waitRelease = false;
            if (changed)
            {
                held = 0; holding = false;
                if (zone != null && hand.HasValue) MedicalHaptics.Pulse(hand.Value, .05f, .015f, "ZoneHover");
            }
            hovered = zone; hoveredAction = action; hoveredPoint = point; hoverHand = hand;
            Prompt = action == null ? "" : action.Label;
        }

        void ClearHover()
        {
            hovered = null; hoveredAction = null; holding = false; held = 0; Prompt = ""; hoverHand = null; waitRelease = false;
        }

        void Drive(bool started, bool pressed)
        {
            if (!pressed) { holding = false; held = 0; waitRelease = false; return; }
            if (waitRelease || Time.unscaledTime < cooldownUntil) return;
            if (started && !holding)
            {
                if (tooFar)
                {
                    var at = hoveredPoint;
                    Bubble(() => at, IsDesktop && !desktop.Kneeling ? "Acércate más (Ctrl para arrodillarte)." : "Acércate más.");
                    waitRelease = true; return;
                }
                if (hoveredAction.Perform == null) { var at = hoveredPoint; Bubble(() => at, hoveredAction.Label); waitRelease = true; return; }
                holding = true; held = 0;
            }
            if (!holding) return;
            held += Time.deltaTime;
            if (held < hoveredAction.HoldSeconds) return;
            var perform = hoveredAction.Perform;
            holding = false; held = 0; waitRelease = true; cooldownUntil = Time.unscaledTime + .35f;
            perform?.Invoke();
            if (perform != null) PerformedCount++;
            foreach (var zone in zones) zone.Invalidate();
            Pulse(.15f, .05f);
        }

        void Pulse(float amplitude, float seconds)
        {
            if (hoverHand.HasValue) MedicalHaptics.Pulse(hoverHand.Value, amplitude, seconds, "HandsOn");
        }

        // ---------------------------------------------------------------- feedback in the world

        /// <summary>Shows a reading or finding next to where it was obtained (instrument screen, stethoscope...).</summary>
        public void Announce(Vector3 at, string text) => Bubble(() => at, text);

        // ---------------------------------------------------------------- stethoscope
        AudioSource heart; AudioClip heartClip; int heartClipRate; float listening; bool listenedReported;
        void UpdateStethoscope(MedicalPhysicalTool held)
        {
            bool onChest = false;
            if (held != null && held.Kind == MedicalToolKind.Stethoscope && held.PlacementTarget != null && Active)
                onChest = Vector3.Distance(held.transform.position, held.PlacementTarget.position) <= held.PlacementRadius;
            if (!onChest)
            {
                listening = 0; listenedReported = false;
                if (heart != null && heart.isPlaying) heart.Stop();
                return;
            }
            var p = rig.Manager.MedicalSession.Patient;
            bool beats = p.circulation != "pulseless" && p.heartRate > 0;
            listening += Time.deltaTime;
            if (beats)
            {
                int rate = Mathf.Clamp(Mathf.RoundToInt((float)p.heartRate), 30, 200);
                if (heart == null) { heart = gameObject.AddComponent<AudioSource>(); heart.loop = true; heart.spatialBlend = 0; heart.volume = .9f; heart.playOnAwake = false; }
                if (heartClip == null || heartClipRate != rate) { if (heartClip != null) Destroy(heartClip); heartClip = HeartbeatClip(rate); heartClipRate = rate; heart.clip = heartClip; heart.Play(); }
                if (!heart.isPlaying) heart.Play();
            }
            else if (heart != null && heart.isPlaying) heart.Stop();
            if (!listenedReported && listening > 2.5f)
            {
                listenedReported = true;
                Bubble(() => held.PlacementTarget.position, beats
                    ? "Ruidos cardíacos rítmicos, " + p.heartRate.ToString("0") + " lpm.\n" + Breathing()
                    : "No se auscultan latidos.\n" + Breathing());
            }
        }

        /// <summary>One "lub-dub" cycle at the given heart rate, generated (no recorded clinical audio is claimed).</summary>
        static AudioClip HeartbeatClip(int beatsPerMinute)
        {
            const int sampleRate = 22050;
            int length = Mathf.RoundToInt(sampleRate * 60f / beatsPerMinute);
            var data = new float[length];
            void Thump(float at, float frequency, float duration, float gain)
            {
                int start = Mathf.RoundToInt(at * sampleRate), count = Mathf.RoundToInt(duration * sampleRate);
                for (int i = 0; i < count && start + i < length; i++)
                {
                    float t = i / (float)sampleRate, envelope = Mathf.Sin(Mathf.PI * i / count);
                    data[start + i] += Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope * envelope * gain;
                }
            }
            Thump(0f, 52f, .11f, .9f);     // S1 "lub"
            Thump(.30f * 60f / Mathf.Max(60, beatsPerMinute), 68f, .08f, .65f); // S2 "dub", closer at high rates
            var clip = AudioClip.Create("Generated heartbeat " + beatsPerMinute, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Bubble(Func<Vector3?> anchor, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            LastObservation = text;
            bubbleAnchor = anchor;
            bubble.text = Wrap(text, 34); bubbleShadow.text = bubble.text;
            bubbleUntil = Time.unscaledTime + BubbleSeconds;
        }

        static string Wrap(string text, int width)
        {
            var lines = new List<string>();
            foreach (var paragraph in text.Split('\n'))
            {
                var line = "";
                foreach (var word in paragraph.Split(' '))
                {
                    if (line.Length > 0 && line.Length + word.Length + 1 > width) { lines.Add(line); line = word; }
                    else line = line.Length == 0 ? word : line + " " + word;
                }
                lines.Add(line);
            }
            return string.Join("\n", lines);
        }

        void LateUpdate()
        {
            if (rig == null || focusRing == null) return;
            var view = Viewer;
            bool active = Active && view != null;
            var eye = view == null ? Vector3.zero : view.transform.position;

            // Focus ring and hold progress on the aimed / touched zone.
            bool showFocus = active && hovered != null && hoveredAction != null;
            focusRing.enabled = showFocus;
            if (showFocus)
            {
                focusRing.sharedMaterial = tooFar ? warning : accent;
                DrawCircle(focusRing, hoveredPoint, hovered.Radius * .85f, Facing(hoveredPoint, eye), 1f, .004f + .0012f * Mathf.Sin(Time.unscaledTime * 6));
            }
            bool showProgress = showFocus && holding && hoveredAction.HoldSeconds > 0;
            progressArc.enabled = showProgress;
            if (showProgress) DrawCircle(progressArc, hoveredPoint, hovered.Radius * 1.05f, Facing(hoveredPoint, eye), HoldProgress, .009f);

            // Guided practice: discreet rings on everything the learner can do near them.
            int ring = 0;
            if (active && rig.TrainingMode)
                foreach (var zone in zones)
                {
                    if (zone == hovered || ring >= hintRings.Count) continue;
                    var point = zone.Position(); if (point == null || zone.Current() == null) continue;
                    if (Vector3.Distance(eye, point.Value) > HintDistance) continue;
                    var line = hintRings[ring++]; line.enabled = true;
                    DrawCircle(line, point.Value, zone.Radius * .55f, Facing(point.Value, eye), 1f, .0025f);
                }
            for (int i = ring; i < hintRings.Count; i++) hintRings[i].enabled = false;

            // Placement guide for the instrument in hand: where it goes, green once it will attach on release.
            var heldTool = HeldTool();
            UpdateStethoscope(heldTool);
            var target = heldTool == null ? null : heldTool.PlacementTarget;
            placementRing.enabled = active && target != null;
            if (placementRing.enabled)
            {
                bool inRange = Vector3.Distance(heldTool.transform.position, target.position) <= heldTool.PlacementRadius;
                placementRing.sharedMaterial = inRange ? placed : accent;
                if (heldTool.IsPad) DrawRectangle(placementRing, target, .1f, .13f);
                else DrawCircle(placementRing, target.position, Mathf.Max(.035f, heldTool.PlacementRadius * .45f), target.rotation * Quaternion.Euler(90, 0, 0), 1f, inRange ? .006f : .004f);
            }

            // VR: the action name floats next to the touched zone.
            bool showLabel = active && !IsDesktop && showFocus;
            label.gameObject.SetActive(showLabel);
            if (showLabel)
            {
                string how = hoveredAction.VrHeadClose ? "\n(acerca tu cara a la suya)" : hoveredAction.VrShake ? "\n(mueve la mano sobre su hombro o gatillo)" :
                    hoveredAction.VrDwell ? "\n(deja la mano quieta)" : hoveredAction.Perform != null ? "\n(gatillo)" : "";
                label.text = Wrap(hoveredAction.Label, 30) + how; labelShadow.text = label.text;
                Billboard(label, hoveredPoint + Vector3.up * (hovered.Radius + .07f), eye);
            }

            // Observation bubble above the patient (or the device that answered).
            var anchor = bubbleAnchor?.Invoke();
            bool showBubble = view != null && Time.unscaledTime < bubbleUntil && anchor != null && (rig.Manager?.IsRunning ?? false);
            bubble.gameObject.SetActive(showBubble);
            if (showBubble) Billboard(bubble, anchor.Value + Vector3.up * .3f, eye);
        }

        MedicalPhysicalTool HeldTool()
        {
            if (desktop != null) return desktop.HeldTool;
            foreach (var tool in tools) if (tool != null && tool.IsHeld) return tool;
            return null;
        }

        static Quaternion Facing(Vector3 point, Vector3 eye)
        {
            var toEye = eye - point;
            return toEye.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(toEye.normalized);
        }

        static void Billboard(TextMesh text, Vector3 position, Vector3 eye)
        {
            var t = text.transform;
            t.position = position;
            var away = position - eye;
            if (away.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            // Fit the glyphs to a readable line height (3 cm at 1.4 m), whatever the font metrics are,
            // and keep the apparent size roughly constant between 0.6 and 3 m.
            var renderer = text.GetComponent<MeshRenderer>();
            float lines = Mathf.Max(1, text.text.Split('\n').Length);
            float measured = renderer == null ? 0 : renderer.localBounds.size.y;
            float lineHeight = .03f * Mathf.Clamp(Vector3.Distance(position, eye) / 1.4f, .55f, 1.8f);
            t.localScale = Vector3.one * (measured > 1e-5f ? lineHeight * lines / measured : 1);
            // Drop shadow offset proportional to one line, in the text's own (unscaled) units.
            if (t.childCount > 0 && measured > 1e-5f) t.GetChild(0).localPosition = new Vector3(.05f, -.05f, .06f) * (measured / lines);
        }

        // Circle in the plane facing `rotation`'s forward axis; `fraction` < 1 draws an arc (hold progress).
        static void DrawCircle(LineRenderer line, Vector3 centre, float radius, Quaternion rotation, float fraction, float width)
        {
            const int segments = 40;
            int count = Mathf.Max(2, Mathf.CeilToInt(segments * Mathf.Clamp01(fraction)) + 1);
            line.loop = fraction >= .999f;
            if (line.loop) count = segments;
            line.positionCount = count;
            line.startWidth = line.endWidth = width;
            float sweep = Mathf.PI * 2 * (line.loop ? 1 : Mathf.Clamp01(fraction));
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.PI * .5f - sweep * i / (line.loop ? count : count - 1);
                line.SetPosition(i, centre + rotation * new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0));
            }
        }

        static void DrawRectangle(LineRenderer line, Transform target, float width, float depth)
        {
            line.loop = true; line.positionCount = 4; line.startWidth = line.endWidth = .005f;
            var right = target.right * width * .5f; var forward = target.forward * depth * .5f; var c = target.position + target.up * .004f;
            line.SetPosition(0, c - right - forward); line.SetPosition(1, c + right - forward);
            line.SetPosition(2, c + right + forward); line.SetPosition(3, c - right + forward);
        }

        // ---------------------------------------------------------------- visuals

        void BuildVisuals()
        {
            var saved = Resources.Load<Material>("Visual/Materials/GuideGlow");
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
            Material Make(string name, Color color)
            {
                var m = saved != null ? new Material(saved) : new Material(shader);
                m.name = name; m.SetColor("_BaseColor", color);
                if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                return m;
            }
            accent = Make("Hands-on focus", new Color(.36f, .93f, .86f));
            warning = Make("Hands-on too far", new Color(1f, .74f, .22f));
            progress = Make("Hands-on progress", new Color(.42f, 1f, .45f));
            dim = Make("Hands-on hint", new Color(.36f, .93f, .86f) * .55f);
            placed = Make("Hands-on placement ready", new Color(.42f, 1f, .45f));

            focusRing = Line("Hands-on focus ring", accent);
            progressArc = Line("Hands-on progress", progress);
            placementRing = Line("Instrument placement guide", accent);
            for (int i = 0; i < 6; i++) hintRings.Add(Line("Hands-on hint " + i, dim));

            (bubble, bubbleShadow) = Text("Hands-on observation", 50, new Color(1f, 1f, 1f));
            (label, labelShadow) = Text("Hands-on action label", 42, new Color(.75f, 1f, .96f));
            bubble.gameObject.SetActive(false); label.gameObject.SetActive(false);
        }

        LineRenderer Line(string name, Material material)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(transform, false);
            var line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true; line.numCornerVertices = 2; line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            line.alignment = LineAlignment.View; line.enabled = false;
            return line;
        }

        (TextMesh text, TextMesh shadow) Text(string name, int size, Color color)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TextMesh Make(string n, Transform parent, Color c, Vector3 offset)
            {
                var go = new GameObject(n, typeof(TextMesh));
                go.transform.SetParent(parent, false); go.transform.localPosition = offset;
                var t = go.GetComponent<TextMesh>();
                t.font = font; t.fontSize = size; t.characterSize = .01f; t.anchor = TextAnchor.LowerCenter; t.alignment = TextAlignment.Center;
                t.color = c; go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                return t;
            }
            var main = Make(name, transform, color, Vector3.zero);
            var shadow = Make(name + " shadow", main.transform, new Color(0, 0, 0, .85f), Vector3.zero);
            return (main, shadow);
        }

        // ---------------------------------------------------------------- Windows prompt

        GUIStyle promptStyle;
        void OnGUI()
        {
            if (!IsDesktop || hoveredAction == null || !Active || desktop.View == null) return;
            var screen = desktop.View.WorldToScreenPoint(hoveredPoint);
            if (screen.z <= 0) return;
            if (promptStyle == null)
                promptStyle = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 50f, 13, 26)), wordWrap = true,
                    alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            string verb = hoveredAction.Perform == null ? "" : hoveredAction.HoldSeconds > 0 ? "Mantén clic: " : "Clic: ";
            string text = tooFar ? "Acércate más" + (desktop.Kneeling ? "" : " · Ctrl para arrodillarte") + "\n" + hoveredAction.Label : verb + hoveredAction.Label;
            float width = Mathf.Min(Screen.width * .42f, 560), height = promptStyle.CalcHeight(new GUIContent(text), width) + 12;
            float x = Mathf.Clamp(screen.x - width * .5f, 8, Screen.width - width - 8);
            float y = Mathf.Clamp(Screen.height - screen.y + 34, 8, Screen.height - height - 40);
            var previous = GUI.color;
            GUI.color = new Color(1, 1, 1, .92f);
            GUI.Box(new Rect(x, y, width, height), text, promptStyle);
            if (holding && hoveredAction.HoldSeconds > 0)
            {
                GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(new Rect(x, y + height + 4, width, 7), Texture2D.whiteTexture);
                GUI.color = new Color(.42f, 1f, .45f, 1); GUI.DrawTexture(new Rect(x, y + height + 4, width * HoldProgress, 7), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        void OnDestroy()
        {
            if (heartClip != null) Destroy(heartClip);
            foreach (var m in new[] { accent, warning, progress, dim, placed }) if (m != null) Destroy(m);
        }
    }
}
