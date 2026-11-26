using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Procedural 22050 Hz horror sound effects and dark ambient drone. Zero external audio assets needed.</summary>
public class Sfx : MonoBehaviour
{
    public static Sfx I;
    public static bool Muted;

    const int Rate = 22050;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    AudioSource oneShot, ambSrc, alarmSrc;
    bool ready, alarmActive;
    float alarmNext;

    public static void InitOn(GameObject host)
    {
        if (I != null) return;
        I = host.AddComponent<Sfx>();
    }

    void Awake()
    {
        I = this;
        if (Application.isBatchMode) return;
        try
        {
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            oneShot = gameObject.AddComponent<AudioSource>();
            oneShot.playOnAwake = false;

            ambSrc = gameObject.AddComponent<AudioSource>();
            ambSrc.loop = true;
            ambSrc.volume = 0.08f;

            alarmSrc = gameObject.AddComponent<AudioSource>();
            alarmSrc.playOnAwake = false;
            alarmSrc.volume = 0.045f;

            BuildAllClips();
            if (clips.TryGetValue("ambient", out var amb)) { ambSrc.clip = amb; ambSrc.Play(); }
            ready = true;
        }
        catch (Exception e) { Debug.LogWarning("Sfx disabled: " + e.Message); ready = false; }
    }

    void Update()
    {
        if (!ready || Muted || !alarmActive) return;
        if (Time.unscaledTime >= alarmNext)
        {
            alarmNext = Time.unscaledTime + 3.4f;
            if (clips.TryGetValue("alarm", out var c)) alarmSrc.PlayOneShot(c, 0.45f);
        }
    }

