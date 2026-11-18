using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Procedural 8x8 pixel-art placeholders. Replace with real sprites any time.</summary>
public static class Sprites
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly Dictionary<char, Color> pal = new Dictionary<char, Color>();

    static Color H(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

    static Sprites()
    {
        pal['.'] = Color.clear; pal['k'] = H("#14141c"); pal['s'] = H("#e0b48c"); pal['h'] = H("#5a3a22");
        pal['b'] = H("#3a6ea5"); pal['p'] = H("#2b2f45"); pal['g'] = H("#6f9a5a"); pal['x'] = H("#6a2a2a");
        pal['r'] = H("#d23a3a");
    }

    public static Sprite Get(string k)
    {
        if (cache.TryGetValue(k, out var s)) return s;
        s = Make(k); cache[k] = s; return s;
    }

    static float N(int x, int y, int s)
    {
        unchecked
        {
            int h = x * 73856093 ^ y * 19349663 ^ s * 83492791;
            h = (h >> 13) ^ h;
            return (h & 0xffff) / 65535f;
        }
    }

    static Sprite Gen(Func<int, int, Color> f)
    {
        var t = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) t.SetPixel(x, y, f(x, y));
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f), 8);
    }

    static Sprite Art(string[] rows) { return Gen((x, y) => pal[rows[7 - y][x]]); }
    static Color G(float v) { return new Color(v, v, v, 1); }

    static Sprite Make(string k)
    {
        switch (k)
        {
            case "player":
                return Art(new[] { "..hhhh..", ".hssssh.", ".ssssss.", "..bbbb..", ".bbbbbb.", ".sbbbbs.", "..pppp..", "..p..p.." });
            case "zombie":
                return Art(new[] { "..gggg..", ".gggggg.", ".grggrg.", "..gggg..", ".xxxxxx.", "gxxxxxxg", "..xxxx..", "..x..x.." });
            case "floor0": return Gen((x, y) => G(.40f + N(x, y, 1) * .07f - ((x == 0 || y == 0) ? .08f : 0)));
            case "floor1": return Gen((x, y) => G(.37f + N(x, y, 2) * .08f - ((x == 0 || y == 0) ? .08f : 0)));
            case "wall":
                return Gen((x, y) =>
                {
                    bool mortar = y % 4 == 0 || (x + ((y / 4) % 2) * 4) % 8 == 0;
                    float v = mortar ? .14f : .30f + N(x, y, 3) * .06f;
                    if (y == 7) v += .1f;
                    return G(v);
                });
            case "crate":
                return Gen((x, y) =>
                {
                    bool border = x == 0 || y == 0 || x == 7 || y == 7;
                    bool diag = x == y || x == 7 - y;
                    return (border || diag) ? H("#4a2e14") : H("#a06a32");
                });
            case "plate":
                return Gen((x, y) =>
                {
                    bool border = x == 0 || y == 0 || x == 7 || y == 7;
                    bool inner = x >= 2 && x <= 5 && y >= 2 && y <= 5;
                    return border ? G(.15f) : (inner ? H("#7d8a9a") : G(.3f));
                });
            case "btn":
                return Gen((x, y) =>
                {
                    float dx = x - 3.5f, dy = y - 3.5f; float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < 2.2f) return Color.white;
                    if (d < 3.6f) return G(.25f);
                    return Color.clear;
                });
            case "note":
                return Gen((x, y) => (x >= 1 && x <= 6 && y >= 1 && y <= 6) ? ((y % 2 == 0 && x > 1 && x < 6) ? G(.55f) : H("#f2ecd0")) : Color.clear);
            case "keypad":
                return Gen((x, y) =>
                {
                    if (x < 1 || x > 6 || y < 0 || y > 7) return Color.clear;
                    if (y >= 5) return H("#40d070");
                    return ((x + y) % 2 == 0) ? G(.6f) : G(.35f);
                });
            case "fuse":
                return Gen((x, y) => (x >= 3 && x <= 4 && y >= 1 && y <= 6) ? ((y == 1 || y == 6) ? G(.6f) : H("#e8c840")) : Color.clear);
            case "fusebox":
                return Gen((x, y) =>
                {
                    if (x < 1 || x > 6 || y < 0 || y > 7) return Color.clear;
                    if (x >= 3 && x <= 4 && y >= 3 && y <= 4) return H("#d23a3a");
                    return G(.45f);
                });
            case "medkit":
                return Gen((x, y) =>
                {
                    if (x < 1 || x > 6 || y < 1 || y > 6) return Color.clear;
                    bool cross = (x >= 3 && x <= 4) || (y >= 3 && y <= 4);
                    return cross ? H("#d23a3a") : Color.white;
                });
            case "ammo":
                return Gen((x, y) =>
                {
                    if (x < 1 || x > 6 || y < 1 || y > 5) return Color.clear;
                    return y == 3 ? H("#3a2a10") : H("#e8c840");
                });
            case "key":
                return Gen((x, y) =>
                {
                    float dx = x - 2.5f, dy = y - 4.5f;
                    bool ring = Mathf.Sqrt(dx * dx + dy * dy) < 2.2f && Mathf.Sqrt(dx * dx + dy * dy) > 1f;
                    bool shaft = x >= 4 && x <= 6 && y == 4;
                    bool tooth = (x == 5 || x == 6) && (y == 3 || y == 2) && x != 4;
                    return (ring || shaft || tooth) ? H("#ffd940") : Color.clear;
                });
            case "door":
                return Gen((x, y) =>
                {
                    if (x < 1 || x > 6) return Color.clear;
                    if (x == 5 && y == 3) return H("#e8c840");
                    return ((x + y / 3) % 2 == 0) ? H("#6b4423") : H("#5a3a1e");
                });
            case "console":
                return Gen((x, y) =>
                {
                    if (x < 0 || y < 1) return Color.clear;
                    if (y >= 3 && y <= 6 && x >= 1 && x <= 6) return H("#40d070");
                    return G(.2f);
                });
            case "shelf":
                return Gen((x, y) =>
                {
                    if (y % 3 == 0) return H("#5a3a1e");
                    if (y % 3 == 1) return (x % 2 == 0) ? H("#c0c0c8") : H("#d28a3a");
                    return (x % 2 == 0) ? H("#9a3a3a") : H("#c0c0c8");
                });
            case "sofa":
                return Gen((x, y) =>
                {
                    if (y >= 5) return H("#7a2e2e");
                    if (y >= 1) return H("#9a3e3e");
                    return Color.clear;
                });
            case "dot":
                return Gen((x, y) => (x >= 3 && x <= 4 && y >= 3 && y <= 4) ? H("#ffe680") : Color.clear);
            case "blob":
                return Gen((x, y) =>
                {
                    float dx = x - 3.5f, dy = y - 3.5f;
                    return Mathf.Sqrt(dx * dx + dy * dy) < 3.6f ? Color.white : Color.clear;
                });
        }
        return Gen((x, y) => Color.magenta);
    }
}
