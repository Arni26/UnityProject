using System.Collections.Generic;
using UnityEngine;

public enum GState { Intro, Play, Dead, Keypad, Win }

public class PState { public int[] code; public int[] seq; public bool[] known = new bool[4]; public bool done; }

public class Game : MonoBehaviour
{
    public static Game I;
    public const int LWall = 8;

    // ---- player state ----
    public GState state = GState.Intro;
    public Player player; public Camera cam;
    public float hp = 100, maxHp = 100, hurtFlash;
    public int completed, cur, ammo, grenades, medkits;
    public bool[] hasW = new bool[4];
    public bool dash, armor, heal, hasKey;
    public string prompt = "";
    public readonly HashSet<string> taken = new HashSet<string>();
    readonly Dictionary<string, PState> pst = new Dictionary<string, PState>();
    System.Random rng;

    // ---- area runtime ----
    public AreaDef def; Transform root; string lastFrom;
    public bool[,] pass; int[,] flow; float flowT;
    public readonly List<Interactable> inters = new List<Interactable>();
    readonly List<(Vector2 p, string t)> labels = new List<(Vector2, string)>();
    readonly List<GameObject> crates = new List<GameObject>();
    readonly List<Vector2> plates = new List<Vector2>();
    PState ps; bool needSeq, needCode, needCrates, needFuses, seqDone, codeDone, cratesDone, fusesDone;
    int seqProg, fusesHeld, fusesIn, fuseTotal, platesOn; float seqShowUntil;
    float msgUntil, deadT; string msgText = ""; string keypadText = ""; string keypadErr = "";
    int introPage, survivorLine; float portalLock;

    static readonly string[] IntroText = {
        "НАЧАЛСЯ АПОКАЛИПСИС.",
        "Всё случилось внутри университета КБТУ. Все, кто успел, спрятались в сером бункере (−1 этаж).",
        "Но некоторые вышли из бункера...\nи не вернулись.",
        "Сработала сигнализация. Здание полностью закрыто, кроме одного выхода — туннеля, связанного с бункером. Для него нужен ключ из секьюрити.",
        "Известно: 1–2 этажи AB (Абылай хана) и PF (Панфилова) и 4 этаж захвачены. 2 этаж TB (Толе би) — тоже.",
        "Чтобы добраться до ключа, нужно пройти 7 головоломок. С каждой — новое оружие, лечение или навык.\nУмрёшь от укуса — вернёшься на предыдущий этап.",
        "Ты берёшь только дубинку и выходишь из бункера.\n\nWASD — движение   Мышь — прицел   ЛКМ — атака   E — действие\n1-4 / колесо — оружие   G — граната   H — аптечка   Shift — рывок   R — сброс комнаты"
    };
    static readonly string[] SurvivorLines = {
        "Не ходи туда. Те, кто выходил, не вернулись...",
        "Каждый этаж — зомби. Ищи оружие в кабинетах.",
        "Ключ от туннеля лежит в секьюрити. Без него — никак.",
        "Отдохни на диване, если совсем плохо."
    };
    static readonly string[] Rewards = { "", "ПИСТОЛЕТ (клавиша 2) + патроны!", "НАВЫК: ЛЕЧЕНИЕ — клавиша H, +2 аптечки!", "ДРОБОВИК (клавиша 3) + патроны!",
        "НАВЫК: РЫВОК — Shift!", "АВТОМАТ (клавиша 4) + патроны!", "НАВЫК: БРОНЯ — урон вдвое меньше!", "ГРАНАТЫ (клавиша G) + патроны!" };

