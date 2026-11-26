using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural 16x16 pixel art: abandoned beige Soviet-style university corridors, offices, bunker, props.
/// 1 tile = 1 world unit (PPU 16). Props have a bottom-centre pivot so they y-sort with characters.
/// </summary>
public static class Sprites
{
    public const int T = 16;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    static Color Sh(Color c, float k) { return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a); }
    static Color A(Color c, float a) { c.a = a; return c; }

    public static float N(int x, int y, int s)
    {
        unchecked
        {
            int h = x * 73856093 ^ y * 19349663 ^ s * 83492791;
            h = (h >> 13) ^ h; h = h * (h * h * 15731 + 789221) + 1376312589;
            return (h & 0x7fffffff) / 2147483647f;
        }
    }

    // ---------- tiny canvas, y = 0 is the TOP row ----------
    class Cv
    {
        public int w, h; public Color[] p;
        public Cv(int w, int h) { this.w = w; this.h = h; p = new Color[w * h]; }
        public void S(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = (h - 1 - y) * w + x; var o = p[i];
            if (c.a >= 0.999f || o.a <= 0.001f) p[i] = c;
            else p[i] = new Color(Mathf.Lerp(o.r, c.r, c.a), Mathf.Lerp(o.g, c.g, c.a), Mathf.Lerp(o.b, c.b, c.a), Mathf.Max(o.a, c.a));
        }
        public Color G(int x, int y) { return (x < 0 || y < 0 || x >= w || y >= h) ? Color.clear : p[(h - 1 - y) * w + x]; }
        public void R(int x, int y, int ww, int hh, Color c) { for (int j = 0; j < hh; j++) for (int i = 0; i < ww; i++) S(x + i, y + j, c); }
        public void Box(int x, int y, int ww, int hh, Color fill, Color edge) { R(x, y, ww, hh, edge); R(x + 1, y + 1, ww - 2, hh - 2, fill); }
        public void L(int x0, int y0, int x1, int y1, Color c)
        {
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, e = dx + dy;
            for (int k = 0; k < 64; k++) { S(x0, y0, c); if (x0 == x1 && y0 == y1) break; int e2 = 2 * e; if (e2 >= dy) { e += dy; x0 += sx; } if (e2 <= dx) { e += dx; y0 += sy; } }
        }
        public void Disc(float cx, float cy, float r, Color c) { for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { float dx = x + .5f - cx, dy = y + .5f - cy; if (dx * dx + dy * dy <= r * r) S(x, y, c); } }
        public void Noise(int seed, float amt) { for (int i = 0; i < p.Length; i++) if (p[i].a > 0) { float n = (N(i % w, i / w, seed) - .5f) * amt; p[i] = new Color(Mathf.Clamp01(p[i].r + n), Mathf.Clamp01(p[i].g + n), Mathf.Clamp01(p[i].b + n), p[i].a); } }
        public void Outline(Color c)
        {
            var q = (Color[])p.Clone();
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    if (q[(h - 1 - y) * w + x].a > 0) continue;
                    bool n = false;
                    int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                    for (int k = 0; k < 4; k++) { int nx = x + dx[k], ny = y + dy[k]; if (nx >= 0 && ny >= 0 && nx < w && ny < h && q[(h - 1 - ny) * w + nx].a > 0.5f) n = true; }
                    if (n) p[(h - 1 - y) * w + x] = c;
                }
        }
        public Sprite Make(float px = .5f, float py = .5f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels(p); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(px, py), T, 0, SpriteMeshType.FullRect);
        }
    }

    public static Sprite Get(string k)
    {
        if (cache.TryGetValue(k, out var s)) return s;
        try { s = Make(k); } catch (Exception e) { Debug.LogWarning("Sprite " + k + ": " + e.Message); s = null; }
        if (s == null) { var c = new Cv(T, T); c.R(0, 0, T, T, Color.magenta); s = c.Make(); }
        cache[k] = s; return s;
    }

    // palettes
    static readonly Color WallUp = H("#cbb68c"), WallLow = H("#8d7a58"), Stripe = H("#6a5a40"), Base = H("#4a3d2c"), Peel = H("#e3d8bb"), Stain = H("#9a8660");
    static readonly Color Conc = H("#6b6e70"), Rust = H("#7a4a2a"), Blood = H("#6a1010"), Blood2 = H("#8c1a17"), Wood = H("#7a5636"), WoodD = H("#523822"), Metal = H("#5c6a62"), Ink = H("#16120f");

    static Sprite Make(string k)
    {
        string[] a = k.Split('_');
        int v = a.Length > 2 ? int.Parse(a[2]) : 0;
        switch (a[0])
        {
            case "floor": return Floor(a[1], v);
            case "wall": return Wall(a[1], v);
            case "top": return Top();
            case "decal": return Decal(a[1], v);
        }
        switch (k)
        {
            case "desk": return Desk(false);
            case "terminal": return Desk(true);
            case "chair": return Chair(false);
            case "chairF": return Chair(true);
            case "locker": return Locker(false);
            case "lockerL": return Locker(true);
            case "shelf": return Shelf();
            case "books": return Books();
            case "sofa": return Sofa();
            case "safe": return Safe();
            case "reader": return Reader();
            case "card": return Card();
            case "key": return Key(H("#ffd23f"));
            case "lkey": return Key(H("#c0c4c8"));
            case "baton": return Baton();
            case "note": return Note();
            case "board": return Board();
            case "fuse": return Fuse();
            case "fusebox": return FuseBox();
            case "crate": return Crate();
            case "plate": return Plate();
            case "lever": return Lever();
            case "console": return Console();
            case "door": return Door(false);
            case "doorL": return Door(true);
            case "gate": return Gate();
            case "plant": return Plant();
            case "table": return Table();
            case "medkit": return Medkit();
            case "ammo": return Ammo();
            case "dot": { var c = new Cv(4, 4); c.R(1, 1, 2, 2, H("#fff2a0")); c.S(0, 1, A(H("#ffcc40"), .6f)); c.S(3, 2, A(H("#ffcc40"), .6f)); var t = c.Make(); return t; }
            case "blob": { var c = new Cv(16, 16); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { float dx = x - 7.5f, dy = y - 7.5f, d = Mathf.Sqrt(dx * dx + dy * dy) / 8f; if (d < 1) c.S(x, y, new Color(1, 1, 1, 1 - d * d)); } return c.Make(); }
            case "shadow": { var c = new Cv(16, 8); for (int y = 0; y < 8; y++) for (int x = 0; x < 16; x++) { float dx = (x - 7.5f) / 8f, dy = (y - 3.5f) / 4f, d = dx * dx + dy * dy; if (d < 1) c.S(x, y, new Color(0, 0, 0, .45f * (1 - d))); } return c.Make(.5f, .5f); }
            case "cone": return Cone();
            case "slash": return Slash();
            case "glow": return Glow();
            case "wshadow": return WallShadow();
            case "player": return Dude(H("#2a2f45"), H("#e0b48c"), H("#5a3a22"));
            case "zombie": return Dude(H("#5a2a2a"), H("#7fa06a"), H("#3a4a2a"));
        }
        return null;
    }

    // ---------------- floors ----------------
    static Sprite Floor(string th, int v)
    {
        var c = new Cv(T, T); int s = v * 31 + th.Length * 7;
        switch (th)
        {
            case "lino": // worn beige linoleum checks
                {
                    Color c1 = H("#9c917a"), c2 = H("#8a806a");
                    for (int y = 0; y < T; y++) for (int x = 0; x < T; x++) c.S(x, y, ((x / 8 + y / 8) % 2 == 0) ? c1 : c2);
                    for (int i = 0; i < T; i++) { c.S(i, 0, A(Ink, .18f)); c.S(0, i, A(Ink, .18f)); c.S(i, 8, A(Ink, .1f)); c.S(8, i, A(Ink, .1f)); }
                    c.Noise(s, .06f); break;
                }
            case "wood":
                {
                    for (int y = 0; y < T; y++) for (int x = 0; x < T; x++)
                        {
                            int row = y / 4; int off = (row % 2) * 6; bool seam = y % 4 == 0 || (x + off) % 12 == 0;
                            Color b = (row % 2 == 0) ? H("#6e5038") : H("#644831");
                            c.S(x, y, seam ? Sh(b, .7f) : Sh(b, .95f + N(x / 3, y, s) * .12f));
                        }
                    c.Noise(s, .04f); break;
                }
            case "conc":
                {
                    for (int y = 0; y < T; y++) for (int x = 0; x < T; x++) c.S(x, y, Sh(Conc, .82f + N(x, y, s) * .14f + N(x / 4, y / 4, s + 1) * .08f));
                    for (int i = 0; i < T; i++) c.S(i, 15, A(Ink, .15f));
                    break;
                }
            case "carpet":
                {
                    for (int y = 0; y < T; y++) for (int x = 0; x < T; x++) c.S(x, y, Sh(H("#4c5852"), .88f + N(x, y, s) * .2f));
                    break;
                }
            case "tile":
                {
                    for (int y = 0; y < T; y++) for (int x = 0; x < T; x++) c.S(x, y, (x % 8 == 0 || y % 8 == 0) ? H("#8f948c") : Sh(H("#b6bab0"), .95f + N(x, y, s) * .07f));
                    break;
                }
        }
        // wear and tear by variant
        int kind = v % 6;
        if (kind == 1) { int x0 = (int)(N(v, 1, s) * 10) + 2, y0 = 2; for (int i = 0; i < 12; i++) { c.S(x0, y0, A(Ink, .45f)); y0++; x0 += N(i, v, s) > .5f ? 1 : (N(i, v, s) < .25f ? -1 : 0); } }
        if (kind == 2) for (int y = 0; y < T; y++) for (int x = 0; x < T; x++) { float dx = x - 8 - N(v, 2, s) * 4, dy = y - 8; if (dx * dx + dy * dy < 22 + N(x, y, s) * 10) c.S(x, y, A(H("#3a3226"), .22f)); }
        if (kind == 3) { c.R(3, 5, 2, 1, A(Ink, .3f)); c.R(10, 11, 3, 1, A(Ink, .25f)); c.S(7, 2, A(Ink, .3f)); }
        return c.Make();
    }

    // ---------------- walls: 3/4 view front face ----------------
    static Sprite Wall(string th, int v)
    {
        var c = new Cv(T, T); int s = v * 17 + 3;
        if (th == "conc")
        {
            for (int y = 0; y < T; y++) for (int x = 0; x < T; x++) c.S(x, y, Sh(Conc, .9f + N(x, y, s) * .12f - (y > 12 ? .15f : 0)));
            c.R(0, 0, T, 1, Sh(Conc, 1.2f)); c.R(0, 7, T, 1, A(Ink, .35f)); c.R(0, 14, T, 2, Sh(Conc, .55f));
            if (v % 3 == 1) for (int y = 1; y < 12; y++) c.S(4 + (y % 3 == 0 ? 1 : 0), y, A(Rust, .5f - y * .03f));
            if (v % 4 == 2) { c.R(5, 2, 6, 4, H("#3a3d40")); c.R(6, 3, 4, 2, H("#a89a4a")); } // vent / lamp box
            return c.Make();
        }
        // beige two-tone corridor wall (paint on top, darker wainscot below)
        for (int y = 0; y < T; y++) for (int x = 0; x < T; x++)
            {
                Color b = y < 9 ? WallUp : WallLow;
                b = Sh(b, .94f + N(x, y, s) * .1f);
                if (y < 9) b = Color.Lerp(b, Stain, Mathf.Clamp01(N(x / 3, 0, s) - .55f) * (1 - y / 9f) * 1.6f); // water stains from the ceiling
                c.S(x, y, b);
            }
        c.R(0, 0, T, 1, Sh(WallUp, 1.12f)); c.R(0, 9, T, 1, Stripe); c.R(0, 14, T, 2, Base);
        for (int i = 0; i < 4; i++) { int px = (int)(N(i, v, s) * 14), py = (int)(N(v, i, s) * 7) + 1; if (N(i, i, s + v) > .55f) { c.R(px, py, 2, 1, Peel); c.S(px + 1, py + 1, Peel); } }
        switch (th == "beige" ? v % 9 : 0)
        {
            case 1: c.L(3, 1, 6, 6, A(Ink, .55f)); c.L(6, 6, 5, 12, A(Ink, .45f)); break;                     // crack
            case 2: c.Box(2, 2, 12, 7, H("#5a6e7a"), WoodD); c.L(1, 3, 14, 6, Wood); c.L(1, 7, 14, 4, Wood); break; // boarded window
            case 3: c.Box(1, 1, 14, 8, H("#2f4a36"), WoodD); c.L(4, 4, 9, 4, A(Color.white, .5f)); c.L(4, 6, 7, 6, A(Color.white, .4f)); break; // chalkboard
            case 4: c.R(5, 2, 6, 7, H("#e9e2cc")); c.R(6, 3, 4, 2, H("#b03030")); c.R(6, 6, 4, 1, A(Ink, .5f)); c.R(6, 7, 3, 1, A(Ink, .5f)); break; // poster
            case 5: for (int i = 0; i < 5; i++) { c.R(4 + i, 3 + i * 2, 2, 2, A(Blood, .8f)); } c.R(3, 2, 3, 2, Blood2); break;  // bloody hand smear
            case 6: c.R(5, 2, 6, 3, H("#2a7a3a")); c.R(6, 3, 1, 1, Color.white); c.R(8, 3, 2, 1, Color.white); break; // exit sign
            case 7: c.R(6, 1, 4, 2, H("#3a3530")); c.R(6, 3, 4, 2, H("#f2df8a")); break; // wall lamp
        }
        return c.Make();
    }

    static Sprite Top()
    {
        var c = new Cv(T, T);
        for (int y = 0; y < T; y++) for (int x = 0; x < T; x++) c.S(x, y, Sh(H("#2a2520"), .9f + N(x, y, 9) * .15f));
        return c.Make();
    }

    // ---------------- decals ----------------
    static Sprite Decal(string kind, int v)
    {
        var c = new Cv(T, T); int s = v * 13 + kind.Length;
        switch (kind)
        {
            case "blood":
                {
                    float cx = 8, cy = 8, r = 3 + N(v, 1, s) * 3;
                    for (int y = 0; y < T; y++) for (int x = 0; x < T; x++) { float dx = x - cx, dy = (y - cy) * 1.3f; float d = Mathf.Sqrt(dx * dx + dy * dy) + N(x, y, s) * 2.5f; if (d < r) c.S(x, y, A(d < r - 1.5f ? Blood : Blood2, .85f)); }
                    for (int i = 0; i < 6; i++) c.S((int)(N(i, 3, s) * 16), (int)(N(3, i, s) * 16), A(Blood, .8f));
                    break;
                }
            case "trail": for (int x = 0; x < T; x++) { int y = 7 + (int)(Mathf.Sin(x * .5f + v) * 1.5f); c.R(x, y, 1, 2 + (x % 3 == 0 ? 1 : 0), A(Blood, .75f)); } break;
            case "paper":
                for (int i = 0; i < 3; i++) { int x = (int)(N(i, 1, s) * 10) + 1, y = (int)(N(1, i, s) * 10) + 1; c.R(x, y, 5, 4, H("#e6e0cc")); c.R(x + 1, y + 1, 3, 1, A(Ink, .35f)); c.R(x + 1, y + 2, 2, 1, A(Ink, .3f)); }
                break;
            case "debris":
                for (int i = 0; i < 9; i++) { int x = (int)(N(i, 2, s) * 15), y = (int)(N(2, i, s) * 15); c.R(x, y, 1 + (i % 2), 1, Sh(H("#8a8578"), .7f + N(i, i, s) * .5f)); }
                c.R(5, 9, 3, 2, H("#6a6458")); break;
            case "glass": for (int i = 0; i < 8; i++) { int x = (int)(N(i, 5, s) * 15), y = (int)(N(5, i, s) * 15); c.S(x, y, A(H("#cfe6ee"), .8f)); c.S(x + 1, y, A(H("#9fbac4"), .6f)); } break;
            case "can": c.R(6, 8, 4, 3, H("#b0b4b8")); c.R(6, 9, 4, 1, H("#b04030")); c.R(10, 10, 2, 1, A(Ink, .4f)); break;
        }
        return c.Make();
    }

    // ---------------- props (pivot bottom centre) ----------------
    static Sprite Desk(bool pc)
    {
        var c = new Cv(16, 18);
        c.R(0, 4, 16, 8, Wood); c.R(0, 4, 16, 1, Sh(Wood, 1.25f)); c.R(0, 12, 16, 4, WoodD); c.R(1, 16, 2, 2, Ink); c.R(13, 16, 2, 2, Ink);
        c.Noise(5, .05f);
        if (pc) { c.R(3, 0, 10, 8, H("#2c2f33")); c.R(4, 1, 8, 5, H("#36d36a")); c.R(5, 2, 4, 1, H("#b8ffcc")); c.R(5, 4, 5, 1, H("#1f8a44")); c.R(7, 8, 2, 2, H("#2c2f33")); c.R(4, 10, 8, 1, H("#1d1f22")); }
        else { c.R(2, 6, 5, 3, H("#e6e0cc")); c.R(3, 7, 3, 1, A(Ink, .4f)); c.R(10, 5, 3, 3, H("#3a5aa0")); }
        c.Outline(A(Ink, .8f)); return c.Make(.5f, 0f);
    }

    static Sprite Chair(bool fallen)
    {
        var c = new Cv(12, 14);
        if (!fallen) { c.R(2, 0, 8, 6, WoodD); c.R(2, 6, 8, 3, Wood); c.R(2, 9, 1, 5, Ink); c.R(9, 9, 1, 5, Ink); }
        else { c.R(0, 8, 10, 3, WoodD); c.R(1, 5, 7, 3, Wood); c.L(9, 8, 11, 12, Ink); c.L(2, 11, 3, 13, Ink); }
        c.Outline(A(Ink, .7f)); return c.Make(.5f, 0f);
    }

    static Sprite Locker(bool locked)
    {
        var c = new Cv(16, 26);
        c.R(1, 0, 14, 26, Metal); c.R(1, 0, 14, 1, Sh(Metal, 1.3f)); c.R(8, 0, 1, 26, Sh(Metal, .6f));
        for (int i = 0; i < 3; i++) { c.R(3, 3 + i * 2, 4, 1, Sh(Metal, .55f)); c.R(10, 3 + i * 2, 4, 1, Sh(Metal, .55f)); }
        c.R(6, 13, 1, 3, H("#c8c8c0")); c.R(10, 13, 1, 3, H("#c8c8c0"));
        if (locked) { c.R(6, 15, 5, 5, H("#d0a030")); c.R(7, 13, 3, 2, H("#909090")); c.S(8, 17, Ink); }
        else { c.L(11, 18, 14, 22, A(Ink, .5f)); c.R(3, 20, 3, 2, A(Rust, .6f)); }
        c.Noise(4, .05f); c.Outline(A(Ink, .8f)); return c.Make(.5f, 0f);
    }

    static Sprite Shelf()
    {
        var c = new Cv(16, 26);
        c.R(0, 0, 2, 26, H("#4a4e52")); c.R(14, 0, 2, 26, H("#4a4e52"));
        Color[] lab = { H("#b04030"), H("#3a70b0"), H("#d0a030"), H("#4a9050"), H("#c0c0c8") };
        for (int s = 0; s < 3; s++)
        {
            int y = 2 + s * 8; c.R(0, y + 5, 16, 2, H("#5c6064"));
            for (int i = 0; i < 4; i++) { if (N(i, s, 7) < .2f) continue; c.R(2 + i * 3, y + 1, 3, 4, H("#a8acb0")); c.R(2 + i * 3, y + 2, 3, 2, lab[(i + s * 2) % lab.Length]); }
        }
        c.Outline(A(Ink, .8f)); return c.Make(.5f, 0f);
    }

    static Sprite Books()
    {
        var c = new Cv(16, 26);
        c.R(0, 0, 16, 26, WoodD); c.R(1, 1, 14, 24, Sh(WoodD, .6f));
        Color[] bc = { H("#8a2a2a"), H("#2a4a8a"), H("#3a6a3a"), H("#8a7a3a"), H("#5a3a6a"), H("#c0b090") };
        for (int s = 0; s < 3; s++) { int y = 2 + s * 8; c.R(1, y + 6, 14, 1, Wood); for (int x = 1; x < 15; x++) if (N(x, s, 3) > .15f) { int hh = 4 + (int)(N(s, x, 4) * 2); c.R(x, y + 6 - hh, 1, hh, bc[(int)(N(x, s, 9) * 6) % 6]); } }
        c.Outline(A(Ink, .8f)); return c.Make(.5f, 0f);
    }

    static Sprite Sofa()
    {
        var c = new Cv(16, 14);
        Color s = H("#7a3a32");
        c.R(0, 0, 16, 7, Sh(s, .8f)); c.R(0, 6, 16, 6, s); c.R(0, 3, 2, 9, Sh(s, .7f)); c.R(14, 3, 2, 9, Sh(s, .7f)); c.R(8, 6, 1, 5, Sh(s, .6f));
        c.R(4, 8, 3, 2, A(H("#e0c080"), .5f)); c.R(1, 12, 2, 2, Ink); c.R(13, 12, 2, 2, Ink);
        c.Noise(6, .06f); c.Outline(A(Ink, .8f)); return c.Make(.5f, 0f);
    }

    static Sprite Safe()
    {
        var c = new Cv(16, 18);
        c.R(1, 0, 14, 17, H("#3a3f45")); c.R(1, 0, 14, 1, H("#5a6068")); c.R(2, 2, 12, 12, H("#2c3035"));
        c.Disc(7.5f, 7.5f, 3.5f, H("#9aa0a8")); c.Disc(7.5f, 7.5f, 2f, H("#555b62")); c.S(7, 4, H("#f0f0f0"));
        c.R(11, 6, 2, 4, H("#c0a040")); c.R(2, 17, 3, 1, Ink); c.R(11, 17, 3, 1, Ink);
        c.Outline(A(Ink, .9f)); return c.Make(.5f, 0f);
    }

    static Sprite Reader()
    {
        var c = new Cv(16, 22);
        c.R(7, 8, 2, 14, H("#4a4e52"));
        c.R(3, 0, 10, 10, H("#26292d")); c.R(4, 1, 8, 2, Color.white); c.R(5, 4, 6, 4, H("#3a3f45")); c.R(6, 5, 4, 1, H("#101214"));
        c.R(5, 20, 6, 2, H("#3a3e42"));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, 0f);
    }

    static Sprite Card()
    {
        var c = new Cv(12, 10);
        c.R(1, 2, 10, 7, Color.white); c.R(1, 4, 10, 1, H("#303030")); c.R(2, 6, 3, 2, H("#d0d0d0"));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, .3f);
    }

    static Sprite Key(Color g)
    {
        var c = new Cv(14, 10);
        c.Disc(3.5f, 5, 3.2f, g); c.Disc(3.5f, 5, 1.3f, Color.clear); c.p[(10 - 1 - 5) * 14 + 3] = Color.clear;
        c.R(6, 4, 7, 2, g); c.R(10, 6, 1, 2, g); c.R(12, 6, 1, 3, g); c.R(6, 4, 7, 1, Sh(g, 1.2f));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, .3f);
    }

    static Sprite Baton()
    {
        var c = new Cv(16, 16);
        c.L(2, 13, 13, 2, H("#3a2a1c")); c.L(3, 13, 14, 2, Wood); c.L(2, 12, 13, 1, Sh(Wood, 1.3f)); c.R(1, 12, 3, 3, H("#202020"));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, .25f);
    }

    static Sprite Note()
    {
        var c = new Cv(10, 10);
        c.R(1, 1, 8, 8, H("#f0e070")); c.R(1, 1, 8, 2, H("#e0c850")); c.R(2, 4, 5, 1, A(Ink, .5f)); c.R(2, 6, 6, 1, A(Ink, .5f)); c.S(8, 8, H("#c0a840"));
        c.Outline(A(Ink, .8f)); return c.Make(.5f, .3f);
    }

    static Sprite Board()
    {
        var c = new Cv(18, 22);
        c.R(1, 0, 16, 13, H("#d8d8d0")); c.R(1, 0, 16, 1, H("#9a9a90"));
        c.R(3, 2, 5, 4, H("#d03a3a")); c.R(10, 2, 5, 4, H("#e8c840")); c.R(3, 7, 5, 4, H("#3a6ad0")); c.R(10, 7, 5, 4, H("#3aa04a"));
        c.L(3, 13, 1, 21, H("#5a4a3a")); c.L(14, 13, 16, 21, H("#5a4a3a"));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, 0f);
    }

    static Sprite Fuse()
    {
        var c = new Cv(8, 12);
        c.R(2, 1, 4, 10, H("#e8c840")); c.R(2, 0, 4, 2, H("#b0b0b0")); c.R(2, 10, 4, 2, H("#b0b0b0")); c.R(3, 3, 1, 6, Sh(H("#e8c840"), 1.2f));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, .2f);
    }

    static Sprite FuseBox()
    {
        var c = new Cv(16, 22);
        c.R(2, 0, 12, 16, H("#5a5f55")); c.R(3, 1, 10, 14, H("#45493f"));
        for (int i = 0; i < 3; i++) { c.R(4 + i * 3, 3, 2, 6, H("#202020")); }
        c.R(5, 11, 6, 3, H("#e8c840")); c.L(6, 11, 9, 13, Ink); c.R(7, 16, 2, 6, H("#3a3e42"));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, 0f);
    }

    static Sprite Crate()
    {
        var c = new Cv(16, 16);
        c.R(0, 0, 16, 16, H("#9a6a36")); c.R(0, 0, 16, 2, H("#b88446")); c.R(0, 14, 16, 2, H("#5a3a1c"));
        c.R(0, 0, 2, 16, H("#6a4422")); c.R(14, 0, 2, 16, H("#6a4422")); c.L(2, 2, 13, 13, H("#6a4422")); c.L(2, 3, 12, 13, H("#6a4422"));
        c.Noise(8, .06f); c.Outline(A(Ink, .9f)); return c.Make(.5f, 0f);
    }

    static Sprite Plate()
    {
        var c = new Cv(16, 16);
        c.Box(1, 1, 14, 14, H("#6a7480"), H("#2a2e33")); c.R(4, 4, 8, 8, H("#8d9aa8")); c.R(4, 4, 8, 1, H("#b8c4d0"));
        c.R(6, 6, 4, 4, H("#e8c840")); return c.Make();
    }

    static Sprite Lever()
    {
        var c = new Cv(14, 18);
        c.R(1, 10, 12, 8, H("#3a3e42")); c.R(2, 11, 10, 6, H("#55595e")); c.L(7, 11, 10, 3, H("#a0a0a0"));
        c.Disc(10.5f, 3.5f, 3f, Color.white); c.Disc(9.8f, 2.8f, 1f, A(Color.white, 1)); c.Outline(A(Ink, .9f)); return c.Make(.5f, 0f);
    }

    static Sprite Console()
    {
        var c = new Cv(16, 22);
        c.R(1, 0, 14, 11, H("#2c2f33")); c.R(2, 1, 12, 8, H("#123018"));
        for (int i = 0; i < 4; i++) c.R(3 + i * 3, 4, 2, 2, H("#46e07a"));
        c.R(6, 11, 4, 5, H("#3a3e42")); c.R(2, 16, 12, 6, H("#45494e")); c.R(3, 17, 10, 1, H("#606468"));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, 0f);
    }

    static Sprite Door(bool locked)
    {
        var c = new Cv(16, 16);
        c.R(0, 0, 16, 16, H("#3a3128")); c.R(2, 1, 12, 15, locked ? H("#5c2a22") : H("#6e4a2c"));
        c.R(3, 2, 10, 5, Sh(locked ? H("#5c2a22") : H("#6e4a2c"), .8f)); c.R(4, 3, 8, 3, locked ? H("#2a2a2a") : H("#2a3a44"));
        c.R(11, 9, 2, 2, H("#d8c060"));
        if (locked) { c.L(2, 8, 13, 12, H("#9a9a9a")); c.L(2, 12, 13, 8, H("#9a9a9a")); c.R(6, 9, 4, 4, H("#c0a030")); }
        c.Noise(11, .05f); return c.Make();
    }

    static Sprite Gate()
    {
        var c = new Cv(16, 16);
        c.R(0, 0, 16, 16, H("#2a2d30"));
        for (int x = 1; x < 16; x += 3) c.R(x, 0, 2, 16, H("#5a6066"));
        c.R(0, 6, 16, 3, H("#a07a20")); c.R(6, 5, 4, 5, H("#d0a030"));
        return c.Make();
    }

    static Sprite Plant()
    {
        var c = new Cv(12, 18);
        c.R(3, 12, 6, 6, H("#8a4a2a")); c.R(3, 12, 6, 1, H("#a85a32"));
        c.L(6, 12, 3, 2, H("#6a5a30")); c.L(6, 12, 9, 4, H("#6a5a30")); c.L(6, 12, 6, 1, H("#7a6a3a")); c.S(2, 3, H("#8a7a40")); c.S(10, 5, H("#8a7a40"));
        c.Outline(A(Ink, .8f)); return c.Make(.5f, 0f);
    }

    static Sprite Table()
    {
        var c = new Cv(16, 14);
        c.R(0, 0, 16, 9, H("#8c8a80")); c.R(0, 0, 16, 1, H("#aaa89e")); c.R(0, 9, 16, 2, H("#5a5850")); c.R(1, 11, 2, 3, Ink); c.R(13, 11, 2, 3, Ink);
        c.R(3, 2, 4, 3, H("#e6e0cc")); c.R(9, 3, 2, 3, H("#b0b4b8"));
        c.Outline(A(Ink, .8f)); return c.Make(.5f, 0f);
    }

    static Sprite Medkit()
    {
        var c = new Cv(12, 10);
        c.R(1, 1, 10, 8, H("#f0f0ea")); c.R(1, 1, 10, 2, H("#d0d0c8")); c.R(5, 3, 2, 5, H("#d02a2a")); c.R(3, 5, 6, 1, H("#d02a2a")); c.R(3, 4, 6, 1, H("#d02a2a")); c.R(4, 0, 4, 1, H("#808080"));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, .3f);
    }

    static Sprite Ammo()
    {
        var c = new Cv(12, 10);
        c.R(1, 3, 10, 6, H("#4a5a2a")); c.R(1, 3, 10, 1, H("#6a7a3a")); c.R(3, 5, 6, 2, H("#e8c840"));
        for (int i = 0; i < 3; i++) { c.R(3 + i * 2, 0, 1, 3, H("#e8c840")); c.S(3 + i * 2, 0, H("#b08030")); }
        c.Outline(A(Ink, .9f)); return c.Make(.5f, .3f);
    }

    static Sprite Dude(Color body, Color skin, Color hair)
    {
        var c = new Cv(12, 20);
        c.R(3, 0, 6, 3, hair); c.R(3, 2, 6, 5, skin); c.S(4, 4, Ink); c.S(7, 4, Ink);
        c.R(2, 7, 8, 7, body); c.R(1, 8, 1, 5, skin); c.R(10, 8, 1, 5, skin); c.R(3, 14, 2, 6, Sh(body, .6f)); c.R(7, 14, 2, 6, Sh(body, .6f));
        c.Outline(A(Ink, .9f)); return c.Make(.5f, 0f);
    }

    static Sprite Cone()
    {
        var c = new Cv(48, 32);
        Color warm = H("#fff3c4");
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 48; x++)
            {
                float u = x / 47f;
                float hw = 0.08f + u * 0.92f;
                float dy = Mathf.Abs(y - 15.5f) / 15.5f;
                if (dy < hw)
                {
                    float radial = 1f - u;
                    float edge = 1f - (dy / hw);
                    float a = Mathf.Pow(radial, 1.35f) * Mathf.Sqrt(edge) * 0.34f;
                    c.S(x, y, A(warm, a));
                }
            }
        return c.Make(0f, .5f);
    }

    static Sprite Slash()
    {
        var c = new Cv(16, 24);
        Color cream = H("#fff8dc"), core = Color.white;
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 16; x++)
            {
                float dx = x - 1.5f, dy = y - 11.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (x >= 3 && r >= 7.5f && r <= 12.2f)
                {
                    float ang = Mathf.Atan2(dy, dx);
                    float taper = Mathf.Clamp01(1f - Mathf.Abs(ang) / 1.15f);
                    if (taper > 0.05f) c.S(x, y, A(r > 9.5f && r < 11.2f ? core : cream, taper * 0.85f));
                }
            }
        return c.Make(0f, .5f);
    }

    static Sprite Glow()
    {
        var c = new Cv(32, 32);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = x - 15.5f, dy = y - 15.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 15.5f;
                if (d < 1f) { float k = 1f - d; c.S(x, y, new Color(1f, .96f, .86f, k * k * 0.42f)); }
            }
        return c.Make(.5f, .5f);
    }

    static Sprite WallShadow()
    {
        var c = new Cv(T, T);
        for (int y = 0; y < 5; y++)
            c.R(0, y, T, 1, new Color(0, 0, 0, (5 - y) / 5f * 0.36f));
        return c.Make(.5f, .5f);
    }
}
