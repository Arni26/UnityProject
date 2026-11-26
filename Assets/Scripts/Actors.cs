using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AnimClip { Pickup, Attack, Shoot, Victory, Hurt, Fall, Dead }

public static class DepthSort
{
    public static int Order(float feetY) => 2000 - Mathf.RoundToInt(feetY * 20f);
}

public class Interactable : MonoBehaviour
{
    public string prompt;
    public System.Action use;
    public float range = 1.45f;
}

public class Portal : MonoBehaviour
{
    public string target;
    void OnTriggerEnter2D(Collider2D o)
    {
        if (o.GetComponent<Player>() != null) Game.I.TryGo(target);
    }
}

public class Pickup : MonoBehaviour
{
    public char kind;
    public string id;
    float baseY;
    float phase;

    void Start() { baseY = transform.position.y; phase = Random.value * 6.28f; }

    void Update()
    {
        var p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 3.5f + phase) * 0.06f;
        transform.position = p;
    }

    void OnTriggerEnter2D(Collider2D o)
    {
        if (o.GetComponent<Player>() == null) return;
        Game.I.Collect(kind, id);
        Destroy(gameObject);
    }
}

public class LampFlicker : MonoBehaviour
{
    public Color baseColor = new Color(1f, 0.9f, 0.65f, 0.5f);
    SpriteRenderer sr;
    float phase;

    void Awake() { sr = GetComponent<SpriteRenderer>(); phase = Random.Range(0f, 100f); }

    void Update()
    {
        if (sr == null) return;
        float n = Mathf.PerlinNoise(Time.time * 6.5f, phase);
        float drop = n < 0.22f ? 0.28f : 1f;
        float k = (0.72f + n * 0.35f) * drop;
        sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * k);
    }
}

public class GhostTrail : MonoBehaviour
{
    SpriteRenderer sr;
    Color col;
    float life = 0.22f, maxLife = 0.22f;

    public static void Spawn(Sprite spr, bool flipX, Vector3 pos, Vector3 scale, int sortOrder)
    {
        var g = new GameObject("ghost");
        g.transform.position = pos;
        g.transform.localScale = scale;
        var gt = g.AddComponent<GhostTrail>();
        gt.sr = g.AddComponent<SpriteRenderer>();
        gt.sr.sprite = spr;
        gt.sr.flipX = flipX;
        gt.sr.sortingOrder = sortOrder - 1;
        gt.col = new Color(0.45f, 0.85f, 1f, 0.55f);
        gt.sr.color = gt.col;
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }
        sr.color = new Color(col.r, col.g, col.b, col.a * (life / maxLife));
    }
}

public class Corpse : MonoBehaviour
{
    public Sprite[] fallFrames, deadFrames;
    public bool flipX, tiltOnDead;
    public Color tint = Color.white;
    SpriteRenderer sr;
    float t;
    const float FallDur = 0.38f;
    bool settled;

    void Awake() { sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        if (settled || sr == null) return;
        t += Time.deltaTime;
        float targetAng = tiltOnDead ? (flipX ? -82f : 82f) : 0f;
        if (t < FallDur)
        {
            if (fallFrames != null && fallFrames.Length > 0)
            {
                int idx = Mathf.Min(fallFrames.Length - 1, Mathf.FloorToInt((t / FallDur) * fallFrames.Length));
                sr.sprite = fallFrames[idx];
            }
            if (tiltOnDead)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, targetAng, t / FallDur));
            sr.flipX = flipX;
            sr.color = tint;
        }
        else
        {
            settled = true;
            if (deadFrames != null && deadFrames.Length > 0)
                sr.sprite = deadFrames[Random.Range(0, deadFrames.Length)];
            if (tiltOnDead) transform.rotation = Quaternion.Euler(0f, 0f, targetAng);
            sr.flipX = flipX;
            sr.color = tint * 0.9f;
            sr.sortingOrder = -120;
        }
    }
}

public class Bullet : MonoBehaviour
{
    public Vector2 dir;
    public float dmg, life = 0.55f, speed = 26f;

