# The Red Door — Stage 1 Vertical Slice v01

## Open this scene

`Assets/TheRedDoor/Stage1/Scenes/Stage1_VerticalSlice_v01.unity`

The original `Stage1_Quest3_SmokeTest.unity` scene is preserved and disabled in
Build Settings. The vertical-slice scene is now the first enabled build scene.

## Controls

- Quest 3 left controller stick: continuous movement at 1.2 m/s.
- Quest 3 right controller stick: 30-degree snap turn.
- Unity Editor fallback: WASD to move and Q/E to snap turn.

The first red door opens once when the headset comes within 1.25 metres. The
door remains physically blocking while closed and releases its blocking
collider when the opening animation is almost complete.

## Included in this iteration

- Character-controller locomotion foundation.
- Simplified carriage, vestibule, seat, table, luggage, and door collision.
- First-red-door proximity interaction framework.
- Mixed-light atmosphere baseline with five no-shadow lights.
- Thirty-six light probes and two baked reflection-probe placeholders.
- Fresh-process Unity validation of materials, build-scene order, door passage,
  and the centre aisle.

## Explicitly not validated yet

- No APK was built or run on Quest 3.
- No final lightmap or reflection-probe bake was produced.
- No controller-hand interaction or grab-to-open door behaviour was added.
- No passenger animation, smoke particles, FMOD, or final audio was added.

The next iteration should be an on-device Quest 3 locomotion and comfort check.
After that passes, bake and tune the lighting before adding passenger animation,
smoke particles, and spatial audio.
