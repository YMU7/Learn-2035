# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

XR Classroom: a Unity base project for prototyping networked multiplayer VR classrooms. It is a trimmed-down fork of Unity's **VR Multiplayer template** (XR Interaction Toolkit 3.3, Netcode for GameObjects 2.7, Unity Gaming Services: Authentication, Multiplayer Sessions, Vivox). It targets OpenXR on Android (standalone headsets) and Standalone. The main scene is `Assets/Scenes/XRClassroom.unity`.

**Unity version is pinned to `6000.0.51f1`** (`ProjectSettings/ProjectVersion.txt`). Never open or upgrade it with another editor version. That version is installed at `C:/Program Files/Unity/Hub/Editor/6000.0.51f1`.

## Project rules (current; may change)

- This is a shared repo. Work only on the user's own branch (currently `interaction-flow`). Never commit to `master`, the main branch.
- Build assets and interactions with Editor scripts in `Assets/Editor/`, exposed as menu items (`[MenuItem("XR Classroom/...")]`). The user runs them in the editor, or they can be triggered through `EditorCommandQueue`.
- Never edit `.unity`, `.prefab`, or `.meta` files directly. Make scene and prefab changes through Editor scripts.
- Make assets from Unity primitives or simple generated meshes: untextured and low-poly.
- For interactions, use the XR Interaction Toolkit components already on the template's rig (interactors, `XRGrabInteractable`, sockets, poke, and so on). Don't add a new rig or a third-party interaction system.

## Building, compiling, testing

There is no CLI build script and there are no automated tests. Work is verified in the Unity Editor.

- **Compile check while the editor is closed** (batch mode refuses to open a project the editor already has open):
  ```bash
  "/c/Program Files/Unity/Hub/Editor/6000.0.51f1/Editor/Unity.exe" -batchmode -quit -projectPath . -logFile -
  ```
  Then search the output for `error CS`.
- **Driving the open editor from outside**: `Assets/Editor/EditorCommandQueue.cs` polls `Temp/EditorCommands/*.cmd` once a second. Write one command per line (`refresh`, `open-scene Assets/Scenes/Foo.unity`, `save`, `play 5`, `enable-openxr-feature <FeatureClassName>`). Results go to the Console and are appended to `Temp/EditorCommands/results.log`. `open-scene` refuses if the current scene has unsaved changes. Use this to trigger a recompile (`refresh`) or a short Play-mode smoke test, then read `results.log` and `Editor.log` (`%LOCALAPPDATA%/Unity/Editor/Editor.log`) for errors.
- **Multiplayer testing without a second headset**: `Assets/Tools/Two Player Scenario.asset` is a Multiplayer Play Mode config (Window → Multiplayer Play Mode).
- **Voice chat in the Editor** requires Vivox Test Mode (Project Settings → Services → Vivox). Never ship a build with Test Mode on.

## Code layout

- `Assets/Scripts/`: XR Classroom's own code, namespace `XRClassroom`, compiled into `Assembly-CSharp` (no asmdef). It references the template code in `VRMP.asmdef`.
- `Assets/VRMPAssets/`: what is left of the original template (`VRMP.asmdef`). Its `Scripts/Network` folder is load-bearing. The rest mixes required pieces with unaudited leftovers, so check what references something before deleting it.
- `Assets/Editor/`: editor-only tooling (`XRClassroom.EditorTools`).
- `Assets/XR`, `Assets/XRI`, `Assets/XRTKSamples`: XR Toolkit settings and imported samples. The player rig is a prefab-variant chain running through `XRTKSamples`, so don't delete or restructure these.
- Scenes, prefabs, and assets are Force Text YAML (see `.gitattributes`). They are readable for inspection, but change them only through Editor scripts (see Project rules). Every asset needs its `.meta` file, and Unity generates it.

## Architecture

### Networking entry points
- `XRINetworkGameManager` (singleton, `XRINetworkGameManager.Instance`) owns the session lifecycle. Use `XRINetworkGameManager.Instance.sessionManager` (`SessionManager`) to create, join, or leave a session. The template's lobby browser was replaced with a single "Join Online" quick-join/create.
- To check online state, use `XRINetworkGameManager.Connected` (a bindable bool: `.Value` or `.Subscribe(...)`) or `CurrentConnectionState`, not `SessionManager`. `ConnectionUIController` is the reference example.
- `XRINetworkPlayer` is the per-player NetworkBehaviour. It replicates head/hand transforms and connects to Vivox voice (`VoiceChatManager`).

### Two copies of every avatar (the most common source of confusion)
- `Assets/Prefabs/Player/XRClassroomLocalRig`: the local XR rig (camera, controllers, interactors). It is **not networked** and exists once per client.
- `Assets/Prefabs/Player/XRClassroomAvatarBase`: the networked avatar spawned per player by the Network Manager. The owner's copy is driven by its local rig, and remote copies are driven by replicated data.
- A bug that looks correct locally but wrong to others (or the reverse) usually means the change went to the wrong prefab.

### Part-based avatars (no skeletons)
Avatars are separate head, body, and hand GameObjects placed by script. The pose pipeline uses `DefaultExecutionOrder` to run in sequence:
1. Sources: `LocalHeadPoseSource` (-100) copies the XR camera pose. `LocalHandPoseSource` (50) picks controller or tracked-hand transforms through `XRInputModalityManager`.
2. Presentation: `AvatarPositioner` (100) moves the avatar parts from self poses (`isSelf`) or from `XRINetworkPlayer`'s replicated poses, with per-model offset and rotation fields set in the Inspector. `AvatarPosePresentation` is a single-head-pose variant.
3. Owner-only hiding: `LocalAvatarHiddenRenderers` hides face parts from the owning player's camera. `OfflineAvatarVisibility` toggles the offline preview based on `Connected`.

Hands are not networked yet. Remote players cannot see your hands.

### Costume closet
`CostumeCloset`, `WearableGarment`, and `AvatarCostumeSync` handle avatar customization. Every client has the same closet in the scene, so only the garment's **GameObject name** is sent over the network (a `FixedString64Bytes` NetworkVariable for head and for body), and each client looks the name up in its local copy. Garment names must be unique and stable. Renaming or duplicating a garment breaks sync.

### Other conventions
- `AvatarVoiceSignal` turns Vivox voice level into a 0–1 loudness value plus a UnityEvent. Voice-driven effects (`MouthMotion`, etc.) subscribe to it. `XRClassroomAvatarVisuals` overrides the template's `UpdateMouth` so it no longer hard-codes blend shape 0.
- `Boundary` (a trigger volume) rescues only the **local** player. It goes through `TeleportationProvider` rather than writing `transform.position`, drops held objects, clears fall velocity, and has a cooldown. Moving the player any other way fights the `CharacterController`.
- `HandLocomotionMode` forces both hands to the same locomotion style. The XRI starter rig is asymmetric by default: left hand walks, right hand teleports.
- Several scripts note in their headers that they were written by Codex and need human review (for example `AvatarCostumeSync`). Be skeptical of their complexity.

## Further docs
- `README.md`: user-facing guide (environment and skybox, adding garments, known bugs).
- `Assets/AVATAR_GUIDE.md`: preparing avatar models in Blender. Possibly out of date.
