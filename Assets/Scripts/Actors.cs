using System.Collections;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    public string prompt; public System.Action use; public float range = 1.35f;
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
    public char kind; public string id;
    void OnTriggerEnter2D(Collider2D o)
    {
        if (o.GetComponent<Player>() == null) return;
        Game.I.Collect(kind, id);
        Destroy(gameObject);
    }
}

public class Corpse : MonoBehaviour
{
    public Sprite dead; float t = 0.3f;
    void Update() { t -= Time.deltaTime; if (t <= 0 && dead != null) { GetComponent<SpriteRenderer>().sprite = dead; dead = null; } }
}

public class Bullet : MonoBehaviour
{
    public Vector2 dir; public float dmg, life = 0.5f, speed = 24f;
    void Update()
    {
        float step = speed * Time.deltaTime;
        var hit = Physics2D.Raycast(transform.position, dir, step + 0.05f);
        if (hit.collider != null)
        {
            var z = hit.collider.GetComponent<Zombie>();
            if (z != null) z.Hurt(dmg, dir * 4f);
            Destroy(gameObject); return;
        }
        transform.position += (Vector3)(dir * step);
        life -= Time.deltaTime; if (life <= 0) Destroy(gameObject);
    }
}

public class Zombie : MonoBehaviour
{
    public string id; public float hp = 4, sight = 6f;
    public Sprite[] frames;
    Rigidbody2D rb; SpriteRenderer sr;
    bool aggro, faceLeft; float lostT, nextBite, flashT, wanderT, speed, atkUntil, phase; Vector2 wanderDir, kb;

    void Awake()
    {
        var gfx = new GameObject("gfx"); gfx.transform.SetParent(transform, false);
        sr = gfx.AddComponent<SpriteRenderer>(); sr.sortingOrder = 8;
        if (Sheets.Z != null) { frames = Sheets.Z[Random.Range(0, 5)]; sr.sprite = frames[0]; gfx.transform.localPosition = new Vector3(0, -0.32f, 0); }
        else sr.sprite = Sprites.Get("zombie");
        phase = Random.value * 4f;
        rb = gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.freezeRotation = true; rb.linearDamping = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var cc = gameObject.AddComponent<CircleCollider2D>(); cc.radius = 0.3f;
        speed = Random.Range(1.5f, 2.1f);
        wanderT = Random.Range(0f, 2f);
    }

    public void Hurt(float d, Vector2 push)
    {
        hp -= d; aggro = true; lostT = Time.time + 8f; flashT = 0.2f; kb += push;
        if (hp <= 0) { Game.I.OnZombieDeath(this); Destroy(gameObject); }
    }

    void Update()
    {
        var g = Game.I;
        if (flashT > 0) { flashT -= Time.deltaTime; sr.color = Color.Lerp(g.def.tint, Color.red, 0.8f); } else sr.color = g.def.tint;
        if (g.state != GState.Play) { rb.linearVelocity = Vector2.zero; return; }
        Vector2 p = transform.position, pp = g.player.transform.position;
        float d = Vector2.Distance(p, pp);
        int wall = 1 << Game.LWall;
        bool los = !Physics2D.Linecast(p, pp, wall);
        if (d < sight && los) { aggro = true; lostT = Time.time + 6f; }
        if (aggro && Time.time > lostT) aggro = false;

        Vector2 mv = Vector2.zero; float sp = speed;
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
        kb = Vector2.Lerp(kb, Vector2.zero, 8f * Time.deltaTime);
        rb.linearVelocity = mv * sp + kb;
        faceLeft = (aggro || mv.x == 0) ? pp.x < p.x : mv.x < 0;
        if (frames == null) sr.flipX = faceLeft;
        else
        {
            int i;
            if (Time.time < atkUntil) i = 4;
            else if (flashT > 0) i = 5;
            else
            {
                i = faceLeft ? 0 : 2;
                if (mv.sqrMagnitude > 0.01f) i += ((int)(Time.time * 4f + phase)) % 2;
            }
            sr.sprite = frames[i];
            sr.flipX = i == 4 && faceLeft;   // attack frame faces right
        }

        if (d < 0.85f && Time.time > nextBite)
        {
            nextBite = Time.time + 0.9f; atkUntil = Time.time + 0.35f;
            g.player.Hurt(10f);
        }
    }
}

