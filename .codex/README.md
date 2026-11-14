# Om Nom Candy Flick - Unity 6.4 port

Open this repository folder in Unity **6000.4.0f1**, then open `Assets/Scenes/CandyFlick.unity`. The installed editor is `D:\Programs\Unity\6000.4.0f1\Editor\Unity.exe`.

The target is the original **2.6 behavior and feel**. The initial reconstruction has been replaced in the main gameplay paths with translations traced from the 2.6 ARM executable and recovered serialized objects. This is an **in-progress port**, with remaining differences listed in [Documentation/RECOVERY.md](Documentation/RECOVERY.md). Passing the current checks does not establish complete 1:1 parity.

## Current behavior

- Slingshot is the default, matching native Tweaker mode 2. Touch the candy, drag down, then release. The port adds a settings button to switch between Slingshot and Flick, saving the selection between launches. Both use recovered 2.6 input/launch code.
- Original gesture angle, power scaling, launch impulses, relative torque, scoring reference point, and stage progression are used. The invented ballistic aim solver and trajectory overlay have been removed.
- Original scene units, hierarchy, collider shapes, camera FOV, gravity, 100 Hz physics step and angular velocity limit are retained.
- Rings use the original prefab planes, textures, animation curves and recovered particle shader equations. The five removed legacy emitter components now have Unity 6 adapters using their original particle settings and materials.
- Menu, gameplay HUD, results and settings use recovered GUI skins, native rectangle calculations, fonts and localization tables. The original five-page first-run tutorial, marker options, About/Reset screens, and GUI-call-based transitions are restored.
- Android Play starts Vuforia Engine 11.4.4 image tracking without ARCore. Both original target cards are retained; print width is 20 cm. The editor uses mouse input and the original saved untracked camera pose as a development preview. Its composition is not a reference for a tracked phone pose.

Vuforia is the current SDK from the same QCAR/Vuforia family used by the original iOS game. The port loads the original 2.6 `CandyFlick.dat` feature database and XML directly. Vuforia handles camera capture, calibration, tracking and live video rendering. The previous OpenCV prototype and buffered camera preview are retired. Only visually TRACKED images show Om Nom; device/world tracking and retained anchors are disabled.

The native Vuforia SDK initializes and loads both original targets in its simulator. Physical Android acquisition range and performance still need a device check.

## Build and checks

The Android APK is `Builds/Android/CandyFlick.apk`. Use the Unity menu **Candy Flick > Build Android APK**, or close the editor and run:

```powershell
.\Tools\Unity.ps1 Prepare
.\Tools\Unity.ps1 Vuforia
.\Tools\Unity.ps1 Test
.\Tools\Unity.ps1 Android
.\Tools\Python\Scripts\python.exe .\Tools\verify_apk.py
```

For an already open editor, `Tools/Invoke-EditorCommand.ps1` queues `Prepare`, `Native`, `Ring`, `Test`, `Visual`, `Vuforia` or `Android`. Test commands enter and leave Play mode. The command result is written to `Temp/candyflick-command-result.txt`.

The Android application ID is `com.aidrum.candyflick`, stored in Project Settings; Prepare/build scripts do not override it.

Android uses ARM64 IL2CPP, OpenGL ES 3, API 29 minimum (Android 10) and target API 36. Builds are signed with the development key.

Evidence:

- `Documentation/native26-validation.json`: 96 comparisons against arithmetic and touch-region functions executed from the actual 2.6 ARM binary.
- `Documentation/validation.json`: controlled Unity play-mode checks of input, real Rigidbody flight, pause, original geometry and ring creation.
- `Documentation/ring26-validation.json`: original fade/hit animation samples and shader property binding.
- `Documentation/vuforia-validation.json`: native Vuforia initialization and original 2.6 database loading. The older `image-tracker-validation.json` is historical evidence for the retired prototype.
- `Documentation/screen26-validation.json`: tutorial entry, page transitions, pause/resume, marker options and live super-candy checks.
- `Documentation/Visual26/`: actual 640x1136 Unity screen captures; the results image is explicitly an empty-score rendering fixture.
- `Documentation/last-build.json` and `Documentation/android-package.json`: build/package verification evidence. Check their timestamps when evaluating a particular APK.

## Recovery workflow

The supplied IPA and `assetripper/` remain outside `Assets`. The 2.6 executable and serialized data are authoritative. The fallback 2.2 managed assemblies were inspected but contain stripped method bodies; no 2.2 runtime implementation is used.

`Documentation/Recovered/dump.cs` contains declarations, not recovered C# bodies. `Documentation/Recovered/Native26/` contains 1,139 typed Ghidra native decompilations, annotated with original string literals. Decompiled NEON expressions and conditional instructions are checked against disassembly or emulation where needed.

Recovery scripts under `Tools/` generate reproducible evidence:

- `recover_serialized26.py`: 77 managed component records and all 10 complete GUISkins, with exact binary consumption.
- `recover_geometry26.py`: original scene and ring/heart hierarchy, preserving units, layers and transforms.
- `recover_gui_animation26.py`: original GUITexture curves with a Unity 6 component binding.
- `recover_shaders26.py`: all 39 original compiled ShaderLab/GLSL programs for shader porting.
- `native26_oracle.py` and `native26_layout.py`: execute original ARM launch/UI functions.

`prepare_project.py` copies only missing assets and preserves restored skins, material edits and shader ports. The historical normalized preview prefabs in `Resources/Recovered` are unused by active gameplay; the port uses `Resources/Original26`.

## Workspace Ghidra MCP

Ghidra 12.1.4, Ghidra MCP 2.0, Il2CppDumper 6.7.46 and an isolated Python environment are installed under `Tools/` (large tool installations are ignored by Git).

```powershell
.\Tools\Start-GhidraMCP.ps1 -Http
.\Tools\Python\Scripts\python.exe .\Tools\mcp_client.py get_program_info
```

The server binds to `127.0.0.1:8081/mcp`. Project-local Codex and VS Code configurations point to it. The Ghidra database lives at `%TEMP%\CandyFlickGhidraProjects`, because Ghidra rejects paths containing dot-prefixed segments such as `.pages`. Only one process can open the database at a time.

Original assets and trademarks belong to ZeptoLab.

The marker Download action saves the unmodified PNG from the 2.6 IPA using Android's document picker. Print opens Android's system print dialog with a 200 mm target on A4 paper. Neither action needs storage permission. Game Center remains an unavailable iOS service; its original buttons are retained.

## Vuforia setup

The official user-provided Vuforia 11.4.4 Unity package is installed through `Packages/com.ptc.vuforia.engine-11.4.4.tgz`. The large package uses Git LFS. Place the port's free Vuforia Basic app license key in `key.txt`, run Prepare, and accept Vuforia's Developer Agreement in Unity if it has not already been accepted. The key and generated `Assets/Resources/VuforiaConfiguration.asset` are excluded from Git. The built app necessarily contains its Vuforia app license; it does not contain account credentials.

Vuforia desktop AR validation requires Direct3D11: `Tools/Unity.ps1 Vuforia` passes `-force-d3d11`. Normal editor Play remains the untracked development preview. Android uses OpenGL ES 3, continuous autofocus and the SDK's quality camera preset. See [Documentation/TRACKING.md](Documentation/TRACKING.md) for migration evidence and limits.
