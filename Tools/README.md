# Tools

- `Build.ps1`, `SmokeTest.ps1`: EditMode tests and the Windows build; the built-player scenario.
- `UiArt/`: the scripts that cut the gameplay UI out of the mockups (`GameplayUI/Selected` of the art drop, or the folder in the
  `HOMA_UI_MOCKUPS` environment variable). `python build_ui.py` erases the mockups' texts, cuts the viewports and writes the
  sprites into `Content/UI/Sprites/Gameplay/` and one layout per screen into `Editor/UiLayouts/` for `GameplayUiSeeder`;
  `python build_common.py` draws the button glow, the check mark and the map objects' placeholder tokens (RPG Awesome icon
  font, SIL OFL, `UiArt/icons/LICENSE.md`); `gloves.py` forges the Gloves of Metal icon. Needs Python 3 with Pillow, NumPy and
  opencv-python-headless. One-off tools: the project works without them and they can be deleted.
- `UiArt/build_magic.py`: draws the placeholder secondary skill and spell icons, the open spell book, the spell shop and luck
  shrine map tokens and synthesizes the spell cast and luck cues (`python build_magic.py`, Pillow and NumPy). One-off tool: the
  project works without it once the files exist.
- `UiArt/revise_panels.py`: narrows the adventure screen's right panel to 400 pixels (the map viewport grows; warhost slots and the ride
  button painted over; the warhost box's leftover top edge and the ornate divider in the middle of the mission card erased), adds rows to the victory frame for the casualties and erases its
  experience and losses icons, builds the defeat frame from the victory frame without the hero and the single button's slot, and covers
  the portraits painted into the dialogue box frame and the war council's portrait slot (`python revise_panels.py`, Pillow and NumPy; run after `build_ui.py`, a second run changes nothing). `PanelRevisionSeeder` placed the
  controls over the new art. One-off tool: the project works without it once the files exist.
- `MapLogic/`: `terrain.py` derives the Ashen Pass terrain logic from the painted map by colour, `design.py` shapes it for the
  mission (castle, portal clearing, the maiden's pocket) and writes `Editor/MapData/AshenPassTerrain.json` input for
  `MissionContentSeeder` (copy `%TEMP%/homa-map/map/final_terrain.json` there). One-off tools, safe to delete.
- The Ashen Pass image was upscaled from the art drop's `map-1-expanded-generated-source.png` (1605 x 980) with
  [Real-ESRGAN ncnn Vulkan](https://github.com/xinntao/Real-ESRGAN) (`realesrgan-ncnn-vulkan -n realesrgan-x4plus`, BSD-3) and
  cropped by 10 px left and 8 px top to 6400 x 3904, the same geometry as the Lanczos export, so the logic layer stays aligned.