    void Update()
    {
        float step = speed * Time.deltaTime;
        var hit = Physics2D.Raycast(transform.position, dir, step + 0.08f);
        if (hit.collider != null)
        {
            var z = hit.collider.GetComponent<Zombie>();
            if (z != null) z.Hurt(dmg, dir * 4.5f);
            else Player.Flash(hit.point, 0.32f, new Color(1f, 0.85f, 0.45f, 0.7f), 0.06f);
            Destroy(gameObject);
            return;
        }
        transform.position += (Vector3)(dir * step);
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }
}

public class Zombie : MonoBehaviour
{
    public static readonly List<Zombie> Active = new List<Zombie>();

    public string id;
    public float hp = 4f, sight = 6f;
    public AnimSet set;
    public bool faceLeft;

    Rigidbody2D rb;
    Transform gfx;
    SpriteRenderer sr;
    bool aggro;
    float lostT, nextBite, flashT, wanderT, speed, atkUntil, phase;
    Vector2 wanderDir, kb, moveVel;

    void OnEnable() { Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    void Awake()
    {
        var sh = new GameObject("shadow");
        sh.transform.SetParent(transform, false);
        sh.transform.localPosition = new Vector3(0, -0.32f, 0);
        sh.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        var ssr = sh.AddComponent<SpriteRenderer>();
        ssr.sprite = Sprites.Get("shadow");
        ssr.sortingOrder = -100;

        gfx = new GameObject("gfx").transform;
        gfx.SetParent(transform, false);
        sr = gfx.gameObject.AddComponent<SpriteRenderer>();
        if (Sheets.ZSets != null && Sheets.ZSets.Length > 0)
        {
            set = Sheets.ZSets[Random.Range(0, Sheets.ZSets.Length)];
            sr.sprite = set.idle[0];
            gfx.localPosition = new Vector3(0, -0.32f, 0);
            gfx.localScale = Vector3.one * set.scale;
        }
        else
        {
            sr.sprite = Sprites.Get("zombie");
            gfx.localPosition = new Vector3(0, -0.32f, 0);
        }

        phase = Random.value * 4f;
        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var cc = gameObject.AddComponent<CircleCollider2D>();
        cc.radius = 0.28f;

        speed = Random.Range(1.55f, 2.2f);
        wanderT = Random.Range(0f, 2f);
    }

    public void Hurt(float d, Vector2 push)
    {
        hp -= d;
        aggro = true;
        lostT = Time.time + 8f;
        flashT = 0.22f;
        kb += push;
        Sfx.PlayHit();
        Player.Flash((Vector2)transform.position + Random.insideUnitCircle * 0.2f, 0.45f, new Color(0.85f, 0.08f, 0.08f, 0.85f), 0.14f);
        if (hp <= 0f)
        {
            Game.I.OnZombieDeath(this);
            Destroy(gameObject);
        }
    }

    void Update()
    {
        var g = Game.I;
        if (flashT > 0f)
        {
            flashT -= Time.deltaTime;
            sr.color = Color.Lerp(g.def.tint, Color.red, 0.8f);
        }
        else sr.color = g.def.tint;

        sr.sortingOrder = DepthSort.Order(transform.position.y - 0.32f);

        if (g.state != GState.Play) { moveVel = Vector2.zero; return; }

        Vector2 p = transform.position, pp = g.player.transform.position;
        float d = Vector2.Distance(p, pp);
        int wall = 1 << Game.LWall;
        bool los = !Physics2D.Linecast(p, pp, wall);
        if (d < sight && los) { aggro = true; lostT = Time.time + 6f; }
        if (aggro && Time.time > lostT) aggro = false;

        Vector2 mv = Vector2.zero;
        float sp = speed;
        if (aggro)
        {
            if (d < 2.2f && los) mv = (pp - p).normalized;
            else if (g.NextStep(p, out var tgt)) mv = (tgt - p).normalized;
        }
        else
        {
            sp = speed * 0.35f;
            if (Time.time > wanderT)
            {
                wanderT = Time.time + Random.Range(1.5f, 3.5f);
                wanderDir = Random.value < 0.4f ? Vector2.zero : Random.insideUnitCircle.normalized;
            }
            mv = wanderDir;
        }

        // Soft separation from nearby zombies so crowds don't stack into one sprite
        Vector2 sep = Vector2.zero;
        for (int i = 0; i < Active.Count; i++)
        {
            var o = Active[i];
            if (o == null || o == this) continue;
            Vector2 diff = p - (Vector2)o.transform.position;
            float dist = diff.magnitude;
            if (dist > 0.001f && dist < 0.65f)
                sep += diff / dist * ((0.65f - dist) / 0.65f * 1.8f);
        }

        kb = Vector2.Lerp(kb, Vector2.zero, 8f * Time.deltaTime);
        moveVel = mv * sp + sep + kb;

        faceLeft = (aggro || Mathf.Abs(mv.x) < 0.02f) ? pp.x < p.x : mv.x < 0f;
        UpdateAnim(mv);

        if (d < 0.85f && Time.time > nextBite)
        {
            nextBite = Time.time + 0.9f;
            atkUntil = Time.time + 0.35f;
            g.player.Hurt(10f);
        }
    }

    void FixedUpdate()
    {
        if (rb != null) rb.linearVelocity = moveVel;
    }

    void UpdateAnim(Vector2 mv)
    {
        sr.flipX = faceLeft;
        if (set == null) return;

        Sprite[] clip;
        float rate;
        if (flashT > 0f && set.hurt != null)
        {
            clip = set.hurt; rate = 8f;
            gfx.localRotation = Quaternion.Euler(0f, 0f, faceLeft ? -14f : 14f);
            gfx.localScale = Vector3.one * set.scale;
        }
        else if (Time.time < atkUntil && set.attack != null)
        {
            clip = set.attack; rate = 8f;
            gfx.localRotation = Quaternion.Euler(0f, 0f, faceLeft ? 12f : -12f);
            gfx.localScale = Vector3.one * (set.scale * 1.07f);
        }
        else
        {
            gfx.localRotation = Quaternion.identity;
            gfx.localScale = Vector3.one * set.scale;
            if (mv.sqrMagnitude > 0.01f)
            {
                clip = (aggro && set.run != null && set.run.Length > 0) ? set.run : set.walk;
                rate = aggro ? 7f : 4.5f;
            }
            else { clip = set.idle ?? set.walk; rate = 2f; }
        }

        if (clip != null && clip.Length > 0)
            sr.sprite = clip[(int)(Time.time * rate + phase) % clip.Length];
    }
}

public class Player : MonoBehaviour
{
    public static readonly string[] WNames = { "Дубинка", "Пистолет", "Дробовик", "Автомат" };
    static readonly float[] Cd = { 0.42f, 0.32f, 0.85f, 0.10f };