    void BuildAllClips()
    {
        var rng = new System.Random(42);
        float Rnd() => (float)(rng.NextDouble() * 2.0 - 1.0);

        // Step: short muffled low-pass thud
        clips["step"] = Make("step", 0.09f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 36f);
            return (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(130f, 55f, t / d) * t) * 0.7f + Rnd() * 0.25f) * env * 0.45f;
        }, lowPass: 0.35f);

        // Bat: whoosh sweep
        clips["bat"] = Make("bat", 0.18f, (t, d) =>
        {
            float env = Mathf.Sin(Mathf.PI * (t / d));
            float f = Mathf.Lerp(180f, 520f, t / d);
            return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.35f + Rnd() * 0.55f) * env * 0.55f;
        }, lowPass: 0.45f);

        // Hit: meaty crunch
        clips["hit"] = Make("hit", 0.14f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 24f);
            float f = Mathf.Lerp(190f, 65f, t / d);
            return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.65f + Rnd() * 0.6f) * env * 0.7f;
        }, lowPass: 0.55f);

        // Pistol: crisp snap + low kick
        clips["pistol"] = Make("pistol", 0.17f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 28f);
            float kick = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(260f, 70f, t / d) * t) * Mathf.Exp(-t * 45f);
            return (Rnd() * 0.75f + kick * 0.65f) * env * 0.75f;
        }, lowPass: 0.75f);

        // Shotgun: heavy double-layered blast
        clips["shotgun"] = Make("shotgun", 0.30f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 14f);
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 45f, t / d) * t);
            return (Rnd() * 0.8f + boom * 0.5f) * env * 0.85f;
        }, lowPass: 0.60f);

        // Rifle: tight punchy bark
        clips["rifle"] = Make("rifle", 0.12f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 34f);
            float bark = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 95f, t / d) * t);
            return (Rnd() * 0.7f + bark * 0.55f) * env * 0.65f;
        }, lowPass: 0.80f);

        // Boom: deep explosion rumble
        clips["boom"] = Make("boom", 0.75f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 4.5f);
            float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(95f, 28f, t / d) * t);
            return (Rnd() * 0.75f + sub * 0.75f) * env * 0.9f;
        }, lowPass: 0.32f);

        // Bite: low zombie snarl/bite
        clips["bite"] = Make("bite", 0.28f, (t, d) =>
        {
            float env = Mathf.Sin(Mathf.PI * (t / d)) * Mathf.Exp(-t * 4f);
            float f = 92f + Mathf.Sin(t * 48f) * 18f;
            float saw = 2f * ((f * t) - Mathf.Floor(0.5f + f * t));
            return (saw * 0.55f + Rnd() * 0.3f) * env * 0.7f;
        }, lowPass: 0.45f);

        // Pickup: 2-note warm chime
        clips["pickup"] = Make("pickup", 0.24f, (t, d) =>
        {
            float f = t < 0.10f ? 523.25f : 783.99f;
            float lt = t < 0.10f ? t : t - 0.10f;
            return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-lt * 16f) * 0.45f;
        });

        // Click: crisp safe dial / terminal key tick
        clips["click"] = Make("click", 0.045f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 95f);
            return (Mathf.Sin(2f * Mathf.PI * 1450f * t) * 0.6f + Rnd() * 0.4f) * env * 0.5f;
        });

        // Unlock: 3-note mechanical unlock chord
        clips["unlock"] = Make("unlock", 0.42f, (t, d) =>
        {
            float f = t < 0.11f ? 440f : (t < 0.22f ? 554.37f : 659.25f);
            float lt = t < 0.11f ? t : (t < 0.22f ? t - 0.11f : t - 0.22f);
            return (Mathf.Sin(2f * Mathf.PI * f * t) + Mathf.Sin(2f * Mathf.PI * f * 1.5f * t) * 0.25f) * Mathf.Exp(-lt * 10f) * 0.4f;
        });

        // Error: low buzzer
        clips["error"] = Make("error", 0.26f, (t, d) =>
        {
            float env = (t < 0.11f || t > 0.14f) ? Mathf.Exp(-(t > 0.14f ? t - 0.14f : t) * 14f) : 0.05f;
            float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 135f * t));
            return sq * env * 0.32f;
        }, lowPass: 0.4f);

        // Lever: heavy metallic clank
        clips["lever"] = Make("lever", 0.18f, (t, d) =>
        {
            float env = Mathf.Exp(-t * 22f);
            float clank = Mathf.Sin(2f * Mathf.PI * 310f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 740f * t) * 0.35f;
            return (clank + Rnd() * 0.35f) * env * 0.6f;
        });

        // Door: creaky latch
        clips["door"] = Make("door", 0.26f, (t, d) =>
        {
            float env = Mathf.Sin(Mathf.PI * (t / d));
            float f = Mathf.Lerp(210f, 340f, t / d) + Mathf.Sin(t * 70f) * 22f;
            return (Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f + Rnd() * 0.2f) * env * 0.45f;
        }, lowPass: 0.5f);

        // Distant siren pulse for 0 floor alarm
        clips["alarm"] = Make("alarm", 0.9f, (t, d) =>
        {
            float env = Mathf.Sin(Mathf.PI * (t / d));
            float f = Mathf.Lerp(480f, 640f, Mathf.Sin(Mathf.PI * (t / d)));
            return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.35f;
        }, lowPass: 0.45f);

        // Ambient: subtle dark underground hum
        clips["ambient"] = Make("ambient", 4.0f, (t, d) =>
        {
            float fade = Mathf.Min(1f, Mathf.Min(t, d - t) * 4f);
            float drone = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.55f + Mathf.Sin(2f * Mathf.PI * 78f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 104f * t) * 0.12f;
            return (drone + Rnd() * 0.05f) * fade * 0.6f;
        }, lowPass: 0.15f);
    }

    AudioClip Make(string name, float dur, Func<float, float, float> sample, float lowPass = 1f)
    {
        int n = Mathf.Max(64, Mathf.RoundToInt(dur * Rate));
        var data = new float[n];
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = Mathf.Clamp(sample(t, dur), -1f, 1f);
            if (lowPass < 1f) { prev = Mathf.Lerp(prev, v, lowPass); v = prev; }
            data[i] = v;
        }
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static void Play(string key, float vol = 1f, float pitchJitter = 0.06f)
    {
        if (I == null || !I.ready || Muted || I.oneShot == null) return;
        if (!I.clips.TryGetValue(key, out var c) || c == null) return;
        I.oneShot.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
        I.oneShot.PlayOneShot(c, vol);
    }

    public static void PlayStep() => Play("step", 0.35f, 0.12f);
    public static void PlayBat() => Play("bat", 0.65f, 0.08f);
    public static void PlayHit() => Play("hit", 0.7f, 0.1f);
    public static void PlayShot(int w) => Play(w == 2 ? "shotgun" : (w == 3 ? "rifle" : "pistol"), 0.7f, 0.05f);
    public static void PlayBoom() => Play("boom", 0.9f, 0.04f);
    public static void PlayBite() => Play("bite", 0.75f, 0.08f);
    public static void PlayPickup() => Play("pickup", 0.55f, 0.03f);
    public static void PlayClick() => Play("click", 0.5f, 0.04f);
    public static void PlayUnlock() => Play("unlock", 0.65f, 0.02f);
    public static void PlayError() => Play("error", 0.55f, 0.02f);
    public static void PlayLever() => Play("lever", 0.65f, 0.06f);
    public static void PlayDoor() => Play("door", 0.55f, 0.06f);

    public static void SetAlarm(bool on)
    {
        if (I == null) return;
        I.alarmActive = on;
        if (on) I.alarmNext = Time.unscaledTime + 0.8f;
    }

    public static void ToggleMute()
    {
        Muted = !Muted;
        if (I != null && I.ambSrc != null) I.ambSrc.mute = Muted;
    }
}
