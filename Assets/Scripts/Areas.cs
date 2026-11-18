using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One playable location. Map chars:
///  ' ' void   '.' floor   'U' / portal chars = doors   'P' player start   'Z' zombie
///  c crate  p plate  r g b y colored levers  n console  q w e o code notes  k keypad
///  f fuse  u fuse box  m medkit  a ammo  K security key  h shelf  s sofa  N survivor
/// </summary>
public class AreaDef
{
    public string key, title, hint;
    public string[] map; public int W, H;
    public Dictionary<char, string> portals = new Dictionary<char, string>();
    public Color tint = Color.white;
    public int puzzle;                 // 1..7 for puzzle rooms
    public int seqLen, codeLen; public bool flash, crates, fuses, alarm;
    public int randZ, randAmmo, randMed, seed; public float sight = 6f;
    public List<(float c, float r, string t)> labels = new List<(float, float, string)>();
}

public static class Areas
{
    static Dictionary<string, AreaDef> d;

    public static AreaDef Get(string k) { Init(); return d[k]; }
    public static string TitleOf(string k) { Init(); return k == "tunnel" ? "ТУННЕЛЬ — ВЫХОД" : d[k].title; }
    public static IEnumerable<AreaDef> All() { Init(); return d.Values; }

    class RB
    {
        public char[][] g; int w, h;
        public RB(int w, int h, int uCol)
        {
            this.w = w; this.h = h;
            int W = w + 4, H = h + 2;
            g = new char[H][];
            for (int r = 0; r < H; r++) { g[r] = new char[W]; for (int c = 0; c < W; c++) g[r][c] = ' '; }
            for (int r = 1; r <= h; r++) for (int c = 2; c < w + 2; c++) g[r][c] = '.';
            g[0][2 + uCol] = 'U';
        }
        public RB Void(int x, int y, int ww, int hh)
        {
            for (int j = 0; j < hh; j++) for (int i = 0; i < ww; i++) g[1 + y + j][2 + x + i] = ' ';
            return this;
        }
        public RB Put(char ch, params int[] xy)
        {
            for (int i = 0; i + 1 < xy.Length; i += 2) g[1 + xy[i + 1]][2 + xy[i]] = ch;
            return this;
        }
        public string[] Rows() { var o = new string[g.Length]; for (int i = 0; i < g.Length; i++) o[i] = new string(g[i]); return o; }
    }

    static AreaDef Add(string key, string title, string[] rows, Color tint, string hint = "")
    {
        int W = 0; foreach (var r in rows) W = Mathf.Max(W, r.Length);
        for (int i = 0; i < rows.Length; i++) rows[i] = rows[i].PadRight(W);
        var a = new AreaDef { key = key, title = title, map = rows, W = W, H = rows.Length, tint = tint, hint = hint };
        d[key] = a; return a;
    }

