# Patient presentation integration

**NEEDS_ASSET:** the repository contains no human mesh, patient rig, facial blendshapes, or authored patient animation. The visible anatomical proxy is a procedural review aid, not a finished realistic character. Its skin has no normal/variation maps. Male/female/age variants are not visually represented yet.

`PatientVisualController.Install(patient)` subscribes to the existing patient presentation event. It consumes a detached `PatientSnapshot`; it never changes physiology, actions, scoring or clinical timing. Breathing, awareness, assisted posture and compression use the same component in every environment and platform. `ChestRestPosition` is the undeformed reference for controller input; moving anchors follow the visible surface.

For a licensed replacement, add `PatientRigAdapter` to the character prefab and explicitly assign chest/abdominal motion pivots, head, eyes, skin renderers, and the CPR/AED/arm/finger/thigh/wound/chin anchors. Anchor `up` points out of the body. The wound anchor is only an attachment site; it does not create a clinical injury. Set `provisionalAsset=false` and call `Bind(adapter)`.

Author `contactVolumes` against that replacement's moving anatomy. The proxy supplies 11 oriented volumes covering torso, head, arms/hands and legs/feet. These geometry checks do not add physics colliders or XR targets. Their configurable world-space margin approximates controller grip proximity; hand offset calibration still needs headset testing. The DEA contact signal remains independent of CPR grip and compression state.

Requested assets: adult male/female Humanoid variants with natural face/hands and commercial redistribution rights; skin and clothing texture atlases; supine, standing, seated, recovery, cough, choking, controlled collapse and seizure/postictal clips. Start with a measured Quest budget of one patient, a few shared materials and 1K–2K textures; confirm actual GPU cost on hardware before setting the final triangle/texture limits. No asset price or vendor has been verified.

The built-in Animation module is absent in the current project. Humanoid/Animator integration is conditionally compiled with `VITALVR_ANIMATION`; no module was installed. Optional controller parameters are `PatientConsciousness`, `PatientPosture`, `PatientExpression` (integer) and `PatientSeizure` (boolean). Face channel names are configurable on the adapter. Authored animation remains an asset requirement.

`PatientControlledRagdoll` requires a real connected skeleton with explicitly configured rigidbodies, joints and convex/simple colliders. It is inactive for the proxy. Falling uses bounded velocities, then freezes after settling; returning to the captured authored pose is an explicit presentation command. This adapter has not been validated with a production human or Quest hardware.

All procedural hand offsets, motion amplitudes and pose durations are visual interaction settings. They are not validated clinical measurements. Medical protocols and thresholds remain **PENDING MEDICAL VALIDATION**.
