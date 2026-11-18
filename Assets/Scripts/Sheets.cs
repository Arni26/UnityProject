using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Cuts the two sprite sheets (StreamingAssets/Sheets/zombies.jpg, player.jpg) into frames at startup:
/// removes the beige background and finds each figure by connected-component labeling.
/// If anything fails, the game falls back to the procedural placeholder sprites.
///
/// Z[variant 0..4][frame]: 0,1 walk left | 2,3 walk right | 4 attack | 5 hit | 6 falling | 7 dead
/// Pl[row][frame 0..7]:    0 walk (0-3 left, 4-7 right) | 1 pick up | 2 attack | 3 victory | 4 damage (0,1) + killed (2-7)
/// </summary>
public static class Sheets
{
    public const float PPU = 72f;
    public static Sprite[][] Z, Pl;

    class Comp
    {
        public List<int> px = new List<int>();
        public int minx = int.MaxValue, maxx = -1, miny = int.MaxValue, maxy = -1;
        public float cx { get { return (minx + maxx) / 2f; } }
        public float cy { get { return (miny + maxy) / 2f; } }
        public int H { get { return maxy - miny + 1; } }
    }

    public static void Load()
    {
        try { Z = Build("zombies.jpg", true); } catch (System.Exception e) { Debug.LogWarning("Sheets zombies: " + e); Z = null; }
        try { Pl = Build("player.jpg", false); } catch (System.Exception e) { Debug.LogWarning("Sheets player: " + e); Pl = null; }
        Debug.Log("Sheets loaded: zombies=" + (Z != null) + " player=" + (Pl != null));
    }

    static Sprite[][] Build(string file, bool zombie)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Sheets", file);
        if (!File.Exists(path)) return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
        int w = tex.width, h = tex.height; var pix = tex.GetPixels32();

        float br = 0, bg = 0, bb = 0; int n = 0;
        for (int y = h - 8; y < h; y++) for (int x = w - 8; x < w; x++) { var c = pix[y * w + x]; br += c.r; bg += c.g; bb += c.b; n++; }
        br /= n; bg /= n; bb /= n;
        var mask = new bool[w * h];
        for (int i = 0; i < mask.Length; i++)
            mask[i] = Mathf.Abs(pix[i].r - br) + Mathf.Abs(pix[i].g - bg) + Mathf.Abs(pix[i].b - bb) > 50f;

        var comps = Label(mask, w, h, zombie ? 2 : 1);
        var rows = new Sprite[5][];

        if (zombie)
        {
            var big = comps.FindAll(c => c.px.Count >= 700);
            var cl = ClusterRows(big, 5); if (cl == null) return null;
            for (int r = 0; r < 5; r++)
            {
                var row = cl[r];
                if (row.Count < 8) { Debug.LogWarning("Sheets: zombie row " + r + " has " + row.Count + " figures"); return null; }
                row.Sort((a, b) => b.px.Count.CompareTo(a.px.Count)); row = row.GetRange(0, 8);
                row.Sort((a, b) => a.cx.CompareTo(b.cx));
                rows[r] = new Sprite[8];
                for (int i = 0; i < 8; i++) rows[r][i] = MakeSprite(new List<Comp> { row[i] }, pix, w);
            }
            return rows;
        }

        // player sheet: 8 equal columns, 5 rows; drop text labels and sparkles (small, thin components)
        var keep = comps.FindAll(c => !(c.px.Count < 250 && c.H <= 18));
        var bodies = keep.FindAll(c => c.H >= 40);
        var rc = ClusterRows(bodies, 5); if (rc == null) return null;
        var rowCy = new float[5];
        for (int r = 0; r < 5; r++) { float s = 0; foreach (var c in rc[r]) s += c.cy; rowCy[r] = s / rc[r].Count; }
        var cells = new List<Comp>[5, 8];
        foreach (var c in keep)
        {
            int br2 = 0; float bd = 1e9f;
            for (int r = 0; r < 5; r++) { float d = Mathf.Abs(c.cy - rowCy[r]); if (d < bd) { bd = d; br2 = r; } }
            int col = Mathf.Clamp((int)(c.cx / (w / 8f)), 0, 7);
            if (cells[br2, col] == null) cells[br2, col] = new List<Comp>();
            cells[br2, col].Add(c);
        }
        for (int r = 0; r < 5; r++)
        {
            rows[r] = new Sprite[8];
            for (int i = 0; i < 8; i++) if (cells[r, i] != null) rows[r][i] = MakeSprite(cells[r, i], pix, w);
            for (int i = 0; i < 8; i++) if (rows[r][i] == null)
                {
                    for (int j = 1; j < 8 && rows[r][i] == null; j++)
                    {
                        if (i - j >= 0 && rows[r][i - j] != null) rows[r][i] = rows[r][i - j];
                        else if (i + j < 8 && rows[r][i + j] != null) rows[r][i] = rows[r][i + j];
                    }
                    if (rows[r][i] == null) return null;
                }
        }
        return rows;
    }

    static bool[] Dilate(bool[] m, int w, int h, int r)
    {
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
                    if (x < c.minx) c.minx = x; if (x > c.maxx) c.maxx = x; if (y < c.miny) c.miny = y; if (y > c.maxy) c.maxy = y;
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

    /// <summary>Split components into k rows (top to bottom) at the k-1 largest vertical gaps.</summary>
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

    static Sprite MakeSprite(List<Comp> cs, Color32[] pix, int w)
    {
        int minx = int.MaxValue, maxx = -1, miny = int.MaxValue, maxy = -1;
        foreach (var c in cs) { minx = Mathf.Min(minx, c.minx); maxx = Mathf.Max(maxx, c.maxx); miny = Mathf.Min(miny, c.miny); maxy = Mathf.Max(maxy, c.maxy); }
        int bw = maxx - minx + 1, bh = maxy - miny + 1;
        var arr = new Color32[bw * bh];
        foreach (var c in cs) foreach (int p in c.px)
            {
                var col = pix[p]; col.a = 255;
                arr[(p / w - miny) * bw + (p % w - minx)] = col;
            }
        var t = new Texture2D(bw, bh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        t.SetPixels32(arr); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, bw, bh), new Vector2(0.5f, 0f), PPU);
    }
}