public class Player : MonoBehaviour
{
    public static readonly string[] WNames = { "Дубинка", "Пистолет", "Дробовик", "Автомат" };
    static readonly float[] Cd = { 0.45f, 0.35f, 0.9f, 0.11f };

    public Rigidbody2D rb; SpriteRenderer sr;
    public Vector2 aim = Vector2.right;
    float nextAtk, nextGren, nextDash, dashUntil, invUntil; Vector2 dashDir;
    // animation from the sprite sheet rows: 0 walk, 1 pick up, 2 attack, 3 victory, 4 damage/killed
    int animRow, animFrom, animTo; float animStart = -10f, animDur;
    const bool WalkLeftFirst = true; // set false if the walk row's first 4 frames face right

    public void Play(int row, float dur, int from = 0, int to = 7) { animRow = row; animFrom = from; animTo = to; animStart = Time.time; animDur = dur; }

    void UpdateAnim(Vector2 mv)
    {
        var P = Sheets.Pl;
        if (P == null) { sr.flipX = aim.x < 0; return; }
        Sprite[] f; int i; bool flip = false;
        if (Time.time < animStart + animDur)
        {
            f = P[animRow]; int n = animTo - animFrom + 1;
            float t = (Time.time - animStart) / animDur;
            i = animFrom + Mathf.Min(n - 1, (int)(t * n));
            flip = aim.x < 0 && animRow == 2;  // attack frames face right
        }
        else
        {
            f = P[0]; bool left = aim.x < 0; int b = (left == WalkLeftFirst) ? 0 : 4;
            i = b + (mv.sqrMagnitude > 0.01f ? ((int)(Time.time * 8f)) % 4 : 0);
        }
        sr.sprite = f[Mathf.Clamp(i, 0, f.Length - 1)]; sr.flipX = flip;
    }

    void Awake()
    {
        var gfx = new GameObject("gfx"); gfx.transform.SetParent(transform, false);
        sr = gfx.AddComponent<SpriteRenderer>(); sr.sortingOrder = 9;
        if (Sheets.Pl != null) { sr.sprite = Sheets.Pl[0][4]; gfx.transform.localPosition = new Vector3(0, -0.32f, 0); }
        else sr.sprite = Sprites.Get("player");
        rb = gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.freezeRotation = true; rb.mass = 2;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var cc = gameObject.AddComponent<CircleCollider2D>(); cc.radius = 0.32f;
    }

    public void Hurt(float d)
    {
        var g = Game.I;
        if (g.state != GState.Play || Time.time < invUntil || Time.time < dashUntil) return;
        if (g.armor) d *= 0.5f;
        g.hp -= d; g.hurtFlash = 0.35f; invUntil = Time.time + 0.2f;
        Play(4, 0.25f, 0, 1);
        if (g.hp <= 0) g.Die();
    }

    void Update()
    {
        var g = Game.I;
        if (g.state != GState.Play) { rb.linearVelocity = Vector2.zero; g.prompt = ""; UpdateAnim(Vector2.zero); sr.color = g.def.tint; return; }

        Vector2 mv = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (mv.sqrMagnitude > 1) mv.Normalize();
        Vector3 m = g.cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toM = (Vector2)m - (Vector2)transform.position;
        if (toM.sqrMagnitude > 0.01f) aim = toM.normalized;
        UpdateAnim(mv); sr.color = g.def.tint;

        if (g.dash && Input.GetKeyDown(KeyCode.LeftShift) && Time.time > nextDash && mv != Vector2.zero)
        { dashUntil = Time.time + 0.16f; nextDash = Time.time + 1.2f; dashDir = mv; }
        rb.linearVelocity = Time.time < dashUntil ? dashDir * 11f : mv * 3.4f;

        // weapon switching
        for (int i = 0; i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i) && g.hasW[i]) g.cur = i;
        float sc = Input.mouseScrollDelta.y;
        if (sc != 0) { int n = g.cur; for (int k = 0; k < 4; k++) { n = (n + (sc > 0 ? 1 : 3)) % 4; if (g.hasW[n]) { g.cur = n; break; } } }

