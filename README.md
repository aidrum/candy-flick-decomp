<p align="center">
  <img alt="Candy Flick+ Logo" src="./logo.png" width="350"/>
</p>

# Om Nom: Candy Flick +

By aidrum

Om Nom: Candy Flick + is an enhanced decompilation of the iOS game Om Nom: Candy Flick, ported to Unity 6.4, adding new modes and taking full advantage of the updated Vuforia library.

## How was this made?

Using AI, Ghidra, IL2CppDumper and ILSpy, the game's code was reconstructed and adapted for Unity 6.4, targeting Android devices.

While it is vibecoded, it has been tested properly, with big human oversight, and all behavior should be practically identical to the original game. Please open an issue if you encounter any bugs or inconsistencies. 

## Why was this made?

This was made due to the original game being locked into old iOS devices forever, wanting Android players to finally experience it, and fulfilling more potential of what was essentially a tech demo. While the game does have a 64-bit version that works on modern iOS devices, the game is bugged, and the camera output is fully black.

## Download

The latest release can be found [here](https://github.com/aidrum/candy-flick-decomp/releases/latest/) (Android only).


## FAQ

- **Will this get any more updates?**

  If I think of cool stuff to add, likely. If I have to fix bugs, definitely.

- **Will any other games get the same treatment?**

  A full decompilation of any other ZeptoLab iOS game is unlikely, at least if done the way I did it. It worked with Candy Flick because it's a very simple game.

- **What devices are supported?**

  Any Android device with arm64 architecture running Android 7.0 or later.

- **What devices was this tested on?**

  - Redmi Note 13 Pro 5G (Android 13): Great performance.
  - Redmi Note 8 Pro (Android 11): Great performance.
  - Samsung Galaxy A54 (Android ): Great performance.
  - LG K61: Good enough performance. (Does not support AR Ground mode)
  

- **Was AI used to generate graphics or music?**

  Nope. AI was purely used for the game code. Graphical edits and music additions were all done manually. This readme was written by hand too.

- **Why was AI used for the code?**

  We do not have the time or resources needed to do a full analysis of the iOS code manually. We playtested it for a while, even comparing it with real hardware (iPad 3 and iPhone 4S) to ensure the game was working right/accurately.
  
## Model credits
- ["Coffee Mug (School Project)"](https://sketchfab.com/3d-models/coffee-mug-school-project-5f5ccee1514c440887c072fae8e0d699) by Ole Gunnar Isager is licensed under [Creative Commons Attribution](https://creativecommons.org/licenses/by/4.0/).
- ["Lunch Plate (School Project)"](https://sketchfab.com/3d-models/lunch-plate-school-project-eef24ebe601c4e2f99da3108ddc3b09b) by Ole Gunnar Isager is licensed under [Creative Commons Attribution](https://creativecommons.org/licenses/by/4.0/).
- ["Cookie (School Project)"](https://sketchfab.com/3d-models/cookie-school-project-434b04afb75d4525ae5eb272282d736d) by Ole Gunnar Isager is licensed under [Creative Commons Attribution](https://creativecommons.org/licenses/by/4.0/).
- Om Nelle Triple Treat 3D model by [MlopsFunny](https://github.com/mlopsfunny/) on [CtR Home Discord](https://ctrhome.github.io/discord/)

<hr>
All rights to original code and assets belong to ZeptoLab. No profits are made from this game.