    public Rigidbody2D rb;
    public Vector2 aim = Vector2.right;

    Transform gfx, flashlight;
    SpriteRenderer sr, flashSr;
    Vector2 moveInput, dashDir;
    bool running, faceLeft;
    float nextAtk, nextGren, nextDash, dashUntil, invUntil, stepT, walkAnimT, ghostT;

    AnimClip clipKind;
    float clipStart = -10f, clipDur;

    void Awake()
    {
        var sh = new GameObject("shadow");
        sh.transform.SetParent(transform, false);
        sh.transform.localPosition = new Vector3(0, -0.32f, 0);
        sh.transform.localScale = new Vector3(0.72f, 0.72f, 1f);
        var ssr = sh.AddComponent<SpriteRenderer>();
        ssr.sprite = Sprites.Get("shadow");
        ssr.sortingOrder = -100;

        flashlight = new GameObject("flashlight").transform;
        flashlight.SetParent(transform, false);
        flashlight.localPosition = new Vector3(0, -0.05f, 0);
        flashSr = flashlight.gameObject.AddComponent<SpriteRenderer>();
        flashSr.sprite = Sprites.Get("cone");
        flashSr.sortingOrder = -85;
        flashSr.color = new Color(1f, 0.95f, 0.8f, 0.85f);

        gfx = new GameObject("gfx").transform;
        gfx.SetParent(transform, false);
        gfx.localPosition = new Vector3(0, -0.32f, 0);
        sr = gfx.gameObject.AddComponent<SpriteRenderer>();
        var set = Sheets.PlayerForWeapon(0);
        if (set != null) { sr.sprite = set.idle[0]; gfx.localScale = Vector3.one * set.scale; }
        else sr.sprite = Sprites.Get("player");

        rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.mass = 2f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var cc = gameObject.AddComponent<CircleCollider2D>();
        cc.radius = 0.28f;
    }