        // attack
        bool fire = g.cur == 3 ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (fire && Time.time > nextAtk) Attack();
        if (Input.GetKeyDown(KeyCode.G)) Grenade();
        if (Input.GetKeyDown(KeyCode.H)) g.UseMedkit();

        // interact
        Interactable best = null; float bd = 1e9f;
        foreach (var it in g.inters)
        {
            if (it == null) continue;
            float d = Vector2.Distance(it.transform.position, transform.position);
            if (d < it.range && d < bd) { bd = d; best = it; }
        }
        g.prompt = best != null ? "E — " + best.prompt : "";
        if (best != null && Input.GetKeyDown(KeyCode.E) && best.use != null) best.use();
    }

    void Attack()
    {
        var g = Game.I; int w = g.cur;
        nextAtk = Time.time + Cd[w];
        Vector2 p = transform.position;
        if (w == 0)
        {
            Play(2, 0.4f);
            Flash(p + aim * 0.9f, 1.1f, new Color(1, 1, 1, .6f), 0.08f);
            foreach (var c in Physics2D.OverlapCircleAll(p + aim * 0.6f, 1.0f))
            {
                var z = c.GetComponent<Zombie>(); if (z == null) continue;
                if (Vector2.Angle(aim, (Vector2)z.transform.position - p) < 75f) z.Hurt(1.5f, aim * 6f);
            }
            return;
        }
        int cost = w == 2 ? 2 : 1;
        if (g.ammo < cost) { g.Msg("Нет патронов!", 1f); nextAtk = Time.time + 0.4f; return; }
        g.ammo -= cost;
        int n = w == 2 ? 6 : 1; float spread = w == 2 ? 16f : (w == 3 ? 6f : 1.5f); float dmg = w == 1 ? 2f : (w == 2 ? 1.2f : 1.2f);
        for (int i = 0; i < n; i++)
        {
            float a = Random.Range(-spread, spread);
            Vector2 dir = Quaternion.Euler(0, 0, a) * aim;
            var b = new GameObject("bullet"); b.transform.position = p + aim * 0.45f;
            var s = b.AddComponent<SpriteRenderer>(); s.sprite = Sprites.Get("dot"); s.sortingOrder = 10;
            var bl = b.AddComponent<Bullet>(); bl.dir = dir; bl.dmg = dmg;
        }
        Flash(p + aim * 0.6f, 0.8f, new Color(1f, .9f, .5f, .8f), 0.04f);
    }

    void Grenade()
    {
        var g = Game.I;
        if (g.grenades <= 0 || Time.time < nextGren) return;
        g.grenades--; nextGren = Time.time + 1f;
        Vector3 m = g.cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 p = transform.position;
        float dist = Mathf.Min(6.5f, Vector2.Distance(m, p));
        var hit = Physics2D.Raycast(p, aim, dist, 1 << Game.LWall);
        if (hit.collider != null) dist = Mathf.Max(0.5f, hit.distance - 0.4f);
        g.StartCoroutine(Boom(p + aim * dist));
    }

    IEnumerator Boom(Vector2 at)
    {
        var o = Flash(at, 0.5f, Color.green, 0.9f);
        yield return new WaitForSeconds(0.9f);
        Flash(at, 5.2f, new Color(1f, .6f, .1f, .8f), 0.25f);
        foreach (var c in Physics2D.OverlapCircleAll(at, 2.6f))
        {
            var z = c.GetComponent<Zombie>(); if (z != null) z.Hurt(8f, ((Vector2)z.transform.position - at).normalized * 8f);
        }
        if (Vector2.Distance(at, transform.position) < 1.8f) { invUntil = 0; Hurt(25f); }
    }

    public static GameObject Flash(Vector2 pos, float scale, Color col, float life)
    {
        var f = new GameObject("fx"); f.transform.position = pos; f.transform.localScale = Vector3.one * scale;
        var s = f.AddComponent<SpriteRenderer>(); s.sprite = Sprites.Get("blob"); s.color = col; s.sortingOrder = 11;
        Destroy(f, life); return f;
    }
}
