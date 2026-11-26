using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// One playable KBTU location. Map chars:
///   ' ' void/wall   '.' floor   'U' / portal chars = doors   'P' player start   'Z' zombie
///   y locker key   L padlocked locker   R card reader   V word terminal   Q dial safe
///   q w e o clue notes   f fuse   u fusebox   c crate   p plate   r g t j colored levers (PF/KB/TB/AB)
///   n console   K tunnel key   m medkit   A ammo
///   h canned shelf   s sofa   N survivor   D desk   H bookshelf   l locker   T table
///   i chair   I fallen chair   v dead plant   W KBTU board   M dossier note
/// </summary>
public class AreaDef
{
    public string key, title, hint, dossier;
    public string[] map;
    public int W, H;
    public readonly Dictionary<char, string> portals = new Dictionary<char, string>();
    public Color tint = Color.white;
    public string floorTheme = "lino", wallTheme = "beige";
    public bool isClassroom;

    public int puzzle; // 1..7 for puzzle rooms
    public bool needKeyCard, needWord, needSafe, needFuses, needCrates, lockerHasFuse, flash, alarm;
    public int seqLen;
    public string wordGroup;

    public int randZ, randAmmo, randMed, seed;
    public float sight = 6f;
    public readonly List<(float c, float r, string t)> labels = new List<(float, float, string)>();
}

public static class Areas
{
    public const string SolidChars = "DHlThsNunVQLRWck";
    static Dictionary<string, AreaDef> d;

    public static AreaDef Get(string k) { Init(); return d[k]; }
    public static string TitleOf(string k) { Init(); return k == "tunnel" ? "ТУННЕЛЬ — ВЫХОД" : d[k].title; }
    public static IEnumerable<AreaDef> All() { Init(); return d.Values; }

