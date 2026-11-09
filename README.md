# Skeleton Waves VR

A first-person survival game for **Meta Quest** (Android, standalone VR). Skeletons come at you in endless, escalating waves. You collect gems, trade them at a wishing well for better swords, and try to last as long as you can.

<p align="center">
  <img src="docs/media/03-skeleton-wave.jpg" width="32%" alt="A pack of skeletons closing in">
  <img src="docs/media/02-wishing-well.jpg" width="32%" alt="The wishing well">
  <img src="docs/media/01-gems.jpg" width="32%" alt="Dropped gems glowing on the ground">
</p>

**Demo video (with sound):** [`docs/media/demo.mp4`](docs/media/demo.mp4). A full 5-minute run on device: waves 1 to 5, the well, a sword upgrade and the death screen. The small inset at the bottom left is the headset's stereo view.

| | |
|---|---|
| Engine | Unity 6000.3.8f1, URP 17.3 |
| Target | Meta Quest (Android, IL2CPP, min SDK 29) with the Oculus and OpenXR loaders |
| Language | C# (about 3,700 lines in `Assets/Scripts`) |
| Input | Unity Input System: gamepad, keyboard and mouse, plus headset look |
| Editor testing | Works with keyboard and mouse in the editor, no headset needed |

## Gameplay loop

1. **Survive the wave.** A wave spawns around you. Clear every skeleton to start the next.
2. **Loot.** Skeletons sometimes drop gems that despawn after 30 seconds. A barrel somewhere on the map refills with 1 to 3 gems at a random time between 15 seconds and 4 minutes after you last looted it, and glows and plays a looping sound when it is ready.
3. **Wish.** Throw gems into the wishing well, then make a wish. More gems means better odds of a stronger sword.
4. **Fight.** Swords have durability and break. Pick up the new one, drop the old one.
5. **Heal.** Stand in the healing tower's radius to recover 8 health every 2 seconds.
6. **Die.** Your survival time and wave count are shown and you can play again.

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move | `W` `A` `S` `D` | Left stick |
| Look | Mouse | Headset (right stick turns the body) |
| Sprint | `Shift` | Left stick click |
| Jump | `Space` | `A` / South |
| Attack | Left click | Right trigger |
| Pick up or drop sword | `E` | `Y` / North |
| Loot barrel | `E` | `X` / West |
| Make a wish at the well | `E` | `X` or `Y` |
| Throw a gem | `Q` | `B` / East |

## How it works

### Wave scaling (`InfiniteWaveController.cs`)

Each wave is computed from a formula, so the game has no hand-authored wave table and can scale forever. With the values used in the scene:

| Wave | Skeletons | Skeleton speed | Hit damage | Gem drop chance | Hive mind |
|---|---|---|---|---|---|
| 1 | 1 | 2.8 | 8 | 70% | no |
| 2 | 3 | 3.2 | 10 | 66% | no |
| 3 | 6 | 3.6 | 12 | 62% | no |
| 4 | 11 | 4.0 | 14 | 58% | yes |
| 5 | 18 | 4.4 | 16 | 54% | yes |
| 6 | 25 | 4.8 | 18 | 50% | no |
| 7 | 34 | 5.2 | 20 | 46% | yes |

- **Count:** `start + (wave - 1)^1.65 * 1.7`, rounded. Growth is faster than linear but slower than quadratic.
- **Speed:** +0.4 per wave, capped at 90% of the player's sprint speed, so enemies never become impossible to outrun.
- **Damage:** +2 per wave, capped at 40.
- **Gem drops:** fall 4% per wave to a floor of 30%, so rewards shrink while danger grows.
- **Hive mind:** on from wave 3 except every third wave. Waves alternate between coordinated and uncoordinated enemies, so the player cannot settle into one tactic.

### Skeleton AI (`EnemyAI.cs`)

