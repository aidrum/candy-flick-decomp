Om Nelle source assets supplied from `D:\Downloads\candyflick\omnelle`.

Use **Candy Flick > Import Om Nelle** to regenerate the runtime meshes, materials, prefab and voice profile. Then use **Candy Flick > Generate skin menu assets** to regenerate portraits.

The replacement is **CFlickNelle.fbx by MlopsFunny**, stored here as `OmNelle.fbx`. Its supplied `omnom1.etc1.png` atlas is stored as `omnelle.png`.

The rig's scale is matched to the original actor's bind pose. Candy Flick's native animation joints drive the mesh directly, preserving complete affine stretch and shear. The eyelid caps attach to the deformed white-eye triangles, including their stretching and squashing, with a small surface clearance. The caps are subdivided once so their interiors follow the curved whites. Pupils animate from the model's authored geometry, then shift down by 0.035 actor units and project onto the animated eye surface each frame. Applying the lowering after animation preserves the authored alignment rather than rotating a bind-space offset differently for each eye. Lashes retain their authored skin weights. These surface meshes are independent for each Photo mode actor. Actor scale, candy/touch colliders and sounds are unchanged.

Eye-surface searches use a per-instance bounds hierarchy refitted to the animated whites. Normals are cached per posed vertex, and unchanged poses skip mesh updates. The hierarchy still checks the foremost eye surface when faces fold over each other. `Candy Flick > Profile skin animation CPU cost` benchmarks the extra skin work separately from shared animation and rendering.

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