    class RB
    {
        public readonly char[][] g;
        public RB(int w, int h, int uCol)
        {
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

    static AreaDef Add(string key, string title, string[] rows, Color tint, string hint,
        string floorTheme = "lino", string wallTheme = "beige", bool isClassroom = false, string dossier = null)
    {
        int W = 0; foreach (var r in rows) W = Mathf.Max(W, r.Length);
        for (int i = 0; i < rows.Length; i++) rows[i] = rows[i].PadRight(W);
        var a = new AreaDef
        {
            key = key, title = title, map = rows, W = W, H = rows.Length, tint = tint,
            hint = hint, floorTheme = floorTheme, wallTheme = wallTheme, isClassroom = isClassroom, dossier = dossier
        };
        d[key] = a;
        return a;
    }

    static void Init()
    {
        if (d != null) return;
        d = new Dictionary<string, AreaDef>();

        // ---- Bunker (-1 floor): shelves with cans, sofa, 3 survivors ----
        var bk = Add("bunker", "Бункер (−1 этаж)",
            new RB(16, 7, 7)
                .Put('h', 1, 0, 2, 0, 3, 0, 4, 0)
                .Put('s', 10, 6, 11, 6, 12, 6, 13, 6)
                .Put('T', 5, 3).Put('i', 4, 3, 6, 3).Put('I', 2, 5).Put('M', 5, 4)
                .Put('N', 11, 4, 13, 3, 14, 5)
                .Put('m', 1, 6).Put('P', 7, 4).Rows(),
            new Color(.82f, .84f, .88f),
            "Серый бункер. Полки с консервами и диван. Выход в университет — дверь наверху.",
            floorTheme: "conc", wallTheme: "conc",
            dossier: "Журнал №1: Сигнализация сработала, когда зомби прорвали турникеты со стороны Казыбек би. Главный выход и все ворота закрыты — спасёт только подземный туннель (0 этаж, TB слева), но ключ у охраны.");
        bk.portals['U'] = "hub";

        // ---- Floor 0 hub: KBTU square ring around Canteen / Coworking ----
        var hub = Add("hub", "Блоки КБТУ — 0 этаж", HubRows(), new Color(.94f, .93f, .9f),
            "Сирена! Перемещайся по 0 этажу между блоками: KB (верх), AB (лево), PF (право), TB (низ). Tab/M — карта.",
            floorTheme: "lino", wallTheme: "beige",
            dossier: "Сводка штаба КБТУ: 1–2 этажи AB и PF пали первыми. Весь 4 этаж и 2 этаж TB полностью захвачены. Связной отряд ушёл к 726 кабинету и Кунаева 400Б — и не вернулся.");
        hub.alarm = true; hub.sight = 8f; hub.randZ = 16; hub.randAmmo = 6; hub.randMed = 2; hub.seed = 11;
        string[] pKeys = { "", "p1", "p2", "p3", "p4", "p5", "p6", "p7" };
        for (int i = 1; i <= 7; i++) hub.portals[(char)('0' + i)] = pKeys[i];
        hub.portals['S'] = "sec"; hub.portals['X'] = "tunnel"; hub.portals['B'] = "bunker";
        hub.portals['a'] = "inf1"; hub.portals['b'] = "inf2"; hub.portals['c'] = "inf3";
        hub.labels.Add((24, 3.5f, "KB — Казыбек би (Зелёный)"));
        hub.labels.Add((3.5f, 12f, "AB (Жёлтый)"));
        hub.labels.Add((43.5f, 12f, "PF (Красный)"));
        hub.labels.Add((24, 25.5f, "TB — Толе би (Синий)"));
        hub.labels.Add((24, 15f, "СТОЛОВАЯ / COWORKING"));

        // ---- Stage 1: Locker key -> Padlocked locker -> Keycard -> Card reader ----
        var p1 = Add("p1", "Этап 1: Каб. 157 — Блок AB",
            new RB(18, 9, 8)
                .Put('H', 0, 0, 1, 0).Put('R', 10, 0).Put('L', 16, 0)
                .Put('D', 2, 1, 6, 2, 11, 2, 14, 2, 3, 5, 7, 5, 12, 5)
                .Put('i', 2, 2, 6, 3, 11, 3, 3, 6, 7, 6)
                .Put('I', 14, 3, 12, 6).Put('v', 17, 7)
                .Put('y', 1, 7).Put('M', 4, 1)
                .Put('Z', 12, 3, 5, 7, 15, 6).Rows(),
            new Color(1f, .96f, .86f),
            "Найди ключ в аудитории (E), открой запертый шкаф и приложи карту к считывателю у выхода.",
            floorTheme: "lino", wallTheme: "beige", isClassroom: true,
            dossier: "Запись студента (AB 157): Лаборант бросил ключи от шкафа в дальнем углу и побежал на лестницу. Пропуск от 1 блока заперт в металлическом шкафчике.");
        p1.puzzle = 1; p1.needKeyCard = true;

        // ---- Stage 2: KBTU word terminal + 3 clue notes ----
        var p2 = Add("p2", "Этап 2: Каб. 117 — Блок PF",
            new RB(20, 9, 9)
                .Put('H', 0, 0, 19, 0).Put('v', 16, 0).Put('l', 18, 7, 19, 7)
                .Put('D', 4, 2, 14, 2, 5, 6, 14, 6)
                .Put('i', 4, 3, 14, 3, 5, 7, 14, 7)
                .Put('V', 9, 3).Put('q', 1, 1).Put('w', 18, 1).Put('e', 1, 7).Put('M', 11, 4)
                .Put('Z', 9, 1, 2, 5, 17, 5, 10, 7).Rows(),
            new Color(1f, .88f, .86f),
            "Найди 3 записки с подсказками (E) и введи кбтушное слово-пароль на терминале (E).",
            floorTheme: "lino", wallTheme: "beige", isClassroom: true,
            dossier: "Служебная записка (PF 117): Система доступа крыла Панфилова принимает только одно кодовое слово из архива КБТУ. Без всех трёх подсказок не угадать.");
        p2.puzzle = 2; p2.needWord = true; p2.wordGroup = "pf";

        // ---- Stage 3: Abai Press Center 726 — 3 notes + Interactive Dial Safe ----
        var p3 = Add("p3", "Этап 3: Каб. 726 «Абай пресс-центр» — KB",
            new RB(18, 10, 8)
                .Put('H', 0, 0, 1, 0, 16, 0, 17, 0).Put('v', 0, 9)
                .Put('T', 3, 4, 4, 4, 13, 4, 14, 4).Put('I', 4, 6, 13, 6)
                .Put('Q', 8, 2).Put('q', 1, 8).Put('w', 16, 1).Put('e', 16, 8).Put('M', 5, 2)
                .Put('m', 1, 2)
                .Put('Z', 8, 6, 2, 7, 15, 7, 11, 2).Rows(),
            new Color(.88f, .95f, .9f),
            "Найди 3 листка с числами (E) и подбери 3 положения лимба на сейфе (E).",
            floorTheme: "carpet", wallTheme: "beige",
            dossier: "Пресс-центр имени Абая (726): В старом сейфе репортёров лежит резервный дробовик. Комбинацию разделили между столами.");
        p3.puzzle = 3; p3.needSafe = true;

        // ---- Stage 4: 400Б Кабинет Кунаева — 4th floor fuses ----
        var p4 = Add("p4", "Этап 4: 400Б «Кабинет Кунаева» — 4 этаж",
            new RB(24, 12, 11)
                .Void(5, 3, 2, 2).Void(17, 3, 2, 2).Void(5, 7, 2, 2).Void(17, 7, 2, 2).Void(10, 5, 4, 2)
                .Put('H', 0, 0, 1, 0, 22, 0, 23, 0).Put('v', 8, 0, 14, 0)
                .Put('D', 11, 8, 12, 8).Put('i', 11, 9, 12, 9).Put('M', 10, 8)
                .Put('f', 1, 10, 22, 1, 22, 10).Put('u', 11, 3).Put('A', 1, 2)
                .Put('Z', 3, 5, 20, 5, 11, 10, 8, 1, 15, 1, 3, 10).Rows(),
            new Color(.9f, .9f, .96f),
            "4 этаж захвачен! Собери 3 предохранителя (E) между колоннами и вставь в щиток (E).",
            floorTheme: "wood", wallTheme: "beige",
            dossier: "Мемориальный зал Кунаева (400Б): Свет на 4 этаже выбило при срабатывании гермозатворов. Восстанови щиток у центральных колонн.");
        p4.puzzle = 4; p4.needFuses = true;

        // ---- Stage 5: Crates & plates in TB 346 ----
        var p5 = Add("p5", "Этап 5: Каб. 346 — Блок TB",
            new RB(18, 11, 8)
                .Put('D', 0, 0, 1, 0, 16, 0, 17, 0).Put('M', 2, 1).Put('v', 17, 10)
                .Put('c', 4, 3, 8, 3, 12, 3).Put('p', 4, 7, 8, 7, 12, 7)
                .Put('Z', 15, 2, 2, 9, 15, 9).Rows(),
            new Color(.88f, .93f, 1f),
            "Задвинь тяжёлые ящики на все 3 нажимные плиты. R — сбросить ящики.",
            floorTheme: "tile", wallTheme: "beige", isClassroom: true,
            dossier: "Складская аудитория TB 346: Датчики давления в полу разблокируют оружейный шлюз крыла Толе би.");
        p5.puzzle = 5; p5.needCrates = true;

        // ---- Stage 6: Flashing KBTU wing color sequence (AB 270) ----
        var p6 = Add("p6", "Этап 6: Каб. 270 — Блок AB, 2 этаж",
            new RB(24, 12, 11)
                .Void(5, 2, 2, 2).Void(17, 2, 2, 2).Void(5, 8, 2, 2).Void(17, 8, 2, 2)
                .Put('D', 1, 0, 22, 0).Put('l', 0, 11, 23, 11).Put('I', 12, 9)
                .Put('W', 13, 1).Put('M', 9, 1).Put('n', 11, 2)
                .Put('r', 8, 6).Put('g', 10, 6).Put('t', 13, 6).Put('j', 15, 6)
                .Put('m', 1, 10).Put('A', 22, 10)
                .Put('Z', 3, 5, 20, 5, 11, 9, 8, 4, 15, 4, 3, 9, 20, 9, 12, 4).Rows(),
            new Color(.95f, .9f, .76f),
            "Консоль (E) покажет 5 цветов блоков КБТУ на 4.5 сек. Запомни и нажми рычаги по порядку!",
            floorTheme: "lino", wallTheme: "beige",
            dossier: "Диспетчерская AB 270: Красный — Панфилова, Зелёный — Казыбек би, Синий — Толе би, Жёлтый — Абылай хана. Прошивка требует точный код цветов.");
        p6.puzzle = 6; p6.seqLen = 5; p6.flash = true;

        // ---- Stage 7: Final Cab 428 — Key -> Locker (3rd Fuse) -> Fusebox -> KBTU Command Word ----
        var p7 = Add("p7", "Этап 7: Каб. 428 — KB, 4 этаж (Финал)",
            new RB(26, 14, 12)
                .Void(6, 3, 2, 2).Void(18, 3, 2, 2).Void(6, 9, 2, 2).Void(18, 9, 2, 2)
                .Put('H', 0, 0, 25, 0).Put('D', 10, 6, 14, 6).Put('M', 12, 2)
                .Put('L', 1, 1).Put('y', 24, 12)
                .Put('f', 1, 12, 24, 1).Put('u', 12, 4)
                .Put('V', 12, 8).Put('q', 10, 11).Put('w', 15, 11).Put('e', 12, 12)
                .Put('A', 4, 7).Put('m', 21, 7)
                .Put('Z', 12, 1, 3, 4, 22, 4, 3, 10, 22, 10, 9, 7, 16, 7, 8, 12, 17, 12).Rows(),
            new Color(.92f, .8f, .8f),
            "Финал! Найди ключ от шкафа (3-й предохранитель), включи щиток и введи пароль на терминале.",
            floorTheme: "wood", wallTheme: "beige",
            dossier: "Узел управления (428): Последний предохранитель заперт в шкафчике — ключ обронили в юго-восточном углу. После подачи питания терминал откроет комнату Секьюрити.");
        p7.puzzle = 7; p7.needFuses = true; p7.lockerHasFuse = true; p7.needWord = true; p7.wordGroup = "final";

        // ---- Security room (Tunnel Key) ----
        var sec = Add("sec", "Секьюрити — Пост охраны",
            new RB(24, 12, 11).Void(5, 3, 2, 6).Void(17, 3, 2, 6)
                .Put('l', 0, 0, 1, 0, 22, 0, 23, 0).Put('D', 9, 2, 14, 2).Put('i', 9, 3, 14, 3).Put('I', 12, 7)
                .Put('M', 11, 5).Put('K', 11, 9)
                .Put('Z', 11, 2, 3, 2, 20, 2, 11, 7, 3, 10, 20, 10, 8, 5, 15, 5, 8, 8, 15, 8).Rows(),
            new Color(.78f, .86f, .94f), "Пост охраны! Прорвись за золотым ключом от туннеля и уходи!",
            floorTheme: "conc", wallTheme: "conc",
            dossier: "Журнал охраны: Ключ от подземного туннеля лежит на стойке в глубине поста. Беги к выходу на 0 этаже (TB, внизу слева).");

        // ---- Infested optional offices (high risk, ammo & medkits) ----
        string[] ik = { "inf1", "inf2", "inf3" };
        string[] it = { "Каб. 305 — ОПАСНО", "Каб. 162 — ОПАСНО", "Каб. 25 — ОПАСНО" };
        for (int i = 0; i < 3; i++)
        {
            var a = Add(ik[i], it[i],
                new RB(22, 12, 10).Void(5, 3, 2, 2).Void(10, 5, 2, 2).Void(15, 3, 2, 2).Void(8, 8, 2, 2).Void(16, 8, 2, 2)
                    .Put('D', 1, 0, 20, 0).Put('H', 0, 0, 21, 0).Put('I', 4, 6, 17, 6).Put('v', 0, 11)
                    .Put('M', 2, 1).Put('A', 20, 11).Put('m', 1, 11).Rows(),
                new Color(.88f, .72f, .72f), "Заброшенный кабинет полон заражённых. Зато здесь остались патроны и аптечки.",
                floorTheme: "lino", wallTheme: "beige", isClassroom: true,
                dossier: "Остатки баррикад: студенты пытались держать оборону в этом кабинете, прежде чем ушли в бункер.");
            a.randZ = 9; a.randAmmo = 3; a.randMed = 1; a.seed = 100 + i; a.sight = 7f;
        }

        foreach (var a in d.Values)
        {
            a.portals['U'] = "hub";
            if (a.key == "hub") a.portals.Remove('U');
        }
    }

    static string[] HubRows()
    {
        int W = 48, H = 30;
        var g = new char[H][];
        for (int r = 0; r < H; r++) { g[r] = new char[W]; for (int c = 0; c < W; c++) g[r][c] = ' '; }
        for (int r = 2; r <= 27; r++) for (int c = 2; c <= 45; c++) g[r][c] = '.';
        for (int r = 6; r <= 23; r++) for (int c = 6; c <= 41; c++) g[r][c] = ' ';

        // Portals along the 4 wings
        g[10][1] = '1'; g[10][46] = '2'; g[1][22] = '3'; g[6][24] = '4'; g[28][30] = '5';
        g[20][1] = '6'; g[20][46] = '7'; g[1][36] = 'S'; g[28][3] = 'X'; g[28][10] = 'B';
        g[15][1] = 'a'; g[15][46] = 'b'; g[6][15] = 'c';

        // Abandoned university corridor furniture (never blocking 4-tile-wide corridors or portals)
        g[4][2] = 'l'; g[5][2] = 'l'; g[13][2] = 'l'; g[22][2] = 'l'; g[23][2] = 'l';
        g[4][45] = 'l'; g[5][45] = 'l'; g[13][45] = 'l'; g[22][45] = 'l'; g[23][45] = 'l';
        g[2][8] = 'D'; g[2][15] = 'D'; g[2][29] = 'I';
        g[27][18] = 'D'; g[27][25] = 'I'; g[27][36] = 'D';
        g[4][4] = 'v'; g[4][43] = 'v'; g[25][4] = 'v'; g[25][43] = 'v'; g[5][20] = 'v'; g[5][28] = 'v'; g[24][16] = 'v';
        g[24][10] = 'W'; // KBTU map board directly opposite Bunker exit
        g[26][13] = 'M'; // Briefing note outside bunker

        var o = new string[H];
        for (int i = 0; i < H; i++) o[i] = new string(g[i]);
        return o;
    }

    static bool HasChar(AreaDef a, char ch)
    {
        for (int r = 0; r < a.H; r++) for (int c = 0; c < a.W; c++) if (a.map[r][c] == ch) return true;
        return false;
    }

    static int CountChar(AreaDef a, char ch)
    {
        int n = 0;
        for (int r = 0; r < a.H; r++) for (int c = 0; c < a.W; c++) if (a.map[r][c] == ch) n++;
        return n;
    }

    /// <summary>Editor/CI sanity check: every object reachable around solids AND every puzzle has all required objects.</summary>
    public static string Validate()
    {
        Init();
        var sb = new StringBuilder();
        foreach (var a in d.Values)
        {
            int sx = -1, sy = -1;
            for (int r = 0; r < a.H && sx < 0; r++) for (int c = 0; c < a.W; c++)
                    if (a.map[r][c] == 'U' || a.map[r][c] == 'P') { sx = c; sy = r; break; }
            if (a.key == "hub") { sx = 10; sy = 27; }
            if (sx < 0) { sb.AppendLine(a.key + ": no start"); continue; }

            var seen = new bool[a.W, a.H];
            var q = new Queue<Vector2Int>();
            q.Enqueue(new Vector2Int(sx, sy));
            seen[sx, sy] = true;
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
                    if (SolidChars.IndexOf(ch) >= 0) continue; // reachable, don't walk through
                    if (a.portals.ContainsKey(ch)) continue;
                    q.Enqueue(new Vector2Int(x, y));
                }
            }

            for (int r = 0; r < a.H; r++) for (int c = 0; c < a.W; c++)
                    if (a.map[r][c] != ' ' && !seen[c, r])
                        sb.AppendLine($"{a.key}: unreachable '{a.map[r][c]}' at {c},{r}");

            // Puzzle integrity checks
            if (a.needKeyCard && (!HasChar(a, 'y') || !HasChar(a, 'L') || !HasChar(a, 'R')))
                sb.AppendLine(a.key + ": needKeyCard requires y, L, R");
            if (a.needWord && (!HasChar(a, 'V') || !HasChar(a, 'q')))
                sb.AppendLine(a.key + ": needWord requires V and q");
            if (a.needSafe && (!HasChar(a, 'Q') || !HasChar(a, 'q') || !HasChar(a, 'w') || !HasChar(a, 'e')))
                sb.AppendLine(a.key + ": needSafe requires Q and q,w,e");
            if (a.needFuses && (!HasChar(a, 'f') || !HasChar(a, 'u')))
                sb.AppendLine(a.key + ": needFuses requires f and u");
            if (a.lockerHasFuse && (!HasChar(a, 'y') || !HasChar(a, 'L')))
                sb.AppendLine(a.key + ": lockerHasFuse requires y and L");
            if (a.seqLen > 0 && (!HasChar(a, 'n') || !HasChar(a, 'r') || !HasChar(a, 'g') || !HasChar(a, 't') || !HasChar(a, 'j')))
                sb.AppendLine(a.key + ": seqLen requires n and r,g,t,j");
            if (a.needCrates)
            {
                int cc = CountChar(a, 'c'), pc = CountChar(a, 'p');
                if (cc == 0 || cc != pc) sb.AppendLine($"{a.key}: crate/plate mismatch ({cc} vs {pc})");
                for (int r = 0; r < a.H; r++) for (int c = 0; c < a.W; c++)
                        if (a.map[r][c] == 'c')
                        {
                            bool open = true;
                            for (int k = 0; k < 4; k++)
                            {
                                char n = a.map[r + dy[k]][c + dx[k]];
                                if (n == ' ' || SolidChars.IndexOf(n) >= 0) open = false;
                            }
                            if (!open) sb.AppendLine($"{a.key}: crate at {c},{r} needs open 4-way clearance");
                        }
            }
            if (a.key == "sec" && !HasChar(a, 'K')) sb.AppendLine("sec: missing K");
        }
        return sb.Length == 0 ? "OK" : sb.ToString();
    }
}
