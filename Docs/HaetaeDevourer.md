# Haetae Rank 4 — 천명 집행자
2026-09-23. Implemented and verified in Unity 2022.3.62f2.

## Behavior
At battle start, each team with a living, transcended Haetae gets a devouring beast. All opposing combatants are denied temporary numerical buffs, healing (including periodic healing/lifesteal and overflow), and shields before application. Opening benefits already granted during setup are removed. Harmful effects remain; Spirit Deer immunity cannot remove this support-reception rule. Base stats, permanent equipment/passive stats, transformations, summons, and initial resurrection HP remain. Mantis cocoon regeneration is healing and is blocked.

Multiple Haetae share one visual per team. The last living caster falling releases the block immediately; future grants work again, consumed benefits do not return. Resurrection of a registered caster can reactivate the rule. Bench units and shadow clones cannot activate it. Spawned recipients bind the same callbacks. Battle reset/end clears state and visuals.

Basic attacks and selected Rank 3 skills remain. Removed the old six-second avatar / periodic 70% roar. This Rank 4 adds no damage.

## Visuals
A bronze-horned, jade-maned devouring beast appears at the right edge of the battlefield background, facing left; enemy-team effects mirror to the left. A new 16-frame sprite sheet covers idle, mouth opening, inhaling and swallowing. Fixed sprite orientation; no code-driven body rotation.
Appearance: 0.9s pixel dissolve; disappearance after loss of the last caster: 0.65s. Body alpha 0.75. Streams travel on accelerating curves into the mouth in 0.55–0.85s. Healing green, shield blue, enhancement gold. Battle clock freezes animation/particles on pause. Visual packets are throttled per target/kind (0.3s), capped at 32; gameplay blocking is never throttled.

Art created with the built-in image generation tool, not Aseprite scripting.
Saved asset: Assets/Resources/CombatEffects/Haetae/Devourer16.png
Source: C:/Users/<user>/.codex/generated_images/01a0944c-2056-72d0-ac5d-ac423eefe8f8/exec-b22df23a-2c10-472c-819d-f9d6a9f25337.png
Production prompt brief: a transparent 4×4 sheet of 16 consistent pixel-art frames for an original mythical devouring beast inspired by Haetae/Taotie; left-facing three-quarter side view, massive jaw, bronze horns, curled jade mane, amber mouth; match the game's coarse pixel density and Haetae/Tyranno effect references. Frames 0–3 idle, 4–7 mouth opening, 8–10 inhale, 11–14 swallowing with a throat glow, 15 settle. Identical scale, registration and silhouette direction; no labels, scene background or UI.
Generated image retained unchanged; runtime sprite slicing, Point filtering and a bottom-fading dissolve shader handle presentation.

## Data
Local GameMasterDatabase and LocalizationDatabase updated.
Google DB verified identical after write:
- Trait_Master!E202,F202,H202: trigger, effect, version 2.
- Localization_Master!D1182,E1182,I1182: Korean, English, version 2.
Native cells were re-read and compared against Logs/HaetaeDevour/data-manifest.json. Formatting/validation preserved. Both Google-rendered rows visually checked at 100% zoom; text wraps and neighboring rows remain intact.
Spreadsheet: https://docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/edit

Judgment Bellow's older six-cell Google sync (Trait_Master!C180,F180,H180; Localization_Master!D803,E803,I803) was separately approved by the user and completed on 2026-09-23. All six cells match the local master/localization, with original formatting and validation preserved. The prior approval block is resolved; evidence is in Logs/HaetaeBellow/google-verified.json.

## Verification
- Initial focused suite: 18/18 passed.
- Full EditMode suite executed 1,926 tests and reported 9 failures. None were in the new Haetae gate tests.
- Corrected one stale Bellow test expectation from 320% to the previously implemented 280%.
- Final affected suite: 67/67 passed, 0 skipped (1.0844203s).
- Remaining eight full-suite failures concern existing bench reflection signature, Bear concise tooltip expectation, two English localization coverage checks, two Fang Lock tooltip expectations, currency drops and wave composition. These were not changed to conceal unrelated failures; no clean full-suite pass is claimed.
- Manual live battle: blocked 900 HP healing leaves HP 5000; shield absent; Forest Deer haste timer remains zero. After caster death, the same heal reaches HP 5900 and the shield is accepted.
- Captured appearance, absorption, swallowing and fade frames; adjusted screen margin and mouth endpoint after visual inspection.
- Final Unity console: 0 errors/warnings.
- Test configuration restored; original CombatEffectTest scene unchanged and not dirty; play mode stopped.

Evidence: Logs/HaetaeDevour/{focused-regression.json,full-regression.json,capture-results.txt,google-verified.json,frame-*.png}.

## Consumed buff icon fix — 2026-09-23
Reproduced the reported issue with the configured Ironkuma opponents: BenefitsBlocked was true, but the HUD still collected AttackUp directly from bearResolveTimer. That timer intentionally keeps the skill animation and Rank 3 follow-up running after its numerical buff is consumed. The HUD now checks BearResolveStrength, the same validity check used by combat calculations, before adding that representative icon. Existing debuffs and actual action immunity remain visible; a newly granted buff after Haetae dies appears normally and disappears on expiry.
Regression coverage uses the real ApplyBearFightingSpirit grant path and checks blocked stats, absent AttackUp, retained Burning/ControlImmunity, and normal subsequent casts. Related Haetae/HUD/Bear suites: 85/85 passed. No balance, description, or Google DB changes were needed for this display fix. Evidence: Logs/HaetaeDevour/icon-fix-regression.json.