- **Perception:** an overlap sphere (range 20) finds the player, then a raycast checks line of sight. A skeleton behind a tree does not see you.
- **Search:** when it loses sight it walks to the last known position, slowing down on arrival, then goes back to roaming.
- **Roaming:** random heading changes with pauses, so idle skeletons do not move in lock step.
- **Hive mind:** a static shared target and a counter of how many members currently see the player. If one skeleton sees you, every hive-mind skeleton chases you. The target is cleared only when the last sighting is lost, so the counter stays correct as enemies die or lose sight.
- **Attack:** damage plus knockback. `PlayerMovement` applies the knockback impulse and briefly locks input so a hit actually pushes you.
- **Feedback:** an "angry" blendshape on the face is driven from `LateUpdate`, so animation clips cannot overwrite it.

### Wishing well (`WishingWell.cs`)

The odds of each sword tier depend on how many gems were thrown in. The chance of the middle tier rises linearly from 19% at 3 gems to 55% at 20 gems. The chance of the top tier rises with the cube of the progress, from 1% to 30%, so it stays rare until the player commits many gems. Weights are normalised and rolled once. The well light brightens with each gem, and the sword is launched out of the well with a safe-spawn check so it never lands inside geometry.

### Weapons (`Sword.cs`, `WeaponManager.cs`)

| Sword | Damage | Durability loss per hit |
|---|---|---|
| Wood | 10 | 3 |
| Stone | 20 | 2 |
| Diamond | 50 | 1 |

Each sword has 100 durability. A skeleton has 100 health. Swings are a short stab animation, and each enemy can only be hit once per swing. When a sword breaks the weapon manager is notified, the sword is destroyed and the player is left empty-handed.

### Player and HUD (`PlayerHealth.cs`, `PlayerMovement.cs`)

- Health bar, weapon durability bar, wave and gem counters, and an arrow that points to the nearest skeleton relative to where you are looking. The arrow uses the headset's heading in VR.
- A low-health pulse, a death sound, and a death panel with survival time and waves survived.
- Movement uses a rigidbody with velocity, a ground check, sprint, jump and decaying knockback. In VR, look direction comes from the headset and the right stick turns the body.

## Running it

1. Install Unity **6000.3.8f1** with the Android build module.
2. Clone the repo and open the folder in Unity Hub. The first import takes a while. The repo includes the baked lighting for the scene, so the clone is about 330 MB.
3. Open `Assets/Scenes/SampleScene.unity` and press Play to try it in the editor with keyboard and mouse.

To run it on a Quest:

1. In *File > Build Profiles*, switch to **Android**.
2. In *Project Settings > XR Plug-in Management*, enable the Oculus or OpenXR loader for Android.
3. Enable developer mode on the headset, build and run, or sideload the APK with `adb install`.
4. Pair a Bluetooth gamepad with the headset to move.

## Third-party assets

These are included so the project opens and runs as built. Each remains under its own licence and belongs to its owner.

- **Low Poly Stylized Nature** by Cody Dreams (`Assets/Cody Dreams`): trees, props, camp, barrel and structures.
- **Stylized Gem Pack** by PolyKebap (`Assets/PolyKebap`): gems.
- **Lowpoly Forest Pack** by Onerat Games (`Assets/Lowpoly Forest Pack`).
- **Mini Simple Characters Skeleton Demo** (`Assets/Mini Simple Characters Skeleton Demo`): the skeleton character and animations.
- **Google Cardboard XR Plugin** samples and **XR Interaction Toolkit** samples (`Assets/Samples`), TextMesh Pro.
- **Audio** (`Assets/Audio`): music, sword swishes, healing and UI sounds, plus meme and game sound effects used for fun. These belong to their respective creators and are included for demonstration only.

## Known limitations

- **No tracked-controller input.** Looking uses the headset and movement needs a gamepad. Hand tracking and Quest controller interaction are not implemented.
- **Simple pathing.** Skeletons steer directly towards their target with a rigidbody and do not path around obstacles. A NavMesh is baked into the scene but the skeleton movement does not use it.
- **Name-based UI lookup.** The HUD finds its elements by object name at startup. Renaming a UI object in the scene breaks the link.
- **No automated tests.** Balance was tuned by playing.

## Author

Daniel Jackson. [GitHub](https://github.com/DanielJacksonRSA)

Code in `Assets/Scripts` is licensed under the [All rights reserved](LICENSE). Third-party assets keep their own licences, as listed above.
