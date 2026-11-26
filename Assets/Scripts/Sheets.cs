using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AnimSet
{
    public Sprite[] idle, walk, run, pickup, attack, shoot, victory, hurt, fall, dead;
    public float scale = 1f;
}

/// <summary>
/// 3-tier sprite loader:
/// 1) Craftpix Raider / Urban Zombie horizontal strips under StreamingAssets/Packs/.
/// 2) Bundled sheets (StreamingAssets/Sheets/player.jpg, zombies.jpg) with exterior BFS flood-fill alpha removal.
/// 3) Pre-sliced PNGs under Resources/Sprites/ (if StreamingAssets sheets are unavailable).
/// </summary>
public static class Sheets
{
    public const float SheetPPU = 68f;
    public const float CraftpixPPU = 64f;

    public static AnimSet[] PlSets;
    public static AnimSet[] ZSets;
    public static bool UsingCraftpixRaider, UsingCraftpixZombie;

    class Comp
    {
        public readonly List<int> px = new List<int>();
        public int minx = int.MaxValue, maxx = -1, miny = int.MaxValue, maxy = -1;
        public float cx => (minx + maxx) * 0.5f;
        public float cy => (miny + maxy) * 0.5f;
        public int W => maxx - minx + 1;
        public int H => maxy - miny + 1;
    }

    /// <summary>Return the player AnimSet matching the current weapon (supports Craftpix Raider_1..3).</summary>
    public static AnimSet PlayerForWeapon(int w)
    {
        if (PlSets == null || PlSets.Length == 0) return null;
        if (w == 2 && PlSets.Length > 1) return PlSets[1];
        if (w == 3 && PlSets.Length > 2) return PlSets[2];
        return PlSets[0];
    }

    public static void Load()
    {
        UsingCraftpixRaider = UsingCraftpixZombie = false;
        PlSets = LoadCraftpixGroup("Packs/Raider");
        if (PlSets != null && PlSets.Length > 0) UsingCraftpixRaider = true;
        else PlSets = BuildPlayerFromSheet() ?? LoadPlayerFromResources();

        ZSets = LoadCraftpixGroup("Packs/UrbanZombie");
        if (ZSets != null && ZSets.Length > 0) UsingCraftpixZombie = true;
        else ZSets = BuildZombiesFromSheet() ?? LoadZombiesFromResources();

        Debug.Log($"Sheets loaded: player={(PlSets != null ? PlSets.Length : 0)} (craftpix={UsingCraftpixRaider}), zombies={(ZSets != null ? ZSets.Length : 0)} (craftpix={UsingCraftpixZombie})");
    }

    /// <summary>Export all 40 player frames and 40 zombie frames as clean transparent PNGs under Resources/Sprites.</summary>
    public static int ExportCleanPngs(string resourcesSpritesRoot)
    {
        int total = 0;
        var pFrames = SlicePlayerSheetPixels();
        if (pFrames != null)
        {
            string dir = Path.Combine(resourcesSpritesRoot, "Player");
            Directory.CreateDirectory(dir);
            string[] rowNames = { "walk", "pick", "atk", "win", "dmg" };
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 8; c++)
                    if (SaveFramePng(pFrames[r, c], Path.Combine(dir, $"{rowNames[r]}_{c}.png"))) total++;
        }

