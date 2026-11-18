Om Nelle source assets supplied from `D:\Downloads\candyflick\omnelle`.

Use **Candy Flick > Import Om Nelle** to regenerate the runtime meshes, materials, prefab and voice profile. Then use **Candy Flick > Generate skin menu assets** to regenerate portraits.

The FBX's duplicate unused control hierarchy is removed. Its body and face follow the original Candy Flick animation clips through bind-space corrections. The eyelid caps are refined and fitted outside the white eye surfaces. Their complete eye-and-mouth skin weights are transferred from the whites, so the lids follow the same stretch, squash and facial deformation. Each eyelash triangle is weighted to its own eye. The pupil patches are lowered and projected onto the actual eye-white surfaces to remain visible below the lids. Actor scale and gameplay colliders are untouched.

The eyelid mesh uses `omnelle_lashes.png` through its own material. The body keeps `omnelle.png`, and the separate eye-attached eyelash triangles keep `eyelashes.png`.

Voice mapping:

| Source | Game cue |
| --- | --- |
| open_mouth.wav | Open |
| close_mouth.wav | Close |
| sad.wav | Sad, with the original 0.603855-second lead-in |
| petting_tap.wav | Tap |
| petting_hold.wav | Pet |
| Original Om Nom chewing | Chew |

All six cues are covered. WAV samples are preserved at 48 kHz mono PCM; only the sadness lead-in is added. Source files remain unchanged and are editor-only; runtime assets live in `Assets/OmNelle` and `Assets/Resources`.

