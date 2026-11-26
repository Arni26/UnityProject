Craftpix Urban Zombie Sprite Sheet Pack Drop-In Folder
=======================================================
https://craftpix.net/freebies/free-urban-zombie-sprite-sheet-pixel-art-pack/

Craftpix freebies require signing in on craftpix.net to download the archive.
If you download and unzip "Free Urban Zombie Sprite Sheet Pixel Art Pack", place the zombie subfolders here:

  Assets/StreamingAssets/Packs/UrbanZombie/Zombie_1/Idle.png, Walk.png, Run.png, Attack_1.png, Hurt.png, Dead.png
  Assets/StreamingAssets/Packs/UrbanZombie/Zombie_2/...
  Assets/StreamingAssets/Packs/UrbanZombie/Zombie_3/...

On Play, Sheets.cs automatically detects horizontal strip PNGs in each subfolder, slices them at frame height (128px), and assigns them to zombie variants across KBTU.
If this folder has no PNGs, the game automatically uses the bundled Assets/StreamingAssets/Sheets/zombies.jpg sprite sheet.