        var zFrames = SliceZombieSheetPixels();
        if (zFrames != null)
        {
            string dir = Path.Combine(resourcesSpritesRoot, "Zombie");
            Directory.CreateDirectory(dir);
            string[] colNames = { "walkL0", "walkL1", "walkR0", "walkR1", "attack", "hit", "fall", "dead" };
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 8; c++)
                    if (SaveFramePng(zFrames[r, c], Path.Combine(dir, $"z{r + 1}_{colNames[c]}.png"))) total++;
        }
        return total;
    }

    // ==================== Tier 1: Craftpix horizontal strip loader ====================
    static AnimSet[] LoadCraftpixGroup(string relFolder)
    {
        string root = Path.Combine(Application.streamingAssetsPath, relFolder);
        if (!Directory.Exists(root)) return null;

        var dirs = new List<string>();
        if (Directory.GetFiles(root, "*.png").Length > 0) dirs.Add(root);
        var subs = Directory.GetDirectories(root);
        Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
        foreach (var sub in subs)
        {
            if (Directory.GetFiles(sub, "*.png").Length > 0) dirs.Add(sub);
            else
            {
                var nested = Directory.GetDirectories(sub);
                Array.Sort(nested, StringComparer.OrdinalIgnoreCase);
                foreach (var n in nested)
                    if (Directory.GetFiles(n, "*.png").Length > 0) dirs.Add(n);
            }
        }
        if (dirs.Count == 0) return null;

        var sets = new List<AnimSet>();
        foreach (var d in dirs)
        {
            var set = LoadCraftpixChar(d);
            if (set != null) sets.Add(set);
        }
        return sets.Count > 0 ? sets.ToArray() : null;
    }

    static AnimSet LoadCraftpixChar(string dir)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.GetFiles(dir, "*.png"))
            files[Path.GetFileNameWithoutExtension(f)] = f;

        Sprite[] Pick(params string[] names)
        {
            foreach (var n in names)
                if (files.TryGetValue(n, out var path))
                {
                    var s = SliceStrip(path);
                    if (s != null && s.Length > 0) return s;
                }
            return null;
        }

        var set = new AnimSet
        {
            idle = Pick("Idle", "Idle_1"),
            walk = Pick("Walk", "Walk_1"),
            run = Pick("Run", "Run_1"),
            attack = Pick("Attack_1", "Attack", "Attack_2"),
            shoot = Pick("Shot_1", "Shot", "Shot_2"),
            hurt = Pick("Hurt", "Damage"),
            dead = Pick("Dead", "Death"),
            pickup = Pick("Recharge", "Pick"),
            victory = Pick("Jump", "Victory")
        };

        if (set.idle == null && set.walk == null && set.run == null) return null;
        if (set.walk == null) set.walk = set.run ?? set.idle;
        if (set.idle == null) set.idle = new[] { set.walk[0] };
        if (set.run == null) set.run = set.walk;
        if (set.pickup == null) set.pickup = set.idle;
        if (set.attack == null) set.attack = set.shoot ?? set.idle;
        if (set.shoot == null) set.shoot = set.attack ?? set.idle;
        if (set.victory == null) set.victory = set.idle;
        if (set.hurt == null) set.hurt = set.idle;
        if (set.dead == null) set.dead = new[] { set.hurt[set.hurt.Length - 1] };
        set.fall = new[] { set.dead[0] };
        set.scale = 1f;
        return set;
    }

    static Sprite[] SliceStrip(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes)) return null;
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        int tw = tex.width, fh = tex.height;
        int count = Mathf.Max(1, tw / Mathf.Max(1, fh));
        int fw = tw / count;

        var pix = tex.GetPixels32();
        int minY = fh;
        for (int y = 0; y < fh && minY == fh; y++)
            for (int x = 0; x < tw; x++)
                if (pix[y * tw + x].a > 20) { minY = y; break; }
        float py = minY < fh ? (float)minY / fh : 0f;

        var res = new Sprite[count];
        for (int i = 0; i < count; i++)
            res[i] = Sprite.Create(tex, new Rect(i * fw, 0, fw, fh), new Vector2(0.5f, py), CraftpixPPU, 0, SpriteMeshType.FullRect);
        return res;
    }

    // ==================== Tier 2: Bundled sprite sheets with BFS alpha ====================
    struct FramePixels { public int w, h; public Color32[] pix; }

    static AnimSet[] BuildPlayerFromSheet()
    {
        var g = SlicePlayerSheetPixels();
        if (g == null) return null;
        var sp = new Sprite[5, 8];
        for (int r = 0; r < 5; r++)
            for (int c = 0; c < 8; c++)
                sp[r, c] = PixelsToSprite(g[r, c]);

        var set = new AnimSet
        {
            idle = new[] { sp[0, 0] },
            walk = RowRange(sp, 0, 0, 7),
            run = RowRange(sp, 0, 0, 7),
            pickup = RowRange(sp, 1, 0, 7),
            attack = RowRange(sp, 2, 0, 7),
            shoot = new[] { sp[2, 0], sp[2, 1], sp[2, 7] },
            victory = RowRange(sp, 3, 0, 7),
            hurt = new[] { sp[4, 0], sp[4, 1] },
            fall = RowRange(sp, 4, 2, 5),
            dead = new[] { sp[4, 6], sp[4, 7] },
            scale = 1f
        };
        return new[] { set };
    }

    static AnimSet[] BuildZombiesFromSheet()
    {
        var g = SliceZombieSheetPixels();
        if (g == null) return null;
        var sets = new AnimSet[5];
        for (int r = 0; r < 5; r++)
        {
            var sp = new Sprite[8];
            for (int c = 0; c < 8; c++) sp[c] = PixelsToSprite(g[r, c]);
            sets[r] = new AnimSet
            {
                idle = new[] { sp[0] },
                walk = new[] { sp[0], sp[1] },
                run = new[] { sp[2], sp[3] },
                attack = new[] { sp[4] },
                shoot = new[] { sp[4] },
                hurt = new[] { sp[5] },
                fall = new[] { sp[6] },
                dead = new[] { sp[7] },
                pickup = new[] { sp[0] },
                victory = new[] { sp[0] },
                scale = 1f
            };
        }
        return sets;
    }

    static Sprite[] RowRange(Sprite[,] g, int row, int from, int to)
    {
        var a = new Sprite[to - from + 1];
        for (int i = 0; i < a.Length; i++) a[i] = g[row, from + i];
        return a;
    }

    static FramePixels[,] SlicePlayerSheetPixels()
    {
        if (!LoadSheet("player.jpg", out var w, out var h, out var pix, out var fg)) return null;

        // Pass 1 (zero dilation): locate character rows and erase isolated header text above heads
        var rawComps = Label(fg, w, h, 0);
        var rawBodies = rawComps.FindAll(c => c.H >= 40 && c.px.Count >= 650);
        var rc = ClusterRows(rawBodies, 5);
        if (rc == null) return null;

        var rowCy = new float[5];
        for (int r = 0; r < 5; r++)
        {
            float s = 0f; foreach (var c in rc[r]) s += c.cy; rowCy[r] = s / rc[r].Count;
        }
        foreach (var c in rawComps)
        {
            if (c.H <= 22 && c.px.Count < 450 && c.cx < 245f)
            { foreach (int p in c.px) fg[p] = false; continue; }
            int bestR = 0; float bestD = float.MaxValue;
            for (int r = 0; r < 5; r++) { float d = Mathf.Abs(c.cy - rowCy[r]); if (d < bestD) { bestD = d; bestR = r; } }
            if (c.H <= 22 && c.px.Count < 450 && c.cy > rowCy[bestR] + 24f)
                foreach (int p in c.px) fg[p] = false;
        }
        // Trim any tiny remaining header stroke at the top of each row col 0 ("/ V" touching hair tip in row 3)
        for (int y = h - 1; y >= 0; y--)
        {
            int strip = Mathf.FloorToInt((h - 1 - y) / (h / 5f));
            float stripTop = h - strip * (h / 5f);
            float band = strip == 3 ? 21f : 14f;
            if (stripTop - y <= band)
                for (int x = 0; x < 230 && x < w; x++) fg[y * w + x] = false;
        }

        // Pass 2 (dilate = 1): re-label cleaned foreground and pack into 5x8 cells
        var comps = Label(fg, w, h, 1);
        var keep = comps.FindAll(c => !(c.px.Count < 450 && c.H <= 20));
        var cells = new List<Comp>[5, 8];
        foreach (var c in keep)
        {
            int bestR = 0; float bestD = float.MaxValue;
            for (int r = 0; r < 5; r++)
            {
                float d = Mathf.Abs(c.cy - rowCy[r]);
                if (d < bestD) { bestD = d; bestR = r; }
            }
            if (c.H <= 22 && c.miny > rowCy[bestR] + 22f) continue;
            int col = Mathf.Clamp((int)(c.cx / (w / 8f)), 0, 7);
            if (cells[bestR, col] == null) cells[bestR, col] = new List<Comp>();
            cells[bestR, col].Add(c);
        }

        var res = new FramePixels[5, 8];
        for (int r = 0; r < 5; r++)
        {
            for (int c = 0; c < 8; c++)
                if (cells[r, c] != null && cells[r, c].Count > 0)
                    res[r, c] = PackComps(cells[r, c], pix, w);
            for (int c = 0; c < 8; c++)
                if (res[r, c].pix == null)
                    res[r, c] = NearestCol(res, r, c);
        }
        return res;
    }

    static FramePixels[,] SliceZombieSheetPixels()
    {
        if (!LoadSheet("zombies.jpg", out var w, out var h, out var pix, out var fg)) return null;
        var comps = Label(fg, w, h, 2);

        // Drop top text headers and far-left "1.".."5." row indices.
        var keep = comps.FindAll(c =>
            c.maxy < h - 42 &&
            !(c.cx < 46f && c.px.Count < 600) &&
            !(c.H <= 20 && c.px.Count < 450));

        var bodies = keep.FindAll(c => c.px.Count >= 550);
        var rc = ClusterRows(bodies, 5);
        if (rc == null) return null;

        var rowCy = new float[5];
        for (int r = 0; r < 5; r++)
        {
            float s = 0f; foreach (var c in rc[r]) s += c.cy; rowCy[r] = s / rc[r].Count;
        }

        // Assign every kept component in the character bands to its nearest row, then group by X (<48px).
        var rowBins = new List<Comp>[5];
        for (int r = 0; r < 5; r++) rowBins[r] = new List<Comp>();
        foreach (var c in keep)
        {
            int bestR = 0; float bestD = float.MaxValue;
            for (int r = 0; r < 5; r++)
            {
                float d = Mathf.Abs(c.cy - rowCy[r]);
                if (d < bestD) { bestD = d; bestR = r; }
            }
            if (bestD > 62f) continue;
            rowBins[bestR].Add(c);
        }

        var res = new FramePixels[5, 8];
        for (int r = 0; r < 5; r++)
        {
            var bin = rowBins[r];
            bin.Sort((a, b) => a.cx.CompareTo(b.cx));
            var clusters = new List<List<Comp>>();
            foreach (var c in bin)
            {
                if (clusters.Count == 0) { clusters.Add(new List<Comp> { c }); continue; }
                var prev = clusters[clusters.Count - 1];
                float prevCx = MeanCx(prev);
                if (Mathf.Abs(c.cx - prevCx) < 48f) prev.Add(c);
                else clusters.Add(new List<Comp> { c });
            }

            if (clusters.Count > 8)
            {
                clusters.Sort((a, b) => SumPx(b).CompareTo(SumPx(a)));
                clusters = clusters.GetRange(0, 8);
                clusters.Sort((a, b) => MeanCx(a).CompareTo(MeanCx(b)));
            }

            for (int c = 0; c < clusters.Count && c < 8; c++)
                res[r, c] = PackComps(clusters[c], pix, w);
            for (int c = 0; c < 8; c++)
                if (res[r, c].pix == null)
                    res[r, c] = NearestCol(res, r, c);
        }
        return res;
    }

    static float MeanCx(List<Comp> list) { float s = 0f; foreach (var c in list) s += c.cx; return s / list.Count; }
    static int SumPx(List<Comp> list) { int s = 0; foreach (var c in list) s += c.px.Count; return s; }

    static FramePixels NearestCol(FramePixels[,] res, int r, int c)
    {
        for (int d = 1; d < 8; d++)
        {
            if (c - d >= 0 && res[r, c - d].pix != null) return res[r, c - d];
            if (c + d < 8 && res[r, c + d].pix != null) return res[r, c + d];
        }
        return default;
    }

    static float lastBgR, lastBgG, lastBgB;

    static bool LoadSheet(string file, out int w, out int h, out Color32[] pix, out bool[] fg)
    {
        w = h = 0; pix = null; fg = null;
        string path = Path.Combine(Application.streamingAssetsPath, "Sheets", file);
        if (!File.Exists(path)) return false;

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(path))) return false;
        w = tex.width; h = tex.height;
        var px = tex.GetPixels32();
        pix = px;

        // Sample 4 corners for the beige backdrop color
        float br = 0, bg = 0, bb = 0; int n = 0;
        int[] xs = { 0, w - 6 }, ys = { 0, h - 6 };
        foreach (int y0 in ys) foreach (int x0 in xs)
                for (int dy = 0; dy < 6; dy++) for (int dx = 0; dx < 6; dx++)
                    {
                        var c = px[(y0 + dy) * w + (x0 + dx)];
                        br += c.r; bg += c.g; bb += c.b; n++;
                    }
        br /= n; bg /= n; bb /= n;
        lastBgR = br; lastBgG = bg; lastBgB = bb;

        var isBg = new bool[w * h];
        var q = new Queue<int>();
        void TrySeed(int idx)
        {
            if (isBg[idx]) return;
            var c = px[idx];
            if (Mathf.Abs(c.r - br) + Mathf.Abs(c.g - bg) + Mathf.Abs(c.b - bb) <= 68f)
            { isBg[idx] = true; q.Enqueue(idx); }
        }
        for (int x = 0; x < w; x++) { TrySeed(x); TrySeed((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { TrySeed(y * w); TrySeed(y * w + (w - 1)); }

        // 4-neighbor BFS: remove exterior background while preserving light pixels inside dark outlines
        while (q.Count > 0)
        {
            int p = q.Dequeue(); int x = p % w, y = p / w;
            if (x > 0) TrySeed(p - 1);
            if (x < w - 1) TrySeed(p + 1);
            if (y > 0) TrySeed(p - w);
            if (y < h - 1) TrySeed(p + w);
        }

        // 1-pixel exterior edge despill on bright anti-aliased fringe
        var despill = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int p = y * w + x;
                if (isBg[p]) continue;
                bool touchesBg = (x > 0 && isBg[p - 1]) || (x < w - 1 && isBg[p + 1]) || (y > 0 && isBg[p - w]) || (y < h - 1 && isBg[p + w]);
                if (!touchesBg) continue;
                var c = pix[p];
                float dist = Mathf.Abs(c.r - br) + Mathf.Abs(c.g - bg) + Mathf.Abs(c.b - bb);
                float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                if (dist <= 90f && lum > 115f) despill[p] = true;
            }

        fg = new bool[w * h];
        for (int i = 0; i < fg.Length; i++) fg[i] = !isBg[i] && !despill[i];
        return true;
    }

    static bool[] Dilate(bool[] m, int w, int h, int r)
    {
        if (r <= 0) return m;
        var a = new bool[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                if (m[y * w + x]) for (int dx = -r; dx <= r; dx++) { int nx = x + dx; if (nx >= 0 && nx < w) a[y * w + nx] = true; }
        var b = new bool[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                if (a[y * w + x]) for (int dy = -r; dy <= r; dy++) { int ny = y + dy; if (ny >= 0 && ny < h) b[ny * w + x] = true; }
        return b;
    }

    static List<Comp> Label(bool[] mask, int w, int h, int dilate)
    {
        var dil = Dilate(mask, w, h, dilate);
        var lab = new int[w * h]; var res = new List<Comp>(); var st = new Stack<int>();
        for (int i = 0; i < lab.Length; i++)
        {
            if (!dil[i] || lab[i] != 0) continue;
            var c = new Comp(); int id = res.Count + 1; lab[i] = id; st.Push(i);
            while (st.Count > 0)
            {
                int p = st.Pop(); int x = p % w, y = p / w;
                if (mask[p])
                {
                    c.px.Add(p);
                    if (x < c.minx) c.minx = x; if (x > c.maxx) c.maxx = x;
                    if (y < c.miny) c.miny = y; if (y > c.maxy) c.maxy = y;
                }
                if (x > 0 && dil[p - 1] && lab[p - 1] == 0) { lab[p - 1] = id; st.Push(p - 1); }
                if (x < w - 1 && dil[p + 1] && lab[p + 1] == 0) { lab[p + 1] = id; st.Push(p + 1); }
                if (y > 0 && dil[p - w] && lab[p - w] == 0) { lab[p - w] = id; st.Push(p - w); }
                if (y < h - 1 && dil[p + w] && lab[p + w] == 0) { lab[p + w] = id; st.Push(p + w); }
            }
            if (c.px.Count > 0) res.Add(c);
        }
        return res;
    }

    static List<List<Comp>> ClusterRows(List<Comp> list, int k)
    {
        if (list.Count < k) return null;
        list.Sort((a, b) => b.cy.CompareTo(a.cy));
        var gaps = new List<int>(); for (int i = 0; i < list.Count - 1; i++) gaps.Add(i);
        gaps.Sort((a, b) => (list[b].cy - list[b + 1].cy).CompareTo(list[a].cy - list[a + 1].cy));
        var cuts = gaps.GetRange(0, k - 1); cuts.Sort();
        var res = new List<List<Comp>>(); int start = 0;
        foreach (var c in cuts) { res.Add(list.GetRange(start, c - start + 1)); start = c + 1; }
        res.Add(list.GetRange(start, list.Count - start));
        return res;
    }

    static FramePixels PackComps(List<Comp> cs, Color32[] pix, int w)
    {
        int minx = int.MaxValue, maxx = -1, miny = int.MaxValue, maxy = -1;
        foreach (var c in cs)
        {
            minx = Mathf.Min(minx, c.minx); maxx = Mathf.Max(maxx, c.maxx);
            miny = Mathf.Min(miny, c.miny); maxy = Mathf.Max(maxy, c.maxy);
        }
        int bw = maxx - minx + 1, bh = maxy - miny + 1;
        var arr = new Color32[bw * bh];
        foreach (var c in cs) foreach (int p in c.px)
            {
                var col = pix[p]; col.a = 255;
                arr[(p / w - miny) * bw + (p % w - minx)] = col;
            }
        // Clear enclosed beige backdrop pocket trapped between legs and ground shadow
        if (bh >= 45)
        {
            int maxY = Mathf.FloorToInt(bh * 0.42f);
            for (int y = 0; y < maxY; y++)
                for (int x = 0; x < bw; x++)
                {
                    int idx = y * bw + x;
                    var col = arr[idx];
                    if (col.a == 0) continue;
                    float dist = Mathf.Abs(col.r - lastBgR) + Mathf.Abs(col.g - lastBgG) + Mathf.Abs(col.b - lastBgB);
                    float lum = col.r * 0.299f + col.g * 0.587f + col.b * 0.114f;
                    if (dist <= 56f && lum > 192f) arr[idx].a = 0;
                }
        }
        return new FramePixels { w = bw, h = bh, pix = arr };
    }

    static Sprite PixelsToSprite(FramePixels fp)
    {
        if (fp.pix == null || fp.w <= 0 || fp.h <= 0) return null;
        var t = new Texture2D(fp.w, fp.h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        t.SetPixels32(fp.pix); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, fp.w, fp.h), new Vector2(0.5f, 0f), SheetPPU, 0, SpriteMeshType.FullRect);
    }

    static bool SaveFramePng(FramePixels fp, string path)
    {
        if (fp.pix == null || fp.w <= 0 || fp.h <= 0) return false;
        var t = new Texture2D(fp.w, fp.h, TextureFormat.RGBA32, false);
        t.SetPixels32(fp.pix); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        return true;
    }

    // ==================== Tier 3: Resources/Sprites PNG fallback ====================
    static AnimSet[] LoadPlayerFromResources()
    {
        Sprite[] LoadRow(string prefix)
        {
            var l = new List<Sprite>();
            for (int i = 0; i < 8; i++)
            {
                var s = Resources.Load<Sprite>($"Sprites/Player/{prefix}_{i}");
                if (s != null) l.Add(s);
            }
            return l.Count > 0 ? l.ToArray() : null;
        }
        var walk = LoadRow("walk");
        if (walk == null) return null;
        var pick = LoadRow("pick") ?? new[] { walk[0] };
        var atk = LoadRow("atk") ?? walk;
        var win = LoadRow("win") ?? walk;
        var dmg = LoadRow("dmg") ?? new[] { walk[0] };
        return new[] {
            new AnimSet {
                idle = new[] { walk[0] }, walk = walk, run = walk, pickup = pick, attack = atk,
                shoot = new[] { atk[0], atk[Mathf.Min(1, atk.Length - 1)], atk[atk.Length - 1] },
                victory = win,
                hurt = new[] { dmg[0], dmg[Mathf.Min(1, dmg.Length - 1)] },
                fall = dmg.Length > 4 ? new[] { dmg[2], dmg[3], dmg[4] } : dmg,
                dead = new[] { dmg[dmg.Length - 1] },
                scale = 1f
            }
        };
    }

    static AnimSet[] LoadZombiesFromResources()
    {
        var list = new List<AnimSet>();
        for (int z = 1; z <= 5; z++)
        {
            Sprite G(string n) => Resources.Load<Sprite>($"Sprites/Zombie/z{z}_{n}");
            var wl0 = G("walkL0"); if (wl0 == null) continue;
            var wl1 = G("walkL1") ?? wl0;
            var wr0 = G("walkR0") ?? wl0; var wr1 = G("walkR1") ?? wr0;
            var atk = G("attack") ?? wr0; var hit = G("hit") ?? wl0;
            var fall = G("fall") ?? hit; var dead = G("dead") ?? fall;
            list.Add(new AnimSet
            {
                idle = new[] { wl0 }, walk = new[] { wl0, wl1 }, run = new[] { wr0, wr1 },
                attack = new[] { atk }, shoot = new[] { atk }, hurt = new[] { hit },
                fall = new[] { fall }, dead = new[] { dead }, pickup = new[] { wl0 }, victory = new[] { wl0 },
                scale = 1f
            });
        }
        return list.Count > 0 ? list.ToArray() : null;
    }
}
