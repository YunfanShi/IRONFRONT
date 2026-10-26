# Slide integration — 2026-10-26

User approved using the verified free Quaternius UAL2 slide clips. Left Alt / Mac left Option triggers from a grounded sprint; C remains crouch and Ctrl remains prone. Preserve current art and existing work.

Baseline: HEAD 9826942 matches origin/codex/unity-port (0/0), origin/main divergent (37/5). Local work already contains equipment and weapon replacement work at 0.65.0. Saved touched baseline files and 653 art/menu/weapon resource hashes. No original model is changed. New feature version: 0.66.0. Local build: UnityProject/Builds/Slide-0.66.0/IRONFRONT.app.

Source archive: source/Universal-Animation-Library-2-Standard.zip; SHA256 4008ea208a604773a2b2177d965f0f5d3195498b5bf838c3f5785d68e95f2a68. Official https://quaternius.itch.io/universal-animation-library-2 ; CC0. Three clips Slide_Start (0.833333s), Slide_Loop (2s), Slide_Exit (0.5s). sample_slide.py samples 60 Hz semantic motion from the official GLB into Resources/Animation/InfantrySlide.json. All original limb lengths retained. Existing source FBX also retained locally.

Implemented shared slide state, local Alt/crouch/aim/collision handling, lower camera, sampled lower body/chest plus existing weapon grip IK, host validated input + replicated slide phase. 1.08s natural action, speed starts from actual sprint (max6.6), ends2; 0.55s recovery cooldown. Early ADS/collision recovery lasts0.3s. Forward+Shift can stand at the end only with clearance. Other exits crouch.

Evidence: build.log and build-02.log passed Unity macOS development builds. probe-01 found ankle below floor during rotational blending; fixed by final ankle contact IK in probe-02. min ankle now0.1399976m, max grip0.00004333m, no runtime errors. Actual side/front/FP captures inspected. Natural slide4.607m; continuous sprint exit4.761m. Probe02 stopped on a test bookkeeping issue: frame-rate distance used instantaneous IsSliding instead of previous frame; fixed in probe03. Probe03 adds low ceiling WITH Shift held, actual wall-stop assertion, and all weapon grip variants.

Final acceptance: build-03 PASS (808,051,246 bytes). probe-03 PASS, RuntimeErrors=0; max grip error0.00004333m, bone length error0.00002763m, min ankle height0.1399976m. Default distance30/60/120Hz=4.607361/4.611664/4.631104m. Wall, low ceiling with Shift, ADS recovery, Alt latch, all firearms, host authority and phase roundtrip pass. Existing sprint and actual UI regressions PASS, RuntimeErrors=0. Server protocol regression PASS. 653 protected resource hashes unchanged.

Normal-speed side/front/FP video slide-0.66.0.mp4 (960x640,30fps,5.7s) reviewed in QuickTime. New app successfully opened and entered the real match; native C input entered CROUCHED. Native input automation releases held keys between calls, so the attempted two-call Alt sequence is not claimed as manual slide acceptance; the runtime probe validates Alt handling and movement. App relaunched normally and visually confirmed at the lobby, ready for user.

Fresh independent Blender rerun reproduced InfantrySlide.json byte-for-byte, SHA256 f6ed40a922619e13f6bfa55a4e798ff4931368720073f21c240333019ad76b71. Source/CC0 preserved, SOURCES.md and docs/UNITY_HANDOFF_066.md added; README/CHANGELOG/PROGRESS and outer handoff updated. delivery-manifest.json records final source and built assembly hashes.

Remaining validation boundaries: two-machine/high-latency LAN visual check and complex slopes/stairs not performed; no dedicated slide audio/dust. No remaining required implementation or failed checks. No commit/push: pre-existing mixed equipment/weapon work and reference deletions preserved; do not merge Unity into web main.