    // ================= bootstrap =================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        new GameObject("Game").AddComponent<Game>();
    }

    void Awake()
    {
        I = this;
        Sheets.Load();
        Physics2D.queriesHitTriggers = false;
        Physics2D.queriesStartInColliders = false;
        Application.targetFrameRate = 60;

        cam = Camera.main;
        if (cam == null) { var cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); }
        cam.orthographic = true; cam.orthographicSize = 6.5f; cam.backgroundColor = Color.black;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.transform.position = new Vector3(0, 0, -10);

        var pg = new GameObject("Player"); pg.layer = 0;
        player = pg.AddComponent<Player>();
        NewRun(true);
    }

    void NewRun(bool intro)
    {
        rng = new System.Random();
        pst.Clear(); taken.Clear();
        hp = maxHp; completed = 0; cur = 0; ammo = 0; grenades = 0; medkits = 0;
        hasW = new bool[4]; hasW[0] = true; dash = armor = heal = hasKey = false;
        state = intro ? GState.Intro : GState.Play; introPage = 0;
        Enter("bunker", null);
        if (!intro) Msg("Заново. Выйди из бункера.", 4);
    }

    public void Msg(string t, float dur = 3f) { msgText = t; msgUntil = Time.time + dur; }

    // ================= world building =================
    Vector2 P(int c, int r) { return new Vector2(c, def.H - 1 - r); }
    char At(int c, int r) { return (c < 0 || r < 0 || c >= def.W || r >= def.H) ? ' ' : def.map[r][c]; }

    GameObject Spawn(string spr, Vector2 pos, int order, bool solid = false, Color? col = null)
    {
        var g = new GameObject(spr); g.transform.SetParent(root); g.transform.position = pos;
        var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = Sprites.Get(spr); sr.sortingOrder = order; sr.color = col ?? def.tint;
        if (solid) { var bc = g.AddComponent<BoxCollider2D>(); bc.size = new Vector2(0.9f, 0.9f); }
        return g;
    }

    Interactable Inter(GameObject g, string prompt, System.Action use)
    {
        var it = g.AddComponent<Interactable>(); it.prompt = prompt; it.use = use; inters.Add(it); return it;
    }

    PState GetPS(AreaDef a)
    {
        if (pst.TryGetValue(a.key, out var s)) return s;
        s = new PState();
        if (a.codeLen > 0) { s.code = new int[a.codeLen]; for (int i = 0; i < a.codeLen; i++) s.code[i] = rng.Next(0, 10); }
        if (a.seqLen > 0)
        {
            s.seq = new int[a.seqLen];
            if (a.seqLen <= 4)
            {
                var l = new List<int>(); for (int i = 0; i < a.seqLen; i++) l.Add(i);
                for (int i = 0; i < a.seqLen; i++) { int k = rng.Next(l.Count); s.seq[i] = l[k]; l.RemoveAt(k); }
            }
            else for (int i = 0; i < a.seqLen; i++) s.seq[i] = rng.Next(0, 4);
        }
        pst[a.key] = s; return s;
    }

    Vector2 FindSpawn(string from)
    {
        for (int r = 0; r < def.H; r++) for (int c = 0; c < def.W; c++)
            {
                char ch = At(c, r);
                bool match = from == null ? ch == 'P' : (def.portals.TryGetValue(ch, out var t) && t == from);
                if (!match) continue;
                if (from == null) return P(c, r);
                int[] dx = { 0, 0, 1, -1 }, dy = { 1, -1, 0, 0 };
                for (int k = 0; k < 4; k++)
                {
                    char n = At(c + dx[k], r + dy[k]);
                    if (n != ' ' && !def.portals.ContainsKey(n)) return P(c + dx[k], r + dy[k]);
                }
            }
        return new Vector2(def.W / 2f, def.H / 2f);
    }

    public void Enter(string key, string from)
    {
        if (root != null) Destroy(root.gameObject);
        def = Areas.Get(key); lastFrom = from;
        root = new GameObject("Area_" + key).transform;
        inters.Clear(); labels.Clear(); crates.Clear(); plates.Clear();
        pass = new bool[def.W, def.H]; flow = null; flowT = 0;
        ps = def.puzzle > 0 ? GetPS(def) : null;
        bool active = ps != null && !ps.done;
        needSeq = active && def.seqLen > 0; needCode = active && def.codeLen > 0;
        needCrates = active && def.crates; needFuses = active && def.fuses;
        seqDone = codeDone = cratesDone = fusesDone = false; seqProg = 0; fusesHeld = 0; fusesIn = 0; fuseTotal = 0; seqShowUntil = 0; platesOn = 0;
        keypadText = ""; keypadErr = "";
        var floorTiles = new List<Vector2Int>();
        string[] colNames = { "Красный", "Зелёный", "Синий", "Жёлтый" };

        for (int r = 0; r < def.H; r++)
            for (int c = 0; c < def.W; c++)
            {
                char ch = def.map[r][c]; Vector2 pos = P(c, r); int cc = c, rr = r;
                if (ch == ' ')
                {
                    bool near = false;
                    for (int j = -1; j <= 1 && !near; j++) for (int i = -1; i <= 1; i++) if (At(c + i, r + j) != ' ') { near = true; break; }
                    if (near)
                    {
                        var w = Spawn("wall", pos, 2, false, def.tint * 0.9f); w.layer = LWall;
                        var bc = w.AddComponent<BoxCollider2D>(); bc.size = Vector2.one;
                    }
                    continue;
                }
                if (def.portals.TryGetValue(ch, out var target)) { MakePortal(ch, target, pos); continue; }

                pass[c, r] = true;
                Spawn(((c * 7 + r * 13) % 2 == 0) ? "floor0" : "floor1", pos, 0);
                if (ch == '.') floorTiles.Add(new Vector2Int(c, r));
                string id = def.key + ":" + c + "," + r;
                int ci;

                switch (ch)
                {
                    case 'Z':
                        if (!(def.key == "hub" && taken.Contains(id))) SpawnZombie(pos, id);
                        break;
                    case 'm': case 'a': case 'K':
                        if (!taken.Contains(id) && !(ch == 'K' && hasKey)) MakePickup(ch, pos, id);
                        break;
                    case 'c': if (needCrates) { MakeCrate(pos); } break;
                    case 'p': if (needCrates) { Spawn("plate", pos, 1); plates.Add(pos); } break;
                    case 'r': case 'g': case 'b': case 'y':
                        if (needSeq)
                        {
                            ci = "rgby".IndexOf(ch);
                            var sw = Spawn("btn", pos, 4, false, ColorOf(ci));
                            Inter(sw, "нажать рычаг: " + colNames[ci], () => SeqPress(ci));
                        }
                        break;
                    case 'n':
                        if (needSeq)
                        {
                            var cn = Spawn("console", pos, 4, true);
                            Inter(cn, "изучить консоль", () =>
                            {
                                seqShowUntil = Time.time + (def.flash ? 5f : 20f);
                                Msg(def.flash ? "Запоминай порядок!" : "Порядок рычагов на экране.", 3f);
                            });
                        }
                        break;
                    case 'q': case 'w': case 'e': case 'o':
                        ci = "qweo".IndexOf(ch);
                        if (needCode && ci < def.codeLen)
                        {
                            var nt = Spawn("note", pos, 4);
                            Inter(nt, "прочитать записку", () =>
                            {
                                player.Play(1, 0.7f); ps.known[ci] = true;
                                Msg("Записка: цифра №" + (ci + 1) + " кода = " + ps.code[ci], 4f);
                            });
                        }
                        break;
                    case 'k':
                        if (needCode)
                        {
                            var kp = Spawn("keypad", pos, 4, true);
                            Inter(kp, "ввести код", () => { state = GState.Keypad; keypadText = ""; keypadErr = ""; });
                        }
                        break;
                    case 'f':
                        if (needFuses)
                        {
                            fuseTotal++;
                            var fu = Spawn("fuse", pos, 4);
                            Inter(fu, "взять предохранитель", () => { player.Play(1, 0.7f); fusesHeld++; Msg("Предохранитель взят (" + (fusesIn + fusesHeld) + "/" + fuseTotal + ")", 2f); Destroy(fu); });
                        }
                        break;
                    case 'u':
                        if (needFuses)
                        {
                            var fb = Spawn("fusebox", pos, 4, true);
                            Inter(fb, "вставить предохранители", () =>
                            {
                                if (fusesHeld == 0) { Msg("Нужны предохранители: " + fusesIn + "/" + fuseTotal, 2.5f); return; }
                                fusesIn += fusesHeld; fusesHeld = 0;
                                if (fusesIn >= fuseTotal) { fusesDone = true; Msg("Щиток заработал!", 3f); CheckComplete(); }
                                else Msg("Вставлено " + fusesIn + "/" + fuseTotal, 2.5f);
                            });
                        }
                        break;
                    case 'h':
                        {
                            var sh = Spawn("shelf", pos, 4, true);
                            Inter(sh, "осмотреть консервы", () =>
                            {
                                if (taken.Contains(id)) { Msg("Пусто. Остались пустые банки.", 2f); return; }
                                taken.Add(id); hp = Mathf.Min(maxHp, hp + 15); Msg("Ты съел банку тушёнки. +15 HP", 2.5f);
                            });
                        }
                        break;
                    case 's':
                        {
                            var so = Spawn("sofa", pos, 4, true);
                            Inter(so, "отдохнуть на диване", () => { hp = maxHp; Msg("Ты отдохнул. HP восстановлено.", 2.5f); });
                        }
                        break;
                    case 'N':
                        {
                            var nn = Spawn("player", pos, 5, true, new Color(1f, .7f, .4f));
                            Inter(nn, "поговорить", () => { Msg("Выживший: «" + SurvivorLines[survivorLine++ % SurvivorLines.Length] + "»", 4f); });
                        }
                        break;
                }
            }

        // random zombies / loot (hub + infested rooms), deterministic so killed ones stay dead
        if (def.randZ + def.randAmmo + def.randMed > 0)
        {
            var rr2 = new System.Random(def.seed);
            for (int i = floorTiles.Count - 1; i > 0; i--) { int j = rr2.Next(i + 1); var t = floorTiles[i]; floorTiles[i] = floorTiles[j]; floorTiles[j] = t; }
            var bunker = new Vector2(-100, -100);
            var pPos = new List<Vector2>();
            for (int r = 0; r < def.H; r++) for (int c = 0; c < def.W; c++)
                    if (def.portals.ContainsKey(def.map[r][c])) { pPos.Add(P(c, r)); if (def.portals[def.map[r][c]] == "bunker") bunker = P(c, r); }
            var used = new List<Vector2>(); int idx = 0, z = 0, am = 0, md = 0;
            foreach (var t in floorTiles)
            {
                Vector2 pos = P(t.x, t.y); bool ok = true;
                foreach (var q in pPos) if (Vector2.Distance(q, pos) < 3f) { ok = false; break; }
                if (!ok || Vector2.Distance(bunker, pos) < 9f) continue;
                foreach (var q in used) if (Vector2.Distance(q, pos) < 2.5f) { ok = false; break; }
                if (!ok) continue;
                used.Add(pos); idx++;
                string id = def.key + ":r" + idx;
                if (z < def.randZ) { z++; if (!(def.key == "hub" && taken.Contains(id))) SpawnZombie(pos, id); }
                else if (am < def.randAmmo) { am++; if (!taken.Contains(id)) MakePickup('a', pos, id); }
                else if (md < def.randMed) { md++; if (!taken.Contains(id)) MakePickup('m', pos, id); }
                else break;
            }
        }

        foreach (var l in def.labels) labels.Add((new Vector2(l.c, def.H - 1 - l.r), l.t));
        player.transform.position = FindSpawn(from);
        player.rb.linearVelocity = Vector2.zero;
        cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10);
        portalLock = Time.time + 0.4f;
        if (state == GState.Play) Msg(def.title + (string.IsNullOrEmpty(def.hint) ? "" : "\n" + def.hint), 5f);
    }

    static Color ColorOf(int i)
    {
        switch (i) { case 0: return new Color(.95f, .2f, .2f); case 1: return new Color(.2f, .9f, .3f); case 2: return new Color(.3f, .5f, 1f); default: return new Color(1f, .9f, .2f); }
    }

    void MakePortal(char ch, string target, Vector2 pos)
    {
        string reason; bool ok = CanEnter(target, out reason);
        var g = Spawn("door", pos, 2, false, ok ? Color.white : new Color(.9f, .4f, .4f));
        if (ok)
        {
            var bc = g.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = new Vector2(0.9f, 0.9f);
            g.AddComponent<Portal>().target = target;
        }
        else
        {
            var bc = g.AddComponent<BoxCollider2D>(); bc.size = new Vector2(0.9f, 0.9f);
            Inter(g, "закрыто", () => Msg(reason, 3.5f));
        }
        labels.Add((pos + new Vector2(0, 1.1f), Areas.TitleOf(target) + (ok ? "" : "  [закрыто]")));
    }

    bool CanEnter(string t, out string reason)
    {
        reason = "";
        if (t.Length == 2 && t[0] == 'p' && char.IsDigit(t[1]))
        {
            int n = t[1] - '0';
            if (n > completed + 1) { reason = "Сначала пройди предыдущий этап."; return false; }
        }
        if (t == "sec" && completed < 7) { reason = "Секьюрити заперта: реши все 7 головоломок."; return false; }
        if (t == "tunnel" && !hasKey) { reason = "Туннель заперт. Нужен ключ из секьюрити."; return false; }
        return true;
    }

    public void TryGo(string target)
    {
        if (Time.time < portalLock || state != GState.Play) return;
        string reason; if (!CanEnter(target, out reason)) { Msg(reason, 3f); return; }
        if (target == "tunnel") { state = GState.Win; return; }
        Enter(target, def.key);
    }

    void SpawnZombie(Vector2 pos, string id)
    {
        var g = new GameObject("zombie"); g.transform.SetParent(root); g.transform.position = pos; g.layer = 9;
        var z = g.AddComponent<Zombie>(); z.id = id; z.sight = def.sight;
    }

    public void OnZombieDeath(Zombie z)
    {
        if (def.key == "hub") taken.Add(z.id);
        if (z.frames != null)
        {
            var c = new GameObject("corpse"); c.transform.SetParent(root); c.transform.position = z.transform.position + new Vector3(0, -0.32f, 0);
            var sr = c.AddComponent<SpriteRenderer>(); sr.sprite = z.frames[6]; sr.sortingOrder = 3; sr.color = def.tint;
            c.AddComponent<Corpse>().dead = z.frames[7];
        }
        if (Random.value < 0.25f) MakePickup('a', z.transform.position, null);
    }

    void MakePickup(char kind, Vector2 pos, string id)
    {
        string spr = kind == 'm' ? "medkit" : (kind == 'a' ? "ammo" : "key");
        var g = Spawn(spr, pos, 4, false, Color.white);
        var bc = g.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = new Vector2(0.8f, 0.8f);
        var p = g.AddComponent<Pickup>(); p.kind = kind; p.id = id;
    }

    public void Collect(char kind, string id)
    {
        if (id != null) taken.Add(id);
        player.Play(1, 0.7f);
        if (kind == 'm') { medkits++; Msg("Аптечка +1" + (heal ? "" : " (лечение откроется после этапа 2)"), 2.5f); }
        else if (kind == 'a') { ammo += 8; Msg("Патроны +8" + (hasW[1] ? "" : " (оружия пока нет)"), 2f); }
        else { hasKey = true; Msg("КЛЮЧ ОТ ТУННЕЛЯ! Беги к выходу на 0 этаже (внизу слева)!", 6f); }
    }

    void MakeCrate(Vector2 pos)
    {
        var g = Spawn("crate", pos, 4, true);
        g.GetComponent<BoxCollider2D>().size = new Vector2(0.94f, 0.94f);
        var rb = g.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.freezeRotation = true; rb.mass = 3; rb.linearDamping = 14f;
        g.layer = 10; crates.Add(g);
    }

    // ================= puzzle logic =================
    void SeqPress(int color)
    {
        if (seqDone) return;
        if (ps.seq[seqProg] == color) { seqProg++; Msg("Щёлк... " + seqProg + "/" + ps.seq.Length, 1.5f); }
        else { seqProg = 0; Msg("Неверный порядок! Сброс.", 2.5f); }
        if (seqProg >= ps.seq.Length) { seqDone = true; Msg("Рычаги активированы!", 3f); CheckComplete(); }
    }

    void SubmitCode()
    {
        string want = ""; foreach (var d in ps.code) want += d;
        if (keypadText == want) { codeDone = true; state = GState.Play; Msg("Замок открыт!", 3f); CheckComplete(); }
        else { keypadErr = "Неверный код"; keypadText = ""; }
    }

    void CheckComplete()
    {
        if (needSeq && !seqDone) return; if (needCode && !codeDone) return;
        if (needCrates && !cratesDone) return; if (needFuses && !fusesDone) return;
        if (ps == null || ps.done) return;
        ps.done = true; int n = def.puzzle; completed = Mathf.Max(completed, n);
        player.Play(3, 1.8f);
        switch (n)
        {
            case 1: hasW[1] = true; ammo += 12; cur = 1; break;
            case 2: heal = true; medkits += 2; break;
            case 3: hasW[2] = true; ammo += 8; cur = 2; break;
            case 4: dash = true; break;
            case 5: hasW[3] = true; ammo += 30; cur = 3; break;
            case 6: armor = true; break;
            case 7: grenades += 4; ammo += 10; break;
        }
        Msg("ЭТАП " + n + "/7 ПРОЙДЕН!\nНаграда: " + Rewards[n] + (n == 7 ? "\nСекьюрити открыта!" : ""), 7f);
    }

    public void UseMedkit()
    {
        if (!heal) { Msg("Навык лечения откроется после этапа 2.", 2f); return; }
        if (medkits <= 0) { Msg("Нет аптечек.", 1.5f); return; }
        if (hp >= maxHp) return;
        medkits--; hp = Mathf.Min(maxHp, hp + 40); Msg("+40 HP", 1.5f);
    }

    // ================= death / restart =================
    public void Die()
    {
        if (state != GState.Play) return;
        state = GState.Dead; deadT = 2.8f; player.rb.linearVelocity = Vector2.zero;
        player.Play(4, 2.6f, 2, 7);
    }

    void Respawn()
    {
        hp = maxHp; state = GState.Play;
        if (completed == 0) Enter("bunker", null); else Enter("hub", "p" + completed);
        Msg("Тебя укусили... Ты очнулся на предыдущем этапе.", 4f);
    }

    // ================= pathfinding =================
    Vector2Int TileOf(Vector2 p) { return new Vector2Int(Mathf.RoundToInt(p.x), def.H - 1 - Mathf.RoundToInt(p.y)); }

    void RecomputeFlow()
    {
        if (flow == null) flow = new int[def.W, def.H];
        for (int x = 0; x < def.W; x++) for (int y = 0; y < def.H; y++) flow[x, y] = -1;
        var t = TileOf(player.transform.position);
        if (t.x < 0 || t.y < 0 || t.x >= def.W || t.y >= def.H) return;
        var q = new Queue<Vector2Int>(); flow[t.x, t.y] = 0; q.Enqueue(t);
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int x = p.x + dx[k], y = p.y + dy[k];
                if (x < 0 || y < 0 || x >= def.W || y >= def.H || flow[x, y] >= 0 || !pass[x, y]) continue;
                flow[x, y] = flow[p.x, p.y] + 1; q.Enqueue(new Vector2Int(x, y));
            }
        }
    }

    public bool NextStep(Vector2 pos, out Vector2 target)
    {
        target = pos;
        if (flow == null) return false;
        var t = TileOf(pos);
        if (t.x < 0 || t.y < 0 || t.x >= def.W || t.y >= def.H || flow[t.x, t.y] < 0) return false;
        int best = flow[t.x, t.y]; Vector2Int bt = t; bool found = false;
        for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++)
            {
                if (i == 0 && j == 0) continue;
                int x = t.x + i, y = t.y + j;
                if (x < 0 || y < 0 || x >= def.W || y >= def.H || flow[x, y] < 0) continue;
                if (i != 0 && j != 0 && (!pass[t.x + i, t.y] || !pass[t.x, t.y + j])) continue;
                int v = flow[x, y] * 2 + (i != 0 && j != 0 ? 0 : 1) - (i != 0 && j != 0 ? 1 : 0);
                if (flow[x, y] < best || (flow[x, y] == best && !found && false)) { best = flow[x, y]; bt = new Vector2Int(x, y); found = true; }
            }
        if (!found) return false;
        target = P(bt.x, bt.y); return true;
    }

    // ================= update =================
    void Update()
    {
        if (hurtFlash > 0) hurtFlash -= Time.deltaTime;
        switch (state)
        {
            case GState.Intro:
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                {
                    introPage++;
                    if (introPage >= IntroText.Length) { state = GState.Play; Msg(def.title + "\n" + def.hint, 6f); }
                }
                break;
            case GState.Dead: deadT -= Time.deltaTime; if (deadT <= 0) Respawn(); break;
            case GState.Keypad: if (Input.GetKeyDown(KeyCode.Escape)) state = GState.Play; break;
            case GState.Win: if (Input.GetKeyDown(KeyCode.Return)) NewRun(false); break;
            case GState.Play:
                flowT -= Time.deltaTime;
                if (flowT <= 0) { flowT = 0.25f; RecomputeFlow(); }
                if (needCrates && !cratesDone && plates.Count > 0)
                {
                    int on = 0;
                    foreach (var p in plates) foreach (var c in crates) if (c != null && Vector2.Distance(p, c.transform.position) < 0.45f) { on++; break; }
                    platesOn = on;
                    if (on >= plates.Count) { cratesDone = true; Msg("Ящики на местах!", 3f); CheckComplete(); }
                }
                if (Input.GetKeyDown(KeyCode.R) && def.puzzle > 0) { Enter(def.key, lastFrom); Msg("Комната сброшена.", 2f); }
                break;
        }
    }

    void LateUpdate()
    {
        if (player == null || cam == null) return;
        var t = player.transform.position;
        var target = new Vector3(t.x, t.y, -10);
        cam.transform.position = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
    }

    // ================= GUI =================
    GUIStyle sTxt, sBig; Texture2D white, vign;

    void InitGUI()
    {
        white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
        int n = 128; vign = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                vign.SetPixel(x, y, new Color(0, 0, 0, Mathf.SmoothStep(0.3f, 1.1f, d) * 0.92f));
            }
        vign.Apply();
        sTxt = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, richText = true };
        sBig = new GUIStyle(sTxt) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
    }

    void Txt(Rect r, string t, GUIStyle st, Color c, TextAnchor a = TextAnchor.UpperLeft)
    {
        var old = st.alignment; st.alignment = a;
        GUI.color = new Color(0, 0, 0, 0.9f); GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), t, st);
        GUI.color = c; GUI.Label(r, t, st); GUI.color = Color.white; st.alignment = old;
    }

    void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, white); GUI.color = Color.white; }

    void OnGUI()
    {
        if (sTxt == null) InitGUI();
        float sw = Screen.width, sh = Screen.height, s = sh / 720f, vw = sw / s;
        var full = new Rect(0, 0, sw, sh);

        if (state != GState.Intro && state != GState.Win)
        {
            GUI.color = Color.white; GUI.DrawTexture(full, vign);
            if (def != null && def.alarm) Fill(full, new Color(1, 0, 0, (Mathf.Sin(Time.time * 3f) * .5f + .5f) * 0.10f));
            if (hurtFlash > 0) Fill(full, new Color(.8f, 0, 0, Mathf.Clamp01(hurtFlash) * 0.7f));
        }

        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        Color W = Color.white;

        if (state == GState.Intro)
        {
            Fill(new Rect(0, 0, vw, 720), Color.black);
            Txt(new Rect(120, 160, vw - 240, 400), IntroText[Mathf.Min(introPage, IntroText.Length - 1)], introPage == 0 ? sBig : sTxt, W, TextAnchor.MiddleCenter);
            Txt(new Rect(0, 660, vw, 40), "Пробел / клик — дальше", sTxt, new Color(.6f, .6f, .6f), TextAnchor.MiddleCenter);
            return;
        }
        if (state == GState.Win)
        {
            Fill(new Rect(0, 0, vw, 720), Color.black);
            Txt(new Rect(120, 150, vw - 240, 420),
                "ТЫ ВЫБРАЛСЯ!\n\nКлюч повернулся в замке, и тяжёлая дверь туннеля открылась. Ты вернулся в бункер — к тем, кто ждал.\nНо апокалипсис ещё не закончился...\n\nEnter — начать заново", sBig, W, TextAnchor.MiddleCenter);
            return;
        }

        // HUD
        Fill(new Rect(16, 16, 264, 24), new Color(0, 0, 0, .6f));
        Fill(new Rect(18, 18, 260 * Mathf.Clamp01(hp / maxHp), 20), new Color(.8f, .1f, .1f));
        Txt(new Rect(22, 14, 260, 26), "HP " + Mathf.CeilToInt(Mathf.Max(0, hp)), sTxt, W);
        string w = Player.WNames[cur] + (cur > 0 ? "  [" + ammo + "]" : "");
        string extra = (grenades > 0 ? "   Гранаты: " + grenades : "") + (heal ? "   Аптечки: " + medkits : "") + (hasKey ? "   КЛЮЧ" : "");
        Txt(new Rect(16, 46, 600, 30), w + extra, sTxt, new Color(1, .9f, .6f));

        string obj = hasKey ? "Ключ у тебя! Беги к туннельному выходу (0 этаж, внизу слева)."
            : completed >= 7 ? "Все 7 этапов пройдены! Забери ключ в секьюрити (0 этаж, вверху справа)."
            : "Этап " + (completed + 1) + "/7: " + Areas.TitleOf("p" + (completed + 1));
        Txt(new Rect(vw - 520, 14, 504, 60), obj, sTxt, new Color(.7f, 1f, .7f), TextAnchor.UpperRight);
        Txt(new Rect(vw / 2 - 250, 8, 500, 28), def.title, sTxt, new Color(.8f, .8f, .8f), TextAnchor.UpperCenter);

        // puzzle status
        float py = 84;
        if (def.puzzle > 0 && ps != null && !ps.done)
        {
            if (needSeq) { Txt(new Rect(16, py, 500, 26), "Рычаги: " + seqProg + "/" + ps.seq.Length, sTxt, W); py += 26; }
            if (needCode)
            {
                string cs = ""; for (int i = 0; i < ps.code.Length; i++) cs += (ps.known[i] ? ps.code[i].ToString() : "?") + " ";
                Txt(new Rect(16, py, 500, 26), "Код: " + cs, sTxt, W); py += 26;
            }
            if (needCrates) { Txt(new Rect(16, py, 500, 26), "Плиты: " + platesOn + "/" + plates.Count, sTxt, W); py += 26; }
            if (needFuses) { Txt(new Rect(16, py, 500, 26), "Предохранители: щиток " + fusesIn + "/" + fuseTotal + (fusesHeld > 0 ? " (в руках " + fusesHeld + ")" : ""), sTxt, W); py += 26; }
        }
        if (needSeq && Time.time < seqShowUntil)
        {
            float x0 = vw / 2 - ps.seq.Length * 30;
            Fill(new Rect(x0 - 12, 70, ps.seq.Length * 60 + 24, 64), new Color(0, 0, 0, .75f));
            for (int i = 0; i < ps.seq.Length; i++) Fill(new Rect(x0 + i * 60 + 4, 78, 48, 48), ColorOf(ps.seq[i]));
        }

        // world labels
        foreach (var l in labels)
        {
            if (Vector2.Distance(l.p, player.transform.position) > 12f) continue;
            Vector3 sp = cam.WorldToScreenPoint(l.p);
            Txt(new Rect(sp.x / s - 150, (sh - sp.y) / s - 12, 300, 26), l.t, sTxt, new Color(1f, .95f, .6f), TextAnchor.MiddleCenter);
        }

        if (!string.IsNullOrEmpty(prompt) && state == GState.Play)
            Txt(new Rect(0, 560, vw, 30), prompt, sTxt, new Color(1f, 1f, .5f), TextAnchor.MiddleCenter);
        if (Time.time < msgUntil && state != GState.Dead)
            Txt(new Rect(vw / 2 - 450, 600, 900, 100), msgText, sTxt, W, TextAnchor.UpperCenter);

        if (state == GState.Dead)
        {
            Fill(new Rect(0, 0, vw, 720), new Color(.3f, 0, 0, .6f));
            Txt(new Rect(0, 280, vw, 120), "ТЕБЯ УКУСИЛИ\nВозврат на предыдущий этап...", sBig, W, TextAnchor.MiddleCenter);
        }

        if (state == GState.Keypad)
        {
            Fill(new Rect(vw / 2 - 190, 220, 380, 220), new Color(0, 0, 0, .9f));
            Txt(new Rect(vw / 2 - 170, 230, 340, 30), "Кодовый замок (" + ps.code.Length + " цифр)", sTxt, W, TextAnchor.UpperCenter);
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { SubmitCode(); e.Use(); }
            else
            {
                GUI.SetNextControlName("kp");
                keypadText = GUI.TextField(new Rect(vw / 2 - 120, 280, 240, 44), keypadText, ps.code.Length, sBig);
                GUI.FocusControl("kp");
            }
            Txt(new Rect(vw / 2 - 170, 340, 340, 30), keypadErr, sTxt, new Color(1, .4f, .4f), TextAnchor.UpperCenter);
            Txt(new Rect(vw / 2 - 170, 390, 340, 30), "Enter — ввести, Esc — отмена", sTxt, new Color(.7f, .7f, .7f), TextAnchor.UpperCenter);
        }
    }
}
