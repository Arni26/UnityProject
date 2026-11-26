Craftpix Raider Sprite Sheets Drop-In Folder
==========================================
https://craftpix.net/freebies/free-raider-sprite-sheets-pixel-art/

Craftpix freebies require signing in on craftpix.net to download the archive.
If you download and unzip "Free Raider Sprite Sheets Pixel Art", place the character subfolders here:

  Assets/StreamingAssets/Packs/Raider/Raider_1/Idle.png, Walk.png, Run.png, Attack_1.png, Shot_1.png, Hurt.png, Dead.png
  Assets/StreamingAssets/Packs/Raider/Raider_2/...
  Assets/StreamingAssets/Packs/Raider/Raider_3/...

On Play, Sheets.cs automatically detects horizontal strip PNGs in these subfolders, slices them at frame height (128px), and assigns Raider_1/2/3 animations to the player weapons (baton, shotgun, rifle).
If this folder has no PNGs, the game automatically uses the bundled Assets/StreamingAssets/Sheets/player.jpg sprite sheet.