    static void Init()
    {
        if (d != null) return;
        d = new Dictionary<string, AreaDef>();

        // ---- Bunker (-1 floor): one room, shelves with cans and a sofa ----
        var bk = Add("bunker", "Бункер (−1 этаж)",
            new RB(14, 6, 6).Put('h', 1, 0, 2, 0, 3, 0).Put('s', 8, 5, 9, 5, 10, 5, 11, 5).Put('N', 12, 2).Put('P', 6, 3).Rows(),
            new Color(.72f, .74f, .8f), "Серый бункер. Полки с консервами и диван. Выход — дверь наверху.");
        bk.portals['U'] = "hub";

        // ---- Floor 0 hub: ring of corridors around the courtyard/canteen ----
        var hub = Add("hub", "Блоки КБТУ — 0 этаж", HubRows(), new Color(.82f, .88f, 1f),
            "Сирена. Здание закрыто. Блоки: KB (верх), AB (лево), PF (право), TB (низ).");
        hub.alarm = true; hub.sight = 8f; hub.randZ = 16; hub.randAmmo = 6; hub.randMed = 2; hub.seed = 11;
        string[] names = { "", "p1", "p2", "p3", "p4", "p5", "p6", "p7" };
        for (int i = 1; i <= 7; i++) hub.portals[(char)('0' + i)] = names[i];
        hub.portals['S'] = "sec"; hub.portals['X'] = "tunnel"; hub.portals['B'] = "bunker";
        hub.portals['a'] = "inf1"; hub.portals['b'] = "inf2"; hub.portals['c'] = "inf3";
        hub.labels.Add((24, 3.5f, "KB — Казыбек би"));
        hub.labels.Add((3.5f, 12, "AB")); hub.labels.Add((43.5f, 12, "PF"));
        hub.labels.Add((24, 25.5f, "TB — Толе би"));
        hub.labels.Add((24, 15, "СТОЛОВАЯ / COWORKING"));

        // ---- 7 puzzle rooms ----
        var p1 = Add("p1", "Каб. 157 — AB",
            new RB(16, 8, 7).Put('r', 2, 2).Put('g', 7, 2).Put('b', 12, 2).Put('n', 6, 4).Put('Z', 12, 5, 2, 6).Rows(),
            new Color(1f, .95f, .75f), "Изучи консоль (E) — порядок рычагов. Жми E на цветных рычагах по порядку.");
        p1.puzzle = 1; p1.seqLen = 3;

        var p2 = Add("p2", "Каб. 117 — PF",
            new RB(20, 9, 9).Put('k', 8, 3).Put('q', 1, 1).Put('w', 17, 1).Put('e', 1, 7).Put('Z', 9, 1, 1, 5, 18, 5, 10, 7).Rows(),
            new Color(1f, .8f, .8f), "Найди 3 записки с цифрами (E) и введи код на замке (E).");
        p2.puzzle = 2; p2.codeLen = 3;

        var p3 = Add("p3", "726 Абай пресс-центр — KB",
            new RB(14, 9, 6).Put('c', 3, 3, 6, 3, 9, 3).Put('p', 3, 6, 6, 6, 9, 6).Put('Z', 11, 1, 1, 7, 12, 6).Rows(),
            new Color(.8f, .9f, 1f), "Задвинь ящики на 3 плиты. R — сбросить комнату, если застрял.");
        p3.puzzle = 3; p3.crates = true;

        var p4 = Add("p4", "400Б Кабинет Кунаева — 4 этаж",
            new RB(24, 12, 11).Void(6, 3, 2, 2).Void(15, 3, 2, 2).Void(6, 7, 2, 2).Void(15, 7, 2, 2).Void(10, 5, 4, 2)
                .Put('f', 1, 10, 22, 1, 21, 10).Put('u', 11, 4)
                .Put('Z', 4, 5, 19, 5, 11, 9, 8, 1, 16, 1, 3, 10).Rows(),
            new Color(.85f, .85f, 1f), "Собери 3 предохранителя (E) и вставь их в щиток (E).");
        p4.puzzle = 4; p4.fuses = true;

        var p5 = Add("p5", "Каб. 346 — TB",
            new RB(18, 9, 8).Put('r', 3, 6).Put('g', 6, 6).Put('b', 11, 6).Put('y', 14, 6).Put('n', 8, 1)
                .Put('Z', 1, 7, 16, 7, 8, 3).Rows(),
            new Color(.9f, 1f, .85f), "Консоль покажет 5 цветов на несколько секунд. Запомни и повтори на рычагах.");
        p5.puzzle = 5; p5.seqLen = 5; p5.flash = true;

        var p6 = Add("p6", "Каб. 270 — AB",
            new RB(24, 12, 11).Void(5, 2, 2, 3).Void(16, 2, 2, 3).Void(5, 7, 2, 3).Void(16, 7, 2, 3)
                .Put('k', 11, 5).Put('q', 1, 1).Put('w', 22, 1).Put('e', 1, 10).Put('o', 22, 10)
                .Put('Z', 11, 2, 3, 5, 20, 5, 11, 9, 8, 6, 14, 6, 3, 8, 20, 8).Rows(),
            new Color(.7f, .75f, .7f), "Код из 4 цифр. Записки по углам. Много зомби!");
        p6.puzzle = 6; p6.codeLen = 4;

        var p7 = Add("p7", "Каб. 428 — KB, 4 этаж",
            new RB(26, 14, 12).Void(6, 3, 3, 2).Void(17, 3, 3, 2).Void(6, 9, 3, 2).Void(17, 9, 3, 2)
                .Put('f', 1, 12, 24, 12, 24, 1).Put('u', 12, 6)
                .Put('r', 8, 7).Put('g', 11, 7).Put('b', 14, 7).Put('y', 17, 7).Put('n', 12, 11)
                .Put('Z', 12, 3, 3, 3, 22, 3, 3, 7, 22, 7, 12, 9, 6, 12, 18, 12, 1, 6, 24, 6).Rows(),
            new Color(.8f, .7f, .7f), "Финал: вставь предохранители в щиток И нажми рычаги в порядке с консоли.");
        p7.puzzle = 7; p7.fuses = true; p7.seqLen = 4;

        // ---- Security (key) ----
        var sec = Add("sec", "Секьюрити",
            new RB(24, 12, 11).Void(5, 3, 2, 6).Void(17, 3, 2, 6).Put('K', 11, 9)
                .Put('Z', 11, 2, 3, 2, 20, 2, 11, 6, 3, 10, 20, 10, 8, 5, 14, 5, 8, 8, 14, 8).Rows(),
            new Color(.7f, .8f, .9f), "Забери ключ и уходи!");

        // ---- Infested rooms (extra offices): dangerous, but ammo and medkits ----
        string[] ik = { "inf1", "inf2", "inf3" }; string[] it = { "Каб. 305 — ОПАСНО", "Каб. 162 — ОПАСНО", "Каб. 25 — ОПАСНО" };
        for (int i = 0; i < 3; i++)
        {
            var a = Add(ik[i], it[i],
                new RB(22, 12, 10).Void(5, 3, 2, 2).Void(10, 5, 2, 2).Void(15, 3, 2, 2).Void(8, 8, 2, 2).Void(16, 8, 2, 2).Rows(),
                new Color(.75f, .5f, .5f), "Здесь полно зомби. Зато есть патроны и аптечки.");
            a.randZ = 9; a.randAmmo = 3; a.randMed = 1; a.seed = 100 + i; a.sight = 7f;
        }

        foreach (var a in d.Values) { a.portals['U'] = a.key == "hub" ? "x" : "hub"; if (a.key == "hub") a.portals.Remove('U'); }
    }

