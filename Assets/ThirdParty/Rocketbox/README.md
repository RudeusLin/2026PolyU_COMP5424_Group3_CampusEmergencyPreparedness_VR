# Microsoft Rocketbox character used by Campus

Source: https://github.com/microsoft/Microsoft-Rocketbox
Pinned commit: `0943055db6ec570bcef9f2c8b41c9e5467c808f9`
Original directory: `Assets/Avatars/Adults/Male_Adult_08`
License: MIT, copyright Microsoft (2020). The original notice is in `LICENSE.md`.

`MaleAdult08/Male_Adult_08.fbx` is the unchanged upstream skinned FBX. Source SHA-256 hashes, weighted bone names and derived material sections are recorded in `MaleAdult08/Manifest.json`.

The upstream TGA textures were resized to 1024px PNG, preserving the hair alpha channel. Body/head normal and specular source maps are included; the current Unity materials use albedo and normal maps with nonmetallic Standard shading. Derived `*Preview.json` meshes retain authored UVs and normals, relax the shoulders, face +Z, normalize height to 1.74 m and align winding to the normals. Unity uses the native FBX and applies the same scale/shoulder adjustments at build time.

`Tools/import_rocketbox_student.py` reproduces the conversion from downloaded original FBX/TGA files; conversion requires ufbx 0.0.5, SciPy, NumPy and Pillow. Normal campus generation reads the committed derivatives and does not require ufbx/SciPy or any modeling application. No third-party code is added to the runtime.

The original source PNG render was used only to choose a character. Delivered comparison images are our browser renders, not the upstream reference image or Unity captures.
