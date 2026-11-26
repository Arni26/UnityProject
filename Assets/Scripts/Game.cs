using System;
using System.Collections.Generic;
using UnityEngine;

public enum GState { Title, Intro, Play, Dead, Word, Safe, Map, Win }

public class PState
{
    public bool done, hasLockerKey, lockerOpened, hasCard;
    public int[] safeTargets;
    public int[] seq;
    public string wordAnswer;
    public string[] wordAccepts;
    public string[] wordClues;
    public bool[] known = new bool[4];
}

public class Game : MonoBehaviour
{
    public static Game I;
    public const int LWall = 8;

    // ---- player state ----
    public GState state = GState.Title;
    public Player player;
    public Camera cam;
    public float hp = 100f, maxHp = 100f, hurtFlash;
    public int completed, cur, ammo, grenades, medkits;
    public readonly bool[] hasW = new bool[4];
    public bool dash, armor, heal, hasKey;
    public string prompt = "";
    public readonly HashSet<string> taken = new HashSet<string>();
    readonly Dictionary<string, PState> pst = new Dictionary<string, PState>();
    System.Random rng;

    // ---- area runtime ----
    public AreaDef def;
    Transform root;
    string lastFrom;
    public bool[,] pass;
    int[,] flow;
    float flowT, shakeAmt;
    public readonly List<Interactable> inters = new List<Interactable>();
    readonly List<(Vector2 p, string t)> labels = new List<(Vector2, string)>();
    readonly List<GameObject> crates = new List<GameObject>();
    readonly List<Vector2> plates = new List<Vector2>();

    PState ps;
    bool cardDone, wordDone, safeDone, fusesDone, cratesDone, seqDone;
    int seqProg, fusesHeld, fusesIn, fuseTotal, platesOn, safeDial, safeStep;
    float seqShowUntil, msgUntil, deadT, modalLock, portalLock;
    string msgText = "", wordInput = "", wordErr = "", safeErr = "";
    int introPage;
    readonly Texture2D[] storyTex = new Texture2D[6];

    static readonly string[] IntroCaptions = {
        "НАЧАЛСЯ АПОКАЛИПСИС.\nВсё случилось внезапно внутри университета КБТУ. Толпы заражённых окружили корпус на Толе би.",
        "Все, кто успел спастись, укрылись в сером подземном бункере (−1 этаж) среди полок с консервами.",
        "Но смельчаки, что вышли из бункера наверх...\nтак и не вернулись.",
        "Сработала аварийная сигнализация. Все выходы КБТУ заблокированы, кроме секретного туннеля — но ключ остался в Секьюрити. 1–2 этажи AB и PF, 2 этаж TB и 4 этаж захвачены.",
        "Ты берёшь одну лишь дубинку и выходишь в тёмный коридор.\nПройди 7 головоломок за новым оружием и навыками. Умрёшь — очнёшься на предыдущем этапе."
    };

    static readonly string[] Rewards = {
        "",
        "ПИСТОЛЕТ (клавиша 2) + 12 патронов!",
        "НАВЫК: ЛЕЧЕНИЕ (клавиша H) + 2 аптечки!",
        "ДРОБОВИК (клавиша 3) + 8 патронов!",
        "НАВЫК: РЫВОК (клавиша Shift)!",
        "АВТОМАТ (клавиша 4) + 30 патронов!",
        "НАВЫК: БРОНЯ (урон от укусов вдвое меньше)!",
        "ГРАНАТЫ (клавиша G) +4 шт. — СЕКЬЮРИТИ ОТКРЫТА!"
    };

    struct WordSpec { public string answer; public string[] accepts, clues; }

    static readonly WordSpec[] PfWords = {
        new WordSpec {
            answer = "ПАНФИЛОВ",
            accepts = new[] { "ПАНФИЛОВ", "ПАНФИЛОВА", "PANFILOV", "PANFILOVA" },
            clues = new[] {
                "Записка 1: Название улицы в честь героя (красный блок КБТУ справа).",
                "Записка 2: Начало слова — ПАН..., всего 8 букв.",
                "Записка 3: Окончание — ...ФИЛОВ."
            }
        },
        new WordSpec {
            answer = "АБЫЛАЙ",
            accepts = new[] { "АБЫЛАЙ", "АБЫЛАЙХАН", "АБЫЛАЙ ХАН", "ABYLAI", "ABYLAIKHAN" },
            clues = new[] {
                "Записка 1: Проспект на жёлтой стороне корпуса КБТУ (слева).",
                "Записка 2: Первые три буквы — АБЫ...",
                "Записка 3: Имя казахского хана — АБЫЛАЙ."
            }
        },
        new WordSpec {
            answer = "КАЗЫБЕК",
            accepts = new[] { "КАЗЫБЕК", "КАЗЫБЕКБИ", "КАЗЫБЕК БИ", "KAZYBEK", "KAZYBEKBI" },
            clues = new[] {
                "Записка 1: Улица с тыльной зелёной стороны КБТУ (сверху, за турникетом).",
                "Записка 2: Первые три буквы — КАЗ...",
                "Записка 3: Имя великого бия — КАЗЫБЕК."
            }
        },
        new WordSpec {
            answer = "ТОЛЕБИ",
            accepts = new[] { "ТОЛЕБИ", "ТОЛЕ БИ", "ТОЛЕ", "TOLEBI", "TOLE BI", "TOLE" },
            clues = new[] {
                "Записка 1: Улица главного фасада КБТУ с колоннами (синий блок снизу).",
                "Записка 2: Начинается на ТОЛ...",
                "Записка 3: Полный пароль — ТОЛЕБИ (можно слитно)."
            }
        }
    };

    static readonly WordSpec FinalWord = new WordSpec
    {
        answer = "КБТУ",
        accepts = new[] { "КБТУ", "KBTU" },
        clues = new[] {
            "Записка 1: Главный код управления — аббревиатура нашего университета.",
            "Записка 2: 4 заглавные буквы: Казахстанско-Британский...",
            "Записка 3: Введите КБТУ (или KBTU)."
        }
    };

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
        Sfx.InitOn(gameObject);
        Physics2D.queriesHitTriggers = false;
        Physics2D.queriesStartInColliders = false;
        Application.targetFrameRate = 60;

        for (int i = 0; i < 5; i++) storyTex[i] = Resources.Load<Texture2D>($"Story/intro_{i}");
        storyTex[5] = Resources.Load<Texture2D>("Story/ending");