    public void PlayClip(AnimClip clip, float dur)
    {
        clipKind = clip;
        clipStart = Time.time;
        clipDur = dur;
    }

    public void Hurt(float d)
    {
        var g = Game.I;
        if (g.state != GState.Play || Time.time < invUntil || Time.time < dashUntil) return;
        if (g.armor) d *= 0.5f;
        g.hp -= d;
        g.hurtFlash = 0.35f;
        invUntil = Time.time + 0.25f;
        PlayClip(AnimClip.Hurt, 0.28f);
        Sfx.PlayBite();
        g.Shake(0.14f);
        if (g.hp <= 0f) g.Die();
    }

    void Update()
    {
        var g = Game.I;
        sr.sortingOrder = DepthSort.Order(transform.position.y - 0.32f);
        sr.color = g.def != null ? g.def.tint : Color.white;
        if (flashSr != null) flashSr.enabled = g.state == GState.Play || g.state == GState.Dead;

        if (g.state != GState.Play)
        {
            moveInput = Vector2.zero;
            running = false;
            g.prompt = "";
            UpdateAnim(Vector2.zero);
            return;
        }

        // Combine direct WASD / Arrow key polling + Input Axes so movement works in every Unity 6 input setup
        float hx = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                 - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float hy = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                 - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        if (hx == 0f) hx = Input.GetAxisRaw("Horizontal");
        if (hy == 0f) hy = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(hx, hy);
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

        Vector3 m = g.cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toM = (Vector2)m - (Vector2)transform.position;
        if (toM.sqrMagnitude > 0.01f) aim = toM.normalized;

        if (flashlight != null)
            flashlight.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);

        if (Mathf.Abs(aim.x) > 0.08f) faceLeft = aim.x < 0f;
        else if (Mathf.Abs(moveInput.x) > 0.05f) faceLeft = moveInput.x < 0f;

        bool shiftDown = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        running = !g.dash && shiftHeld && moveInput.sqrMagnitude > 0.01f;

        if (g.dash && shiftDown && Time.time > nextDash && moveInput.sqrMagnitude > 0.01f)
        {
            dashUntil = Time.time + 0.16f;
            nextDash = Time.time + 1.1f;
            dashDir = moveInput;
            ghostT = 0f;
            Sfx.PlayBat();
        }

        if (Time.time < dashUntil)
        {
            ghostT -= Time.deltaTime;
            if (ghostT <= 0f)
            {
                ghostT = 0.035f;
                GhostTrail.Spawn(sr.sprite, sr.flipX, gfx.position, gfx.localScale, sr.sortingOrder);
            }
        }

        UpdateAnim(moveInput);

        // Footstep sound & dust puffs
        if (moveInput.sqrMagnitude > 0.01f && Time.time >= dashUntil)
        {
            stepT -= Time.deltaTime;
            if (stepT <= 0f)
            {
                stepT = running ? 0.22f : 0.30f;
                Sfx.PlayStep();
                Flash((Vector2)transform.position + new Vector2(Random.Range(-0.12f, 0.12f), -0.30f),
                    0.22f, new Color(0.85f, 0.8f, 0.7f, 0.35f), 0.25f);
            }
        }
        else stepT = 0.05f;

