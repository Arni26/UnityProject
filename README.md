# KBTU Outbreak (Unity 6, 2D pixel horror)

Open this folder in Unity Hub with Unity 6000.3.x. The Main scene is created automatically (menu `KBTU`). Press Play.

Everything (maps, sprites, zombies, puzzles, UI) is generated from code, so the project needs no art.
If the project opens with "Active Input Handling = Input System only", set it to "Both" (Project Settings > Player).

## Story flow
Intro > Bunker (-1) > Floor 0 hub (KB, AB, PF, TB blocks) > 7 puzzle rooms > Security (key) > Tunnel (win).
If you die from a bite, you respawn at the previous stage (the last completed door, or the bunker).

## Controls
WASD move, mouse aim, LMB attack, E interact, 1-4 / wheel weapons, G grenade, H medkit, Shift dash, R reset room.

## Stages
| # | Room | Puzzle | Reward |
|---|------|--------|--------|
| 1 | 157 AB | lever order from console | Pistol |
| 2 | 117 PF | 3 notes > code lock | Healing skill + medkits |
| 3 | 726 Abai press center | push crates onto plates | Shotgun |
| 4 | 400B Kunaev | 3 fuses > fuse box | Dash |
| 5 | 346 TB | memorize 5 colors | SMG |
| 6 | 270 AB | 4 notes > code lock (many zombies) | Armor |
| 7 | 428 KB | fuses + lever order | Grenades, opens Security |

Extra "dangerous" rooms (305, 162, 25) are full of zombies, with ammo and medkits.

## Where to edit
- `Areas.cs`: maps, room names, puzzle settings
- `Game.cs`: rules, rewards, story text, UI
- `Sprites.cs`: placeholder sprites
- `Actors.cs`: player, zombies, weapons