        cam = Camera.main;
        if (cam == null) { var cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); }
        cam.orthographic = true;
        cam.orthographicSize = 6.5f;
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.07f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, 0, -10);

        var pg = new GameObject("Player");
        pg.layer = 0;
        player = pg.AddComponent<Player>();
        NewRun(GState.Title);
    }

    void NewRun(GState startState)
    {
        rng = new System.Random();
        pst.Clear();
        taken.Clear();
        hp = maxHp;
        completed = 0; cur = 0; ammo = 0; grenades = 0; medkits = 0;
        Array.Clear(hasW, 0, hasW.Length);
        hasW[0] = true;
        dash = armor = heal = hasKey = false;
        state = startState;
        introPage = 0;
        Enter("bunker", null);
        if (startState == GState.Play) Msg("Найди 7 головоломок по корпусам КБТУ. Выходи в коридор наверху.", 5f);
    }

    public void Msg(string t, float dur = 3.5f) { msgText = t; msgUntil = Time.time + dur; }
    public void Shake(float amt) { shakeAmt = Mathf.Max(shakeAmt, amt); }

    // ================= world building =================
    Vector2 P(int c, int r) => new Vector2(c, def.H - 1 - r);
    char At(int c, int r) => (c < 0 || r < 0 || c >= def.W || r >= def.H) ? ' ' : def.map[r][c];

    GameObject Spawn(string spr, Vector2 pos, int order, bool solid = false, Color? col = null)
    {
        var g = new GameObject(spr);
        g.transform.SetParent(root);
        g.transform.position = pos;
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = Sprites.Get(spr);
        sr.sortingOrder = order;
        sr.color = col ?? def.tint;
        if (solid) { var bc = g.AddComponent<BoxCollider2D>(); bc.size = new Vector2(0.86f, 0.86f); }
        return g;
    }

    Interactable Inter(GameObject g, string pr, Action use)
    {
        var it = g.AddComponent<Interactable>();
        it.prompt = pr;
        it.use = use;
        inters.Add(it);
        return it;
    }

    PState GetPS(AreaDef a)
    {
        if (pst.TryGetValue(a.key, out var s)) return s;
        s = new PState();

        if (a.needSafe)
        {
            s.safeTargets = new[] { rng.Next(4, 19), rng.Next(22, 39), rng.Next(41, 57) };
        }
        if (a.needWord)
        {
            var spec = a.wordGroup == "final" ? FinalWord : PfWords[rng.Next(PfWords.Length)];
            s.wordAnswer = spec.answer;
            s.wordAccepts = spec.accepts;
            s.wordClues = spec.clues;
        }
        if (a.seqLen > 0)
        {
            s.seq = new int[a.seqLen];
            for (int i = 0; i < a.seqLen; i++) s.seq[i] = rng.Next(0, 4);
        }
        pst[a.key] = s;
        return s;
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
                    if (n != ' ' && !def.portals.ContainsKey(n) && Areas.SolidChars.IndexOf(n) < 0)
                        return P(c + dx[k], r + dy[k]);
                }
            }
        return new Vector2(def.W * 0.5f, def.H * 0.5f);
    }

    int WallVariant(int c, int r)
    {
        bool nearPortal = def.portals.ContainsKey(At(c - 1, r)) || def.portals.ContainsKey(At(c + 1, r));
        if (def.wallTheme == "conc")
        {
            if (c % 5 == 2) return 2; // wall lamp box
            return (c * 11 + r * 7) % 3;
        }
        if (nearPortal) return 6; // green EXIT plaque next to doorway
        if (c % 6 == 3) return 7; // fluorescent wall lamp
        if (def.isClassroom && (c + r * 3) % 7 == 0) return 3; // chalkboard
        int[] pool = { 0, 0, 1, 2, 4, 5 };
        return pool[(int)(Sprites.N(c, r, def.seed + 19) * pool.Length) % pool.Length];
    }

    Color TileTint(int c, int r)
    {
        Color t = def.tint;
        if (def.key != "hub") return t;
        if (c < 10) return Color.Lerp(t, new Color(1f, 0.94f, 0.68f), 0.30f);   // AB Yellow
        if (c > 37) return Color.Lerp(t, new Color(1f, 0.72f, 0.72f), 0.28f);   // PF Red
        if (r > 23) return Color.Lerp(t, new Color(0.75f, 0.85f, 1f), 0.28f);   // TB Blue
        if (r < 6) return Color.Lerp(t, new Color(0.78f, 0.96f, 0.82f), 0.26f);  // KB Green
        return t;
    }

    public void Enter(string key, string from)
    {
        if (root != null) Destroy(root.gameObject);
        def = Areas.Get(key);
        lastFrom = from;
        root = new GameObject("Area_" + key).transform;
        inters.Clear(); labels.Clear(); crates.Clear(); plates.Clear();
        pass = new bool[def.W, def.H]; flow = null; flowT = 0f;

        ps = def.puzzle > 0 ? GetPS(def) : null;
        bool active = ps != null && !ps.done;
        cardDone = ps != null && ps.done;
        wordDone = ps != null && ps.done;
        safeDone = ps != null && ps.done;
        fusesDone = ps != null && ps.done;
        cratesDone = ps != null && ps.done;
        seqDone = ps != null && ps.done;
        seqProg = 0; fusesHeld = 0; fusesIn = 0;
        fuseTotal = def.lockerHasFuse ? 1 : 0;
        seqShowUntil = 0f; platesOn = 0;
        wordInput = ""; wordErr = ""; safeErr = "";

        var floorTiles = new List<Vector2Int>();
        string[] wingNames = { "Красный (PF)", "Зелёный (KB)", "Синий (TB)", "Жёлтый (AB)" };

        for (int r = 0; r < def.H; r++)
            for (int c = 0; c < def.W; c++)
            {
                char ch = def.map[r][c];
                Vector2 pos = P(c, r);

                if (ch == ' ')
                {
                    bool near = false;
                    for (int j = -1; j <= 1 && !near; j++)
                        for (int i = -1; i <= 1; i++)
                            if (At(c + i, r + j) != ' ') { near = true; break; }
                    if (near)
                    {
                        bool front = At(c, r + 1) != ' ';
                        if (front)
                        {
                            int wv = WallVariant(c, r);
                            var w = Spawn($"wall_{def.wallTheme}_{wv}", pos, DepthSort.Order(pos.y - 0.48f), true, def.tint * 0.96f);
                            w.layer = LWall;
                            w.GetComponent<BoxCollider2D>().size = Vector2.one;
                            if ((def.wallTheme == "beige" && wv == 7) || (def.wallTheme == "conc" && wv == 2))
                            {
                                var gl = Spawn("glow", pos + new Vector2(0f, -0.05f), 2150, false, new Color(1f, 0.9f, 0.62f, 0.44f));
                                var lf = gl.AddComponent<LampFlicker>();
                                lf.baseColor = new Color(1f, 0.9f, 0.62f, 0.44f);
                            }
                        }
                        else
                        {
                            var w = Spawn("top", pos, DepthSort.Order(pos.y), true, def.tint);
                            w.layer = LWall;
                            w.GetComponent<BoxCollider2D>().size = Vector2.one;
                        }
                    }
                    continue;
                }

                if (def.portals.TryGetValue(ch, out var target)) { MakePortal(target, pos); continue; }

                pass[c, r] = true;
                Color tt = TileTint(c, r);
                Spawn($"floor_{def.floorTheme}_{(c * 7 + r * 13) % 6}", pos, -500, false, tt);
                if (At(c, r - 1) == ' ') Spawn("wshadow", pos, -480, false, Color.white);

                if (ch == '.')
                {
                    floorTiles.Add(new Vector2Int(c, r));
                    float roll = Sprites.N(c, r, def.seed + 77);
                    int dv = (c + r * 5) % 3;
                    if (roll < 0.045f) Spawn($"decal_blood_{dv}", pos, -450, false, tt);
                    else if (roll < 0.09f) Spawn($"decal_paper_{dv}", pos, -450, false, tt);
                    else if (roll < 0.13f) Spawn($"decal_debris_{dv}", pos, -450, false, tt);
                    else if (roll < 0.145f) Spawn($"decal_glass_{dv % 2}", pos, -450, false, tt);
                    else if (def.key == "bunker" && roll < 0.19f) Spawn("decal_can_0", pos, -450, false, tt);
                    continue;
                }

                Vector2 propPos = pos + new Vector2(0f, -0.45f);
                int pOrder = DepthSort.Order(propPos.y);
                string id = $"{def.key}:{c},{r}";

                switch (ch)
                {
                    case 'Z':
                        if (!(def.key == "hub" && taken.Contains(id))) SpawnZombie(pos, id);
                        break;
                    case 'm': case 'A': case 'K':
                        {
                            char pk = ch == 'A' ? 'a' : ch;
                            if (!taken.Contains(id) && !(ch == 'K' && hasKey)) MakePickup(pk, pos, id);
                            break;
                        }
                    case 'y':
                        if (active && !ps.hasLockerKey && !ps.lockerOpened)
                        {
                            var lk = Spawn("lkey", pos, -150, false, Color.white);
                            Inter(lk, "взять ключ от шкафчика", () =>
                            {
                                player.PlayClip(AnimClip.Pickup, 0.6f);
                                Sfx.PlayPickup();
                                ps.hasLockerKey = true;
                                Msg("Взят ключ от металлического шкафчика!", 3f);
                                Destroy(lk);
                            });
                        }
                        break;
                    case 'L':
                        {
                            bool open = ps != null && (ps.lockerOpened || ps.done);
                            var lr = Spawn(open ? "locker" : "lockerL", propPos, pOrder, true, tt);
                            lr.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
                            if (active)
                                Inter(lr, "открыть запертый шкаф", () =>
                                {
                                    if (ps.lockerOpened || ps.done) { Msg("Внутри больше ничего нет.", 2f); return; }
                                    if (!ps.hasLockerKey) { Sfx.PlayError(); Msg("Шкафчик заперт на замок. Найди ключ!", 3f); return; }
                                    ps.lockerOpened = true;
                                    player.PlayClip(AnimClip.Pickup, 0.7f);
                                    Sfx.PlayUnlock();
                                    lr.GetComponent<SpriteRenderer>().sprite = Sprites.Get("locker");
                                    if (def.lockerHasFuse)
                                    {
                                        fusesHeld++;
                                        Msg($"Шкафчик открыт! Найден 3-й предохранитель ({fusesIn + fusesHeld}/{fuseTotal}).", 3.5f);
                                    }
                                    else
                                    {
                                        ps.hasCard = true;
                                        Msg("Шкафчик открыт! Получена магнитная карта доступа!", 3.5f);
                                    }
                                });
                            break;
                        }
                    case 'R':
                        {
                            var rd = Spawn("reader", propPos, pOrder, true, tt);
                            rd.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
                            Spawn("glow", pos + new Vector2(0f, 0.1f), 2140, false, active ? new Color(1f, 0.35f, 0.3f, 0.35f) : new Color(0.35f, 1f, 0.45f, 0.35f));
                            if (active && def.needKeyCard)
                                Inter(rd, "приложить магнитную карту", () =>
                                {
                                    if (cardDone) return;
                                    if (!ps.hasCard) { Sfx.PlayError(); Msg("Нужна магнитная карта из запертого шкафа.", 3f); return; }
                                    cardDone = true;
                                    Sfx.PlayUnlock();
                                    Msg("Карта доступа принята! Сектор 1 разблокирован!", 3.5f);
                                    CheckComplete();
                                });
                            break;
                        }
                    case 'V':
                        {
                            var tm = Spawn("terminal", propPos, pOrder, true, tt);
                            tm.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
                            Spawn("glow", pos + new Vector2(0f, 0.25f), 2140, false, new Color(0.3f, 1f, 0.48f, 0.36f));
                            if (active && def.needWord)
                                Inter(tm, "ввести кодовое слово", () =>
                                {
                                    if (wordDone) return;
                                    if (def.needFuses && !fusesDone)
                                    {
                                        Sfx.PlayError();
                                        Msg("Нет питания! Сначала вставь все предохранители в щиток.", 3.5f);
                                        return;
                                    }
                                    Sfx.PlayClick();
                                    state = GState.Word;
                                    wordInput = ""; wordErr = "";
                                    modalLock = Time.unscaledTime + 0.15f;
                                });
                            break;
                        }
                    case 'Q':
                        {
                            var sf = Spawn("safe", propPos, pOrder, true, tt);
                            sf.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
                            if (active && def.needSafe)
                                Inter(sf, "взломать сейф (лимб)", () =>
                                {
                                    if (safeDone) return;
                                    Sfx.PlayClick();
                                    state = GState.Safe;
                                    safeStep = 0; safeErr = "";
                                    modalLock = Time.unscaledTime + 0.15f;
                                });
                            break;
                        }
                    case 'q': case 'w': case 'e': case 'o':
                        {
                            int ci = "qweo".IndexOf(ch);
                            if (active && (def.needWord || def.needSafe) && ci < 3)
                            {
                                var nt = Spawn("note", pos, -140, false, Color.white);
                                Inter(nt, "прочитать записку", () =>
                                {
                                    player.PlayClip(AnimClip.Pickup, 0.55f);
                                    Sfx.PlayPickup();
                                    ps.known[ci] = true;
                                    if (def.needSafe)
                                        Msg($"Записка №{ci + 1}: деление лимба сейфа = {ps.safeTargets[ci]:D2}", 5f);
                                    else if (def.needWord && ps.wordClues != null && ci < ps.wordClues.Length)
                                        Msg(ps.wordClues[ci], 6f);
                                });
                            }
                            break;
                        }
                    case 'f':
                        if (active && def.needFuses)
                        {
                            fuseTotal++;
                            var fu = Spawn("fuse", pos, -140, false, Color.white);
                            Inter(fu, "взять предохранитель", () =>
                            {
                                player.PlayClip(AnimClip.Pickup, 0.6f);
                                Sfx.PlayPickup();
                                fusesHeld++;
                                Msg($"Предохранитель взят ({fusesIn + fusesHeld}/{fuseTotal}).", 2.5f);
                                Destroy(fu);
                            });
                        }
                        break;
                    case 'u':
                        {
                            var fb = Spawn("fusebox", propPos, pOrder, true, tt);
                            fb.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
                            if (active && def.needFuses)
                                Inter(fb, "вставить предохранители", () =>
                                {
                                    if (fusesDone) { Msg("Щиток уже под напряжением.", 2f); return; }
                                    if (fusesHeld == 0) { Sfx.PlayError(); Msg($"Нужны предохранители: {fusesIn}/{fuseTotal}", 2.5f); return; }
                                    fusesIn += fusesHeld; fusesHeld = 0;
                                    if (fusesIn >= fuseTotal)
                                    {
                                        fusesDone = true; Sfx.PlayUnlock();
                                        Msg("Щиток заработал! Питание подано!", 3.5f);
                                        CheckComplete();
                                    }
                                    else { Sfx.PlayClick(); Msg($"Вставлено {fusesIn}/{fuseTotal}.", 2.5f); }
                                });
                            break;
                        }
                    case 'c': if (active && def.needCrates) MakeCrate(pos, propPos); break;
                    case 'p': if (active && def.needCrates) { Spawn("plate", pos, -400, false, Color.white); plates.Add(pos); } break;
                    case 'r': case 'g': case 't': case 'j':
                        if (active && def.seqLen > 0)
                        {
                            int ci = "rgtj".IndexOf(ch);
                            var sw = Spawn("lever", propPos, pOrder, false, ColorOf(ci));
                            Inter(sw, "нажать рычаг: " + wingNames[ci], () => SeqPress(ci));
                        }
                        break;
                    case 'n':
                        if (active && def.seqLen > 0)
                        {
                            var cn = Spawn("console", propPos, pOrder, true, tt);
                            cn.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
                            Inter(cn, "изучить консоль", () =>
                            {
                                Sfx.PlayClick();
                                seqShowUntil = Time.time + (def.flash ? 4.5f : 20f);
                                Msg(def.flash ? "Запоминай последовательность блоков!" : "Порядок рычагов на экране.", 3f);
                            });
                        }
                        break;
                    case 'h':
                        {
                            var sh = Spawn("shelf", propPos, pOrder, true, tt);
                            sh.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
                            Inter(sh, "осмотреть консервы", () =>
                            {
                                if (taken.Contains(id)) { Msg("Пусто. Остались пустые жестянки.", 2f); return; }
                                taken.Add(id); hp = Mathf.Min(maxHp, hp + 15f);
                                player.PlayClip(AnimClip.Pickup, 0.6f); Sfx.PlayPickup();
                                Msg("Ты съел банку тушёнки. +15 HP", 2.5f);
                            });
                            break;
                        }
                    case 's':
                        {
                            var so = Spawn("sofa", propPos, pOrder, true, tt);
                            so.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
                            Inter(so, "отдохнуть на диване", () => { hp = maxHp; Sfx.PlayPickup(); Msg("Ты перевёл дух на диване. HP восстановлено.", 2.5f); });
                            break;
                        }
                    case 'N': SpawnSurvivor(pos, propPos, pOrder, c); break;
                    case 'D': { var dsk = Spawn("desk", propPos, pOrder, true, tt); dsk.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f); break; }
                    case 'H': { var bk = Spawn("books", propPos, pOrder, true, tt); bk.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f); break; }
                    case 'l': { var lk = Spawn("locker", propPos, pOrder, true, tt); lk.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f); break; }
                    case 'T': { var tb = Spawn("table", propPos, pOrder, true, tt); tb.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f); break; }
                    case 'i': Spawn("chair", propPos, pOrder, false, tt); break;
                    case 'I': Spawn("chairF", propPos, pOrder, false, tt); break;
                    case 'v': Spawn("plant", propPos, pOrder, false, tt); break;
                    case 'W':
                        {
                            var bd = Spawn("board", propPos, pOrder, true, Color.white);
                            bd.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
                            Inter(bd, "смотреть карту КБТУ", () => { Sfx.PlayClick(); state = GState.Map; });
                            break;
                        }
                    case 'M':
                        {
                            var dn = Spawn("note", pos, -140, false, new Color(0.75f, 0.95f, 1f));
                            Inter(dn, "прочитать журнал", () =>
                            {
                                player.PlayClip(AnimClip.Pickup, 0.55f); Sfx.PlayPickup();
                                Msg(string.IsNullOrEmpty(def.dossier) ? def.hint : def.dossier, 7f);
                            });
                            break;
                        }
                }
            }

        // Deterministic zombies / loot for hub and dangerous optional offices
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
                string rid = def.key + ":r" + idx;
                if (z < def.randZ) { z++; if (!(def.key == "hub" && taken.Contains(rid))) SpawnZombie(pos, rid); }
                else if (am < def.randAmmo) { am++; if (!taken.Contains(rid)) MakePickup('a', pos, rid); }
                else if (md < def.randMed) { md++; if (!taken.Contains(rid)) MakePickup('m', pos, rid); }
                else break;
            }
        }

        foreach (var l in def.labels) labels.Add((new Vector2(l.c, def.H - 1 - l.r), l.t));
        player.transform.position = FindSpawn(from);
        player.rb.linearVelocity = Vector2.zero;
        cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
        portalLock = Time.time + 0.45f;
        Sfx.SetAlarm(def.alarm && state == GState.Play);
        if (state == GState.Play) Msg(def.title + (string.IsNullOrEmpty(def.hint) ? "" : "\n" + def.hint), 5.5f);
    }

    void SpawnSurvivor(Vector2 pos, Vector2 propPos, int pOrder, int col)
    {
        Color[] coats = { new Color(1f, 0.78f, 0.55f), new Color(0.68f, 0.88f, 1f), new Color(0.85f, 0.95f, 0.72f) };
        Spawn("shadow", propPos + new Vector2(0f, 0.08f), -100, false, Color.white).transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        var nn = Spawn("player", propPos, pOrder, true, coats[col % coats.Length]);
        nn.GetComponent<BoxCollider2D>().offset = new Vector2(0f, 0.45f);
        nn.GetComponent<BoxCollider2D>().size = new Vector2(0.65f, 0.7f);
        Inter(nn, "поговорить с выжившим", () =>
        {
            Sfx.PlayClick();
            Msg(SurvivorDialogue(col), 5.5f);
        });
    }

    string SurvivorDialogue(int idx)
    {
        if (hasKey) return "Выживший: «Ключ от туннеля у тебя! Беги к выходу в нижнем левом углу 0 этажа!»";
        if (completed >= 7) return "Выживший: «Все 7 этапов пройдены! Секьюрити вверху справа на 0 этаже открыта — забери ключ!»";
        switch (idx % 3)
        {
            case 0:
                return completed == 0
                    ? "Выживший: «Те, кто вышел наверх, не вернулись... В 157 кабинете слева (AB) заперта магнитная карта.»"
                    : $"Выживший: «Следующая цель — {Areas.TitleOf("p" + (completed + 1))}. Будь осторожен.»";
            case 1:
                return "Выживший: «Синие коридоры — Толе би, жёлтые — Абылай хана, красные — Панфилова, зелёные — Казыбек би. Жми Tab для карты.»";
            default:
                return "Выживший: «Ранен? Отдохни здесь на диване или поешь консервов. Осторожнее в кабинетах 305, 162 и 25 — там полно зомби.»";
        }
    }

    static Color ColorOf(int i)
    {
        switch (i)
        {
            case 0: return new Color(0.95f, 0.25f, 0.25f); // PF Red
            case 1: return new Color(0.22f, 0.9f, 0.35f);  // KB Green
            case 2: return new Color(0.28f, 0.55f, 1f);    // TB Blue
            default: return new Color(1f, 0.88f, 0.22f);   // AB Yellow
        }
    }

    void MakePortal(string target, Vector2 pos)
    {
        bool ok = CanEnter(target, out string reason);
        string spr = ok ? (target == "tunnel" ? "gate" : "door") : (target == "tunnel" ? "gate" : "doorL");
        var g = Spawn(spr, pos, DepthSort.Order(pos.y), false, ok ? Color.white : new Color(0.92f, 0.55f, 0.55f));
        if (ok)
        {
            var bc = g.AddComponent<BoxCollider2D>();
            bc.isTrigger = true;
            bc.size = new Vector2(0.88f, 0.88f);
            g.AddComponent<Portal>().target = target;
        }
        else
        {
            var bc = g.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(0.9f, 0.9f);
            Inter(g, "закрыто", () => { Sfx.PlayError(); Msg(reason, 3.5f); });
        }
        labels.Add((pos + new Vector2(0f, 1.08f), Areas.TitleOf(target) + (ok ? "" : "  [закрыто]")));
    }

    bool CanEnter(string t, out string reason)
    {
        reason = "";
        if (t.Length == 2 && t[0] == 'p' && char.IsDigit(t[1]))
        {
            int n = t[1] - '0';
            if (n > completed + 1) { reason = $"Сначала пройди этап {completed + 1}/7."; return false; }
        }
        if (t == "sec" && completed < 7) { reason = "Секьюрити заперта: нужно пройти все 7 головоломок."; return false; }
        if (t == "tunnel" && !hasKey) { reason = "Туннель заперт. Нужен ключ из Секьюрити (вверху справа)."; return false; }
        return true;
    }

    public void TryGo(string target)
    {
        if (Time.time < portalLock || state != GState.Play) return;
        if (!CanEnter(target, out string reason)) { Sfx.PlayError(); Msg(reason, 3f); return; }
        Sfx.PlayDoor();
        if (target == "tunnel") { Sfx.SetAlarm(false); Sfx.PlayUnlock(); state = GState.Win; return; }
        Enter(target, def.key);
    }

    void SpawnZombie(Vector2 pos, string id)
    {
        var g = new GameObject("zombie");
        g.transform.SetParent(root);
        g.transform.position = pos;
        g.layer = 9;
        var z = g.AddComponent<Zombie>();
        z.id = id;
        z.sight = def.sight;
    }

    public void OnZombieDeath(Zombie z)
    {
        if (def.key == "hub") taken.Add(z.id);
        Spawn("decal_blood_" + UnityEngine.Random.Range(0, 3), z.transform.position + new Vector3(0f, -0.25f, 0f), -450, false, def.tint);
        if (z.set != null)
        {
            var c = new GameObject("corpse");
            c.transform.SetParent(root);
            c.transform.position = z.transform.position + new Vector3(0f, -0.32f, 0f);
            c.transform.localScale = Vector3.one * z.set.scale;
            var sr = c.AddComponent<SpriteRenderer>();
            sr.sortingOrder = DepthSort.Order(z.transform.position.y - 0.32f);
            var cp = c.AddComponent<Corpse>();
            cp.fallFrames = z.set.fall;
            cp.deadFrames = z.set.dead;
            cp.flipX = z.faceLeft;
            cp.tint = def.tint;
        }
        if (UnityEngine.Random.value < 0.25f) MakePickup('a', z.transform.position, null);
    }

    void MakePickup(char kind, Vector2 pos, string id)
    {
        string spr = kind == 'm' ? "medkit" : (kind == 'a' ? "ammo" : "key");
        var g = Spawn(spr, pos, -140, false, Color.white);
        var bc = g.AddComponent<BoxCollider2D>();
        bc.isTrigger = true;
        bc.size = new Vector2(0.8f, 0.8f);
        var p = g.AddComponent<Pickup>();
        p.kind = kind;
        p.id = id;
    }

    public void Collect(char kind, string id)
    {
        if (id != null) taken.Add(id);
        player.PlayClip(AnimClip.Pickup, 0.6f);
        Sfx.PlayPickup();
        if (kind == 'm') { medkits++; Msg("Аптечка +1" + (heal ? "" : " (лечение откроется после этапа 2)"), 2.5f); }
        else if (kind == 'a') { ammo += 8; Msg("Патроны +8" + (hasW[1] ? "" : " (огнестрел скоро откроется)"), 2f); }
        else { hasKey = true; Sfx.PlayUnlock(); Msg("КЛЮЧ ОТ ТУННЕЛЯ! Беги к выходу на 0 этаже (внизу слева)!", 6f); }
    }

    void MakeCrate(Vector2 pos, Vector2 propPos)
    {
        var g = Spawn("crate", propPos, DepthSort.Order(propPos.y), true);
        var bc = g.GetComponent<BoxCollider2D>();
        bc.size = new Vector2(0.88f, 0.88f);
        bc.offset = new Vector2(0f, 0.45f);
        var rb = g.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.mass = 2.5f; rb.linearDamping = 14f;
        g.layer = 10;
        crates.Add(g);
    }

    // ================= puzzle logic =================
    void SeqPress(int color)
    {
        if (seqDone) return;
        if (ps.seq[seqProg] == color)
        {
            seqProg++;
            Sfx.PlayLever();
            Msg($"Щёлк! Рычаг {seqProg}/{ps.seq.Length}", 1.5f);
        }
        else
        {
            seqProg = 0;
            Sfx.PlayError();
            Msg("Неверный цвет блока! Последовательность сброшена.", 2.5f);
        }
        if (seqProg >= ps.seq.Length)
        {
            seqDone = true;
            Sfx.PlayUnlock();
            Msg("Рычаги активированы!", 3f);
            CheckComplete();
        }
    }

    void SubmitWord()
    {
        string guess = (wordInput ?? "").Trim().ToUpperInvariant();
        bool match = false;
        if (ps.wordAccepts != null)
            foreach (var a in ps.wordAccepts) if (guess == a) { match = true; break; }
        if (match)
        {
            wordDone = true;
            state = GState.Play;
            Sfx.PlayUnlock();
            Msg($"Терминал принял пароль «{ps.wordAnswer}»!", 3.5f);
            CheckComplete();
        }
        else
        {
            Sfx.PlayError();
            wordErr = "Неверный пароль. Ищи подсказки на листах в комнате!";
            wordInput = "";
        }
    }

    void TurnDial(int delta)
    {
        safeDial = (safeDial + delta + 60) % 60;
        Sfx.PlayClick();
        safeErr = "";
    }

    void TestDial()
    {
        if (safeStep >= ps.safeTargets.Length) return;
        if (safeDial == ps.safeTargets[safeStep])
        {
            Sfx.PlayUnlock();
            safeStep++;
            safeErr = $"Щелчок! Деление {safeStep}/3 поймано.";
            if (safeStep >= ps.safeTargets.Length)
            {
                safeDone = true;
                state = GState.Play;
                Msg("Сейф открылся!", 3.5f);
                CheckComplete();
            }
        }
        else
        {
            Sfx.PlayError();
            safeStep = 0;
            safeErr = "Сорвалось! Сейф сброшен на шаг 1/3.";
        }
    }

    void CheckComplete()
    {
        if (def.needKeyCard && !cardDone) return;
        if (def.needWord && !wordDone) return;
        if (def.needSafe && !safeDone) return;
        if (def.needFuses && !fusesDone) return;
        if (def.needCrates && !cratesDone) return;
        if (def.seqLen > 0 && !seqDone) return;
        if (ps == null || ps.done) return;

        ps.done = true;
        int n = def.puzzle;
        completed = Mathf.Max(completed, n);
        player.PlayClip(AnimClip.Victory, 1.8f);
        Sfx.PlayUnlock();

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
        Msg($"ЭТАП {n}/7 ПРОЙДЕН!\nНаграда: {Rewards[n]}", 7f);
    }

    public void UseMedkit()
    {
        if (!heal) { Sfx.PlayError(); Msg("Навык лечения откроется после этапа 2.", 2f); return; }
        if (medkits <= 0) { Sfx.PlayError(); Msg("Нет аптечек.", 1.5f); return; }
        if (hp >= maxHp) return;
        medkits--;
        hp = Mathf.Min(maxHp, hp + 40f);
        Sfx.PlayPickup();
        Msg("+40 HP", 1.5f);
    }

    // ================= death / respawn =================
    public void Die()
    {
        if (state != GState.Play) return;
        state = GState.Dead;
        deadT = 2.8f;
        Sfx.SetAlarm(false);
        player.rb.linearVelocity = Vector2.zero;
        player.PlayClip(AnimClip.Fall, 1.3f);
    }

    void Respawn()
    {
        hp = maxHp;
        state = GState.Play;
        if (completed == 0) Enter("bunker", null);
        else Enter("hub", "p" + completed);
        Msg("Тебя укусили... Ты очнулся на предыдущем этапе.", 4.5f);
    }

    // ================= pathfinding =================
    Vector2Int TileOf(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x), def.H - 1 - Mathf.RoundToInt(p.y));

    void RecomputeFlow()
    {
        if (flow == null) flow = new int[def.W, def.H];
        for (int x = 0; x < def.W; x++) for (int y = 0; y < def.H; y++) flow[x, y] = -1;
        var t = TileOf(player.transform.position);
        if (t.x < 0 || t.y < 0 || t.x >= def.W || t.y >= def.H) return;
        var q = new Queue<Vector2Int>();
        flow[t.x, t.y] = 0; q.Enqueue(t);
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int x = p.x + dx[k], y = p.y + dy[k];
                if (x < 0 || y < 0 || x >= def.W || y >= def.H || flow[x, y] >= 0 || !pass[x, y]) continue;
                flow[x, y] = flow[p.x, p.y] + 1;
                q.Enqueue(new Vector2Int(x, y));
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
                if (flow[x, y] < best) { best = flow[x, y]; bt = new Vector2Int(x, y); found = true; }
            }
        if (!found) return false;
        target = P(bt.x, bt.y);
        return true;
    }

    // ================= update =================
    void Update()
    {
        if (hurtFlash > 0f) hurtFlash -= Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.F8)) Sfx.ToggleMute();

        switch (state)
        {
            case GState.Title:
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                { Sfx.PlayClick(); state = GState.Intro; introPage = 0; }
                else if (Input.GetKeyDown(KeyCode.K))
                { Sfx.PlayDoor(); state = GState.Play; Msg(def.title + "\n" + def.hint, 5.5f); }
                break;

            case GState.Intro:
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    Sfx.PlayDoor(); state = GState.Play; Msg(def.title + "\n" + def.hint, 5.5f);
                }
                else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
                {
                    Sfx.PlayClick();
                    introPage++;
                    if (introPage >= IntroCaptions.Length) { state = GState.Play; Msg(def.title + "\n" + def.hint, 6f); }
                }
                break;

            case GState.Dead:
                deadT -= Time.deltaTime;
                if (deadT < 1.5f) player.PlayClip(AnimClip.Dead, 1.5f);
                if (deadT <= 0f) Respawn();
                break;

            case GState.Word:
                if (Input.GetKeyDown(KeyCode.Escape)) state = GState.Play;
                break;

            case GState.Safe:
                if (Time.unscaledTime >= modalLock)
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) state = GState.Play;
                    else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) TurnDial(-1);
                    else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) TurnDial(1);
                    else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) TurnDial(-5);
                    else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) TurnDial(5);
                    else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) TestDial();
                }
                break;

            case GState.Map:
                if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape))
                { Sfx.PlayClick(); state = GState.Play; }
                break;

            case GState.Win:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) NewRun(GState.Play);
                break;

            case GState.Play:
                if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.M)) { Sfx.PlayClick(); state = GState.Map; break; }
                flowT -= Time.deltaTime;
                if (flowT <= 0f) { flowT = 0.25f; RecomputeFlow(); }

                if (def.needCrates && !cratesDone && plates.Count > 0)
                {
                    int on = 0;
                    foreach (var p in plates)
                        foreach (var c in crates)
                            if (c != null && Vector2.Distance(p, c.transform.position + new Vector3(0f, 0.45f, 0f)) < 0.48f) { on++; break; }
                    if (on > platesOn) Sfx.PlayClick();
                    platesOn = on;
                    if (on >= plates.Count) { cratesDone = true; Sfx.PlayUnlock(); Msg("Ящики зафиксированы на плитах!", 3f); CheckComplete(); }
                }

                if (Input.GetKeyDown(KeyCode.R) && def.puzzle > 0)
                { Enter(def.key, lastFrom); Msg("Комната сброшена.", 2f); }
                break;
        }
    }

    void LateUpdate()
    {
        if (player == null || cam == null) return;
        shakeAmt = Mathf.MoveTowards(shakeAmt, 0f, Time.deltaTime * 1.4f);
        Vector2 off = shakeAmt > 0f ? UnityEngine.Random.insideUnitCircle * shakeAmt : Vector2.zero;
        var t = player.transform.position;
        var target = new Vector3(t.x + off.x, t.y + off.y, -10f);
        cam.transform.position = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
    }

    // ================= GUI =================
    GUIStyle sTxt, sMed, sBig, sSmall, sBtn, sFld;
    Texture2D white, vign;
    Font osFont;

    void InitGUI()
    {
        white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
        int n = 128; vign = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x - n * 0.5f) / (n * 0.5f), dy = (y - n * 0.5f) / (n * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                vign.SetPixel(x, y, new Color(0, 0, 0, Mathf.SmoothStep(0.32f, 1.1f, d) * 0.90f));
            }
        vign.Apply();

        try { osFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Segoe UI", "Arial" }, 18); }
        catch { osFont = null; }

        sTxt = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, richText = true };
        if (osFont != null) sTxt.font = osFont;
        sMed = new GUIStyle(sTxt) { fontSize = 22, fontStyle = FontStyle.Bold };
        sBig = new GUIStyle(sTxt) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sSmall = new GUIStyle(sTxt) { fontSize = 14 };
        sBtn = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
        if (osFont != null) sBtn.font = osFont;
        sFld = new GUIStyle(GUI.skin.textField) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
        if (osFont != null) sFld.font = osFont;
    }

    void Txt(Rect r, string t, GUIStyle st, Color c, TextAnchor a = TextAnchor.UpperLeft)
    {
        var old = st.alignment; st.alignment = a;
        GUI.color = new Color(0, 0, 0, 0.92f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), t, st);
        GUI.color = c;
        GUI.Label(r, t, st);
        GUI.color = Color.white;
        st.alignment = old;
    }

    void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, white); GUI.color = Color.white; }

    void DrawStoryTex(Texture2D tex, float vw, float vh, float dim = 1f)
    {
        Fill(new Rect(0, 0, vw, vh), Color.black);
        if (tex == null) return;
        GUI.color = new Color(dim, dim, dim, 1f);
        GUI.DrawTexture(new Rect(0, 0, vw, vh), tex, ScaleMode.ScaleAndCrop);
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        if (sTxt == null) InitGUI();
        float sw = Screen.width, sh = Screen.height, s = sh / 720f, vw = sw / s;
        var full = new Rect(0, 0, sw, sh);

        if (state != GState.Title && state != GState.Intro && state != GState.Win)
        {
            GUI.color = Color.white; GUI.DrawTexture(full, vign);
            if (def != null && def.alarm) Fill(full, new Color(1, 0, 0, (Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f) * 0.09f));
            if (hurtFlash > 0f) Fill(full, new Color(0.85f, 0, 0, Mathf.Clamp01(hurtFlash) * 0.7f));
        }

        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        Color W = Color.white;

        if (state == GState.Title)
        {
            DrawStoryTex(storyTex[0], vw, 720, 0.75f);
            Fill(new Rect(0, 210, vw, 300), new Color(0, 0, 0, 0.65f));
            Txt(new Rect(80, 230, vw - 160, 80), "K B T U   O U T B R E A K", sBig, new Color(1f, 0.86f, 0.45f), TextAnchor.MiddleCenter);
            Txt(new Rect(80, 305, vw - 160, 70), "2D Pixel Survival Horror в университете КБТУ\n7 этапов • 7 головоломок • Побег через подземный туннель", sMed, W, TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(vw * 0.5f - 170, 405, 160, 44), "НАЧАТЬ ИСТОРИЮ", sBtn)) { Sfx.PlayClick(); state = GState.Intro; introPage = 0; }
            if (GUI.Button(new Rect(vw * 0.5f + 10, 405, 160, 44), "СРАЗУ В БУНКЕР", sBtn)) { Sfx.PlayDoor(); state = GState.Play; Msg(def.title + "\n" + def.hint, 5.5f); }
            Txt(new Rect(0, 468, vw, 30), "Пробел — пролог  |  K — пропустить  |  F8 — звук", sSmall, new Color(0.78f, 0.78f, 0.78f), TextAnchor.MiddleCenter);
            return;
        }

        if (state == GState.Intro)
        {
            int idx = Mathf.Clamp(introPage, 0, IntroCaptions.Length - 1);
            DrawStoryTex(storyTex[idx], vw, 720, 0.92f);
            Fill(new Rect(0, 0, vw, 44), new Color(0, 0, 0, 0.85f));
            Fill(new Rect(0, 540, vw, 180), new Color(0, 0, 0, 0.86f));
            Txt(new Rect(24, 10, 200, 28), $"ПРОЛОГ ({idx + 1}/{IntroCaptions.Length})", sTxt, new Color(1f, 0.85f, 0.45f));
            Txt(new Rect(90, 558, vw - 180, 105), IntroCaptions[idx], sMed, W, TextAnchor.MiddleCenter);
            Txt(new Rect(0, 678, vw, 30), "Пробел / клик — далее    |    Esc — в игру", sSmall, new Color(0.65f, 0.65f, 0.65f), TextAnchor.MiddleCenter);
            return;
        }

        if (state == GState.Win)
        {
            DrawStoryTex(storyTex[5], vw, 720, 0.95f);
            Fill(new Rect(0, 500, vw, 220), new Color(0, 0, 0, 0.84f));
            Txt(new Rect(90, 514, vw - 180, 140),
                "ТЫ ВЫБРАЛСЯ ИЗ КБТУ!\nКлюч открыл тяжёлый затвор подземного туннеля. Все выжившие из бункера спасены!",
                sMed, new Color(1f, 0.94f, 0.72f), TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(vw * 0.5f - 110, 654, 220, 42), "НАЧАТЬ ЗАНОВО (Enter)", sBtn)) NewRun(GState.Play);
            return;
        }

        // ---- In-game HUD ----
        Fill(new Rect(16, 14, 264, 24), new Color(0, 0, 0, 0.7f));
        Fill(new Rect(18, 16, 260f * Mathf.Clamp01(hp / maxHp), 20), new Color(0.82f, 0.14f, 0.14f));
        Txt(new Rect(22, 12, 260, 26), "HP " + Mathf.CeilToInt(Mathf.Max(0f, hp)) + (armor ? "  [БРОНЯ]" : ""), sTxt, W);

        string wpn = Player.WNames[cur] + (cur > 0 ? $"  [{ammo}]" : "");
        string extra = (grenades > 0 ? $"   Гранаты: {grenades}" : "") + (heal ? $"   Аптечки: {medkits}" : "") + (dash ? "   Рывок" : "") + (hasKey ? "   [КЛЮЧ]" : "");
        Txt(new Rect(16, 44, 650, 28), wpn + extra, sTxt, new Color(1f, 0.9f, 0.55f));

        string obj = hasKey ? "КЛЮЧ У ТЕБЯ! Беги к туннелю (0 этаж, TB внизу слева)!"
            : completed >= 7 ? "Все 7 этапов пройдены! Иди в Секьюрити (0 этаж, вверху справа)!"
            : $"Цель ({completed}/7): {Areas.TitleOf("p" + (completed + 1))}";
        Txt(new Rect(vw - 540, 14, 524, 56), obj, sTxt, new Color(0.65f, 1f, 0.65f), TextAnchor.UpperRight);
        Txt(new Rect(vw * 0.5f - 280, 10, 560, 28), def.title, sTxt, new Color(0.88f, 0.88f, 0.88f), TextAnchor.UpperCenter);
        Txt(new Rect(vw - 320, 686, 304, 24), "Tab / M — Карта КБТУ   |   F8 — Звук", sSmall, new Color(0.7f, 0.7f, 0.7f), TextAnchor.UpperRight);

        // Active puzzle tracker
        float py = 78f;
        if (def.puzzle > 0 && ps != null && !ps.done)
        {
            if (def.needKeyCard)
            {
                string st = ps.hasCard ? "Карта найдена -> приложи к считывателю у выхода"
                    : ps.hasLockerKey ? "Ключ найден -> открой запертый шкаф"
                    : "Найди ключ от металлического шкафа в аудитории";
                Txt(new Rect(16, py, 600, 26), st, sTxt, W); py += 26;
            }
            if (def.needFuses)
            {
                string extraF = def.lockerHasFuse && !ps.lockerOpened ? (ps.hasLockerKey ? " + открой шкаф ключом" : " + найди ключ от шкафа") : "";
                Txt(new Rect(16, py, 680, 26), $"Щиток: {fusesIn}/{fuseTotal} предохр. (в руках {fusesHeld}){extraF}", sTxt, W); py += 26;
            }
            if (def.needWord && (!def.needFuses || fusesDone))
            {
                int knownCnt = 0; for (int i = 0; i < 3; i++) if (ps.known[i]) knownCnt++;
                Txt(new Rect(16, py, 600, 26), $"Пароль КБТУ: найдено подсказок {knownCnt}/3 -> введи на терминале", sTxt, W); py += 26;
            }
            if (def.needSafe)
            {
                string cs = "";
                for (int i = 0; i < 3; i++) cs += (ps.known[i] ? ps.safeTargets[i].ToString("D2") : "??") + " ";
                Txt(new Rect(16, py, 500, 26), "Записки сейфа: " + cs, sTxt, W); py += 26;
            }
            if (def.needCrates) { Txt(new Rect(16, py, 500, 26), $"Ящики на плитах: {platesOn}/{plates.Count}", sTxt, W); py += 26; }
            if (def.seqLen > 0) { Txt(new Rect(16, py, 500, 26), $"Кодовые рычаги: {seqProg}/{ps.seq.Length}", sTxt, W); py += 26; }
        }

        if (def.seqLen > 0 && !seqDone && Time.time < seqShowUntil)
        {
            float x0 = vw * 0.5f - ps.seq.Length * 32f;
            Fill(new Rect(x0 - 12, 68, ps.seq.Length * 64 + 24, 68), new Color(0, 0, 0, 0.8f));
            for (int i = 0; i < ps.seq.Length; i++) Fill(new Rect(x0 + i * 64 + 4, 76, 56, 52), ColorOf(ps.seq[i]));
        }

        // World door/wing labels
        foreach (var l in labels)
        {
            if (Vector2.Distance(l.p, player.transform.position) > 13f) continue;
            Vector3 sp = cam.WorldToScreenPoint(l.p);
            if (sp.z < 0f) continue;
            Txt(new Rect(sp.x / s - 160, (sh - sp.y) / s - 12, 320, 26), l.t, sSmall, new Color(1f, 0.95f, 0.62f), TextAnchor.MiddleCenter);
        }

        if (!string.IsNullOrEmpty(prompt) && state == GState.Play)
            Txt(new Rect(0, 548, vw, 32), prompt, sMed, new Color(1f, 1f, 0.48f), TextAnchor.MiddleCenter);
        if (Time.time < msgUntil && state != GState.Dead)
        {
            Fill(new Rect(vw * 0.5f - 460, 588, 920, 92), new Color(0, 0, 0, 0.68f));
            Txt(new Rect(vw * 0.5f - 445, 594, 890, 82), msgText, sTxt, W, TextAnchor.MiddleCenter);
        }

        if (state == GState.Dead)
        {
            Fill(new Rect(0, 0, vw, 720), new Color(0.35f, 0, 0, 0.62f));
            Txt(new Rect(0, 280, vw, 130), "ТЕБЯ УКУСИЛИ\nВозврат на предыдущий этап...", sBig, W, TextAnchor.MiddleCenter);
        }

        if (state == GState.Word) DrawWordTerminal(vw);
        if (state == GState.Safe) DrawSafeModal(vw);
        if (state == GState.Map) DrawKbtuMap(vw);
    }

    void DrawWordTerminal(float vw)
    {
        float x = vw * 0.5f - 280f, y = 140f;
        Fill(new Rect(x, y, 560, 410), new Color(0.04f, 0.08f, 0.05f, 0.96f));
        Fill(new Rect(x + 8, y + 8, 544, 38), new Color(0.1f, 0.3f, 0.14f));
        Txt(new Rect(x + 16, y + 12, 520, 30), "ТЕРМИНАЛ ДОСТУПА КБТУ", sMed, new Color(0.55f, 1f, 0.65f), TextAnchor.MiddleCenter);

        float cy = y + 58;
        for (int i = 0; i < 3; i++)
        {
            string cl = (ps.known[i] && ps.wordClues != null && i < ps.wordClues.Length) ? ps.wordClues[i] : $"Подсказка {i + 1}: [записка в комнате не найдена]";
            Txt(new Rect(x + 22, cy, 516, 40), cl, sSmall, ps.known[i] ? new Color(0.85f, 1f, 0.85f) : new Color(0.55f, 0.55f, 0.55f));
            cy += 42;
        }

        var e = Event.current;
        if (e.type == EventType.KeyDown && Time.unscaledTime >= modalLock && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        { SubmitWord(); e.Use(); }
        else
        {
            GUI.SetNextControlName("wt");
            wordInput = GUI.TextField(new Rect(x + 80, y + 210, 400, 52), wordInput, 20, sFld);
            GUI.FocusControl("wt");
        }

        Txt(new Rect(x + 20, y + 272, 520, 30), wordErr, sTxt, new Color(1f, 0.42f, 0.42f), TextAnchor.MiddleCenter);
        if (GUI.Button(new Rect(x + 110, y + 320, 160, 44), "ВВОД (Enter)", sBtn)) SubmitWord();
        if (GUI.Button(new Rect(x + 290, y + 320, 160, 44), "ОТМЕНА (Esc)", sBtn)) state = GState.Play;
    }

    void DrawSafeModal(float vw)
    {
        float x = vw * 0.5f - 280f, y = 135f;
        Fill(new Rect(x, y, 560, 430), new Color(0.11f, 0.12f, 0.14f, 0.96f));
        Fill(new Rect(x + 8, y + 8, 544, 38), new Color(0.22f, 0.25f, 0.28f));
        Txt(new Rect(x + 16, y + 12, 520, 30), $"СЕЙФ ПРЕСС-ЦЕНТРА — ШАГ {safeStep + 1}/3", sMed, new Color(1f, 0.88f, 0.48f), TextAnchor.MiddleCenter);

        for (int i = 0; i < 3; i++)
        {
            float sx = x + 95 + i * 130;
            bool ok = i < safeStep;
            Fill(new Rect(sx, y + 66, 110, 58), ok ? new Color(0.16f, 0.42f, 0.22f) : new Color(0.06f, 0.06f, 0.08f));
            string lbl = ok ? ps.safeTargets[i].ToString("D2") : (ps.known[i] ? $"({ps.safeTargets[i]:D2})" : "??");
            Txt(new Rect(sx, y + 72, 110, 46), lbl, sMed, ok ? new Color(0.55f, 1f, 0.6f) : new Color(0.85f, 0.85f, 0.85f), TextAnchor.MiddleCenter);
        }

        // Dial bar 00..59
        Fill(new Rect(x + 185, y + 148, 190, 74), new Color(0.05f, 0.05f, 0.06f));
        Txt(new Rect(x + 185, y + 154, 190, 62), safeDial.ToString("D2"), sBig, new Color(1f, 0.92f, 0.55f), TextAnchor.MiddleCenter);
        Fill(new Rect(x + 60, y + 236, 440, 14), new Color(0.05f, 0.05f, 0.06f));
        Fill(new Rect(x + 60 + (safeDial / 59f) * 428f, y + 232, 12, 22), new Color(1f, 0.82f, 0.25f));

        if (GUI.Button(new Rect(x + 70, y + 270, 88, 42), "-5", sBtn)) TurnDial(-5);
        if (GUI.Button(new Rect(x + 170, y + 270, 88, 42), "-1 (A)", sBtn)) TurnDial(-1);
        if (GUI.Button(new Rect(x + 302, y + 270, 88, 42), "+1 (D)", sBtn)) TurnDial(1);
        if (GUI.Button(new Rect(x + 402, y + 270, 88, 42), "+5", sBtn)) TurnDial(5);

        Txt(new Rect(x + 20, y + 326, 520, 28), safeErr, sTxt, new Color(1f, 0.72f, 0.45f), TextAnchor.MiddleCenter);
        if (GUI.Button(new Rect(x + 70, y + 364, 260, 44), "ЗАФИКСИРОВАТЬ (Пробел)", sBtn)) TestDial();
        if (GUI.Button(new Rect(x + 350, y + 364, 140, 44), "ВЫХОД (Esc)", sBtn)) state = GState.Play;
    }

    void DrawKbtuMap(float vw)
    {
        float x = vw * 0.5f - 390f, y = 45f;
        Fill(new Rect(x, y, 780, 630), new Color(0.07f, 0.08f, 0.09f, 0.96f));
        Txt(new Rect(x + 20, y + 14, 740, 34), "СХЕМА УНИВЕРСИТЕТА КБТУ — 0 ЭТАЖ И БЛОКИ", sMed, new Color(1f, 0.88f, 0.48f), TextAnchor.MiddleCenter);

        // Wings
        Fill(new Rect(x + 70, y + 66, 640, 74), new Color(0.16f, 0.45f, 0.22f, 0.85f));   // KB Green top
        Fill(new Rect(x + 70, y + 140, 120, 250), new Color(0.58f, 0.48f, 0.14f, 0.85f)); // AB Yellow left
        Fill(new Rect(x + 590, y + 140, 120, 250), new Color(0.55f, 0.16f, 0.16f, 0.85f)); // PF Red right
        Fill(new Rect(x + 70, y + 390, 640, 74), new Color(0.16f, 0.28f, 0.56f, 0.85f));  // TB Blue bottom
        Fill(new Rect(x + 250, y + 185, 280, 160), new Color(0.22f, 0.22f, 0.24f, 0.9f)); // Canteen

        Txt(new Rect(x + 70, y + 70, 640, 28), "KB — ул. Казыбек би (Зелёный)   |   [3] 726 Абай   |   [7] 428 Финал   |   [S] Секьюрити", sSmall, Color.white, TextAnchor.MiddleCenter);
        Txt(new Rect(x + 74, y + 165, 112, 180), "AB (Жёлтый)\n\n[1] Каб 157\n[6] Каб 270\n[a] 305 (опас)", sSmall, Color.white, TextAnchor.MiddleCenter);
        Txt(new Rect(x + 594, y + 165, 112, 180), "PF (Красный)\n\n[2] Каб 117\n[b] 162 (опас)", sSmall, Color.white, TextAnchor.MiddleCenter);
        Txt(new Rect(x + 70, y + 395, 640, 64), "TB — ул. Толе би (Синий)\n[X] ТУННЕЛЬ (слева)   |   [B] БУНКЕР (−1 эт)   |   [5] Каб 346", sSmall, Color.white, TextAnchor.MiddleCenter);
        Txt(new Rect(x + 255, y + 205, 270, 120), $"СТОЛОВАЯ / COWORKING\n[4] 400Б Кабинет Кунаева (4 эт)\n\nТы сейчас: {def.title}", sSmall, new Color(1f, 0.95f, 0.68f), TextAnchor.MiddleCenter);

        string prog = "ПРОГРЕСС: ";
        for (int i = 1; i <= 7; i++) prog += i <= completed ? $"[{i}:OK] " : (i == completed + 1 ? $"[{i}:>>] " : $"[{i}:--] ");
        Txt(new Rect(x + 40, y + 482, 700, 32), prog, sTxt, new Color(0.75f, 1f, 0.75f), TextAnchor.MiddleCenter);
        Txt(new Rect(x + 40, y + 522, 700, 62),
            "Управление: WASD / Стрелки — шаг   |   Мышь + ЛКМ — огонь / дубинка   |   E — действие\n1-4 / Колесо — оружие   |   Shift — бег / рывок   |   H — аптечка   |   G — граната   |   R — сброс ящиков",
            sSmall, new Color(0.82f, 0.82f, 0.82f), TextAnchor.MiddleCenter);

        if (GUI.Button(new Rect(x + 310, y + 584, 160, 36), "ЗАКРЫТЬ (Tab/Esc)", sBtn)) { Sfx.PlayClick(); state = GState.Play; }
    }
}