    static string[] HubRows()
    {
        int W = 48, H = 30;
        var g = new char[H][];
        for (int r = 0; r < H; r++) { g[r] = new char[W]; for (int c = 0; c < W; c++) g[r][c] = ' '; }
        for (int r = 2; r <= 27; r++) for (int c = 2; c <= 45; c++) g[r][c] = '.';
        for (int r = 6; r <= 23; r++) for (int c = 6; c <= 41; c++) g[r][c] = ' ';
        g[10][1] = '1'; g[10][46] = '2'; g[1][22] = '3'; g[6][24] = '4'; g[28][30] = '5';
        g[20][1] = '6'; g[20][46] = '7'; g[1][36] = 'S'; g[28][3] = 'X'; g[28][10] = 'B';
        g[15][1] = 'a'; g[15][46] = 'b'; g[6][15] = 'c';
        var o = new string[H]; for (int i = 0; i < H; i++) o[i] = new string(g[i]);
        return o;
    }

    /// <summary>Editor/CI sanity check: every portal reachable, every object reachable.</summary>
    public static string Validate()
    {
        Init(); var sb = new System.Text.StringBuilder();
        foreach (var a in d.Values)
        {
            int sx = -1, sy = -1;
            for (int r = 0; r < a.H && sx < 0; r++) for (int c = 0; c < a.W; c++)
                    if (a.map[r][c] == 'U' || a.map[r][c] == 'P') { sx = c; sy = r; break; }
            if (a.key == "hub") { sx = 2; sy = 2; }
            if (sx < 0) { sb.AppendLine(a.key + ": no start"); continue; }
            var seen = new bool[a.W, a.H]; var q = new Queue<Vector2Int>(); q.Enqueue(new Vector2Int(sx, sy)); seen[sx, sy] = true;
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int x = p.x + dx[k], y = p.y + dy[k];
                    if (x < 0 || y < 0 || x >= a.W || y >= a.H || seen[x, y] || a.map[y][x] == ' ') continue;
                    char ch = a.map[y][x];
                    seen[x, y] = true;
                    if (ch == 'h' || ch == 's' || ch == 'N' || ch == 'u' || ch == 'n' || ch == 'k' || ch == 'c') continue; // solid: reach but don't pass
                    if (a.portals.ContainsKey(ch)) continue;
                    q.Enqueue(new Vector2Int(x, y));
                }
            }
            for (int r = 0; r < a.H; r++) for (int c = 0; c < a.W; c++)
                    if (a.map[r][c] != ' ' && !seen[c, r]) sb.AppendLine(a.key + ": unreachable '" + a.map[r][c] + "' at " + c + "," + r);
        }
        return sb.Length == 0 ? "OK" : sb.ToString();
    }
}