        // Weapon switching
        for (int i = 0; i < 4; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) && g.hasW[i]) g.cur = i;
        float sc = Input.mouseScrollDelta.y;
        if (sc != 0f)
        {
            int n = g.cur;
            for (int k = 0; k < 4; k++)
            {
                n = (n + (sc > 0f ? 1 : 3)) % 4;
                if (g.hasW[n]) { g.cur = n; break; }
            }
        }

        // Attack & abilities
        if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.F)) && Time.time > nextAtk) MeleeSwing(transform.position);
        bool fire = g.cur == 3 ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (fire && Time.time > nextAtk) Attack();
        if (Input.GetKeyDown(KeyCode.G)) Grenade();
        if (Input.GetKeyDown(KeyCode.H)) g.UseMedkit();

        // Nearest interactable
        Interactable best = null;
        float bd = float.MaxValue;
        foreach (var it in g.inters)
        {
            if (it == null) continue;
            float d = Vector2.Distance(it.transform.position, transform.position);
            if (d < it.range && d < bd) { bd = d; best = it; }
        }
        g.prompt = best != null ? "E — " + best.prompt : "";
        if (best != null && Input.GetKeyDown(KeyCode.E) && best.use != null) best.use();
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        var g = Game.I;
        if (g == null || g.state != GState.Play) { rb.linearVelocity = Vector2.zero; return; }
        if (Time.time < dashUntil) { rb.linearVelocity = dashDir * 12f; return; }
        float spd = running ? 4.9f : 3.8f;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, moveInput * spd, 34f * Time.fixedDeltaTime);
    }

    void UpdateAnim(Vector2 mv)
    {
        var set = Sheets.PlayerForWeapon(Game.I != null ? Game.I.cur : 0);
        sr.flipX = faceLeft;
        if (set == null) return;

        float bob = 1f;
        Sprite spr = null;

        if (Time.time < clipStart + clipDur)
        {
            Sprite[] arr = null;
            switch (clipKind)
            {
                case AnimClip.Pickup: arr = set.pickup; break;
                case AnimClip.Attack: arr = set.attack; break;
                case AnimClip.Shoot: arr = set.shoot; break;
                case AnimClip.Victory: arr = set.victory; break;
                case AnimClip.Hurt: arr = set.hurt; break;
                case AnimClip.Fall: arr = set.fall; break;
                case AnimClip.Dead: arr = set.dead; break;
            }
            if (arr != null && arr.Length > 0)
            {
                float u = Mathf.Clamp01((Time.time - clipStart) / Mathf.Max(0.01f, clipDur));
                int idx = Mathf.Min(arr.Length - 1, (int)(u * arr.Length));
                spr = arr[idx];
            }
        }
        else if (mv.sqrMagnitude > 0.01f)
        {
            walkAnimT += Time.deltaTime * (running ? 13f : 9.5f);
            var walkArr = (running && set.run != null && set.run.Length > 0) ? set.run : set.walk;
            if (walkArr != null && walkArr.Length > 0) spr = walkArr[(int)walkAnimT % walkArr.Length];
        }
        else
        {
            walkAnimT = 0f;
            var idleArr = set.idle ?? set.walk;
            if (idleArr != null && idleArr.Length > 0) spr = idleArr[(int)(Time.time * 2.5f) % idleArr.Length];
            bob = 1f + Mathf.Sin(Time.time * 3.2f) * 0.025f;
        }

        if (spr != null) sr.sprite = spr;
        gfx.localScale = new Vector3(set.scale, set.scale * bob, 1f);
    }

    void MeleeSwing(Vector2 p)
    {
        nextAtk = Time.time + Cd[0];
        PlayClip(AnimClip.Attack, 0.38f);
        Sfx.PlayBat();

        var slash = new GameObject("slash");
        slash.transform.position = p + aim * 0.42f;
        slash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        var ssr = slash.AddComponent<SpriteRenderer>();
        ssr.sprite = Sprites.Get("slash");
        ssr.sortingOrder = sr.sortingOrder + 2;
        Destroy(slash, 0.12f);

        bool landed = false;
        foreach (var c in Physics2D.OverlapCircleAll(p + aim * 0.65f, 1.15f))
        {
            var z = c.GetComponent<Zombie>();
            if (z == null) continue;
            if (Vector2.Angle(aim, (Vector2)z.transform.position - p) < 80f)
            {
                z.Hurt(1.6f, aim * 6.5f);
                landed = true;
            }
        }
        if (landed) Game.I.Shake(0.08f);
    }

    void Attack()
    {
        var g = Game.I;
        int w = g.cur;
        Vector2 p = transform.position;

        if (w == 0) { MeleeSwing(p); return; }

        int cost = w == 2 ? 2 : 1;
        if (g.ammo < cost)
        {
            if (w == 2 && g.ammo >= 1 && g.hasW[1])
            {
                g.cur = 1; w = 1; cost = 1;
                g.Msg("Мало патронов для дробовика — переключено на пистолет!", 2f);
            }
            else
            {
                g.cur = 0;
                g.Msg("Патроны закончились — переключено на дубинку (рукопашка)!", 2.5f);
                MeleeSwing(p);
                return;
            }
        }

        nextAtk = Time.time + Cd[w];
        g.ammo -= cost;
        PlayClip(AnimClip.Shoot, 0.18f);
        Sfx.PlayShot(w);
        g.Shake(w == 2 ? 0.16f : 0.06f);

        int n = w == 2 ? 6 : 1;
        float spread = w == 2 ? 16f : (w == 3 ? 6f : 1.5f);
        float dmg = w == 1 ? 2.2f : 1.25f;
        for (int i = 0; i < n; i++)
        {
            float a = Random.Range(-spread, spread);
            Vector2 dir = Quaternion.Euler(0f, 0f, a) * aim;
            var b = new GameObject("bullet");
            b.transform.position = p + aim * 0.45f;
            var s = b.AddComponent<SpriteRenderer>();
            s.sprite = Sprites.Get("dot");
            s.sortingOrder = 2100;
            var bl = b.AddComponent<Bullet>();
            bl.dir = dir;
            bl.dmg = dmg;
        }
        Flash(p + aim * 0.55f, 0.7f, new Color(1f, 0.9f, 0.5f, 0.85f), 0.05f);

        if (g.ammo <= 0)
        {
            g.cur = 0;
            g.Msg("Патроны закончились — в руках снова дубинка!", 2.5f);
        }
    }

    void Grenade()
    {
        var g = Game.I;
        if (g.grenades <= 0 || Time.time < nextGren) return;
        g.grenades--;
        nextGren = Time.time + 1f;
        Vector3 m = g.cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 p = transform.position;
        float dist = Mathf.Min(6.5f, Vector2.Distance(m, p));
        var hit = Physics2D.Raycast(p, aim, dist, 1 << Game.LWall);
        if (hit.collider != null) dist = Mathf.Max(0.5f, hit.distance - 0.4f);
        g.StartCoroutine(Boom(p + aim * dist));
    }

    IEnumerator Boom(Vector2 at)
    {
        Flash(at, 0.5f, Color.green, 0.9f);
        yield return new WaitForSeconds(0.9f);
        Sfx.PlayBoom();
        Game.I.Shake(0.32f);
        Flash(at, 5.4f, new Color(1f, 0.6f, 0.1f, 0.85f), 0.25f);
        foreach (var c in Physics2D.OverlapCircleAll(at, 2.7f))
        {
            var z = c.GetComponent<Zombie>();
            if (z != null) z.Hurt(8.5f, ((Vector2)z.transform.position - at).normalized * 8.5f);
        }
        if (Vector2.Distance(at, transform.position) < 1.8f) { invUntil = 0f; Hurt(25f); }
    }

    public static GameObject Flash(Vector2 pos, float scale, Color col, float life)
    {
        var f = new GameObject("fx");
        f.transform.position = pos;
        f.transform.localScale = Vector3.one * scale;
        var s = f.AddComponent<SpriteRenderer>();
        s.sprite = Sprites.Get("blob");
        s.color = col;
        s.sortingOrder = 2200;
        Destroy(f, life);
        return f;
    }
}
