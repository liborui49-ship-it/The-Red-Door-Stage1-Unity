The Red Door — Stage 1 Unity Export v11

Recommended Unity target: URP + OpenXR, 1 Unity unit = 1 metre.
FBX import scale: 1.0. Axis conversion is already exported as -Z Forward / Y Up.
Do not import the .blend file directly into Unity.

Import order:
1. Import 03_Textures/Environment/v11_UnityURP.
2. Import the four FBX files from this folder.
3. Create URP/Lit materials and assign the maps below.
4. Keep Stage1_RedDoors_Interactive_v11 as separate GameObjects for later interaction.
5. Character rigs are intentionally excluded from these environment FBXs.

URP/Lit texture mapping:
- Base Map: *_BaseColor.png (sRGB ON)
- Normal Map: *_Normal.png (Texture Type = Normal map, sRGB OFF)
- Metallic Map: *_MetallicSmoothness.png (sRGB OFF; R=Metallic, A=Smoothness)
- Occlusion Map: *_AO.png (sRGB OFF)
- Smoothness Source: Metallic Alpha

FBX files:
- environment: 02_FBX_Exports/Environment/v11/Stage1_Environment_Static_v11.fbx
- props: 02_FBX_Exports/Environment/v11/Stage1_Props_Static_v11.fbx
- doors: 02_FBX_Exports/Environment/v11/Stage1_RedDoors_Interactive_v11.fbx
- markers: 02_FBX_Exports/Environment/v11/Stage1_Markers_v11.fbx

Texture sets: 6
Validation: every FBX was re-imported into a clean Blender scene after export.
This does not claim Unity Editor or Quest 3 validation.

Known limitation:
Only the six baked carriage material families have packed URP texture sets in v11.
Small lifestyle props retain their authored Blender material colors and can be upgraded later.
