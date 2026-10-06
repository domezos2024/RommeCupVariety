using System.Collections.Generic;
using UnityEngine;

namespace RommeCup.Core
{
    public static class Sfx
    {
        const string M = "Sfx";
        const int SR = 44100;
        const float Tau = Mathf.PI * 2;
        static readonly AudioSource[] voices = new AudioSource[12];
        static AudioSource room;
        static int next;
        static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        static readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();
        static bool on = true;
        static System.Random rnd = new System.Random(7);

        public static bool On
        {
            get => on;
            set { on = value; PlayerPrefs.SetInt("sound", value ? 1 : 0); if (room) room.mute = !value; Log.Var(M, "on", value); }
        }

        class Bq
        {
            float b0, b1, b2, a1, a2, z1, z2;
            static Bq Make(float f, float q, int type)
            {
                float w = Tau * Mathf.Clamp(f, 20, SR * .45f) / SR, cs = Mathf.Cos(w), al = Mathf.Sin(w) / (2 * q), a0 = 1 + al;
                var b = new Bq();
                if (type == 0) { b.b0 = (1 - cs) / 2; b.b1 = 1 - cs; b.b2 = (1 - cs) / 2; }
                else if (type == 1) { b.b0 = (1 + cs) / 2; b.b1 = -(1 + cs); b.b2 = (1 + cs) / 2; }
                else { b.b0 = al; b.b1 = 0; b.b2 = -al; }
                b.b0 /= a0; b.b1 /= a0; b.b2 /= a0; b.a1 = -2 * cs / a0; b.a2 = (1 - al) / a0;
                return b;
            }
            public static Bq LP(float f, float q = .707f) => Make(f, q, 0);
            public static Bq HP(float f, float q = .707f) => Make(f, q, 1);
            public static Bq BP(float f, float q) => Make(f, q, 2);
            public float Tick(float x) { float y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return y; }
        }

        static float R => (float)rnd.NextDouble();
        static float N => R * 2 - 1;
        static float[] Buf(float sec) => new float[(int)(sec * SR)];
        static int S(float sec) => (int)(sec * SR);

        static void Mode(float[] d, int at, float f, float decay, float amp, float phase = 0)
        {
            if (at < 0) return;
            int n = Mathf.Min(d.Length - at, (int)(decay * 9 * SR));
            float w = Tau * f / SR, k = Mathf.Exp(-1f / (decay * SR)), e = amp;
            for (int i = 0; i < n; i++) { d[at + i] += Mathf.Sin(w * i + phase) * e * Mathf.Min(1, i / 12f); e *= k; }
        }

        static void Burst(float[] d, int at, float decay, float amp, Bq f, float attack = .0003f)
        {
            if (at < 0) return;
            int n = Mathf.Min(d.Length - at, (int)(decay * 9 * SR)), na = Mathf.Max(1, S(attack));
            float k = Mathf.Exp(-1f / (decay * SR)), e = amp;
            for (int i = 0; i < n; i++) { float env = i < na ? i / (float)na : 1; d[at + i] += f.Tick(N) * e * env; if (i >= na) e *= k; }
        }

        static void Clack(float[] d, int at, float amp, bool wood, float pitch = 1)
        {
            float f0 = 2150 * pitch * (.88f + .24f * R);
            float[] ratio = { 1f, 1.59f, 2.41f, 3.27f, 4.52f }, dec = { .019f, .013f, .009f, .006f, .004f }, am = { 1f, .72f, .5f, .34f, .22f };
            for (int i = 0; i < ratio.Length; i++) Mode(d, at, f0 * ratio[i] * (1 + .04f * N), dec[i] * (.8f + .4f * R), amp * am[i] * (.7f + .6f * R) * .35f, R * Tau);
            Burst(d, at, .0012f, amp * .9f, Bq.HP(2500));
            if (!wood) return;
            Mode(d, at, 135 + 50 * R, .026f, amp * .55f);
            Mode(d, at, 300 + 80 * R, .016f, amp * .28f);
            Burst(d, at, .004f, amp * .35f, Bq.LP(900));
        }

        static void CardSnap(float[] d, int at, float amp)
        {
            Burst(d, at, .0045f, amp * .8f, Bq.HP(1700));
            Burst(d, at, .011f, amp * .55f, Bq.LP(520));
            Mode(d, at, 170 + 40 * R, .014f, amp * .25f);
        }

        static void Clap(float[] d, int at, float amp, Bq f)
        {
            Burst(d, at, .006f + .004f * R, amp, f, .0004f);
        }

        static void Reverb(ref float[] d, float wet, float tail = .35f)
        {
            var o = new float[d.Length + S(tail)];
            System.Array.Copy(d, o, d.Length);
            int[] cd = { S(.0297f), S(.0371f), S(.0411f), S(.0437f) };
            float[] fb = { .62f, .58f, .55f, .52f };
            var acc = new float[o.Length];
            for (int c = 0; c < cd.Length; c++)
            {
                var buf = new float[cd[c]]; int p = 0; float lp = 0;
                for (int i = 0; i < o.Length; i++)
                {
                    float y = buf[p]; lp = lp + (y - lp) * .45f;
                    buf[p] = o[i] + lp * fb[c]; p = (p + 1) % buf.Length;
                    acc[i] += y * .25f;
                }
            }
            foreach (var ap in new[] { S(.005f), S(.0017f) })
            {
                var buf = new float[ap]; int p = 0;
                for (int i = 0; i < acc.Length; i++) { float bo = buf[p], x = acc[i]; float y = -x + bo; buf[p] = x + bo * .5f; p = (p + 1) % ap; acc[i] = y; }
            }
            for (int i = 0; i < o.Length; i++) o[i] += acc[i] * wet;
            d = o;
        }

        static AudioClip Clip(string name, float[] d, float peak)
        {
            float m = 1e-6f; foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
            float g = peak / m;
            int fade = Mathf.Min(d.Length, SR / 100);
            for (int i = 0; i < d.Length; i++) d[i] *= g;
            for (int i = 0; i < fade; i++) d[d.Length - 1 - i] *= i / (float)fade;
            var c = AudioClip.Create(name, d.Length, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        static void Add(string name, params AudioClip[] c) => clips[name] = c;

        public static void Init(GameObject host)
        {
            for (int i = 0; i < voices.Length; i++) { voices[i] = host.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
            room = host.AddComponent<AudioSource>();
            room.loop = true; room.volume = .07f;
            on = PlayerPrefs.GetInt("sound", 1) == 1;
            var t0 = Time.realtimeSinceStartup;
            var tiles = new AudioClip[5];
            for (int v = 0; v < tiles.Length; v++) { var d = Buf(.16f); Clack(d, S(.002f), 1, true); Reverb(ref d, .1f, .2f); tiles[v] = Clip("tile" + v, d, .85f); }
            Add("tile", tiles);
            var picks = new AudioClip[3];
            for (int v = 0; v < picks.Length; v++) { var d = Buf(.08f); Clack(d, S(.001f), .5f, false, 1.35f); picks[v] = Clip("tpick" + v, d, .45f); }
            Add("tpick", picks);
            { var d = Buf(.5f); var f = Bq.BP(1400, .6f); for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] += f.Tick(N) * .12f * Mathf.Sin(Mathf.Clamp01(t / .45f) * Mathf.PI); } for (int k = 0; k < 5; k++) Clack(d, S(.04f + k * .085f + .02f * R), .35f + .25f * R, false, .9f); Reverb(ref d, .1f, .15f); Add("rack", Clip("rack", d, .5f)); }
            {
                var d = Buf(2.2f); var f = Bq.BP(900, .5f);
                for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; d[i] += f.Tick(N) * .1f * Mathf.Clamp01(t / .15f) * Mathf.Clamp01((2.1f - t) / .5f) * (.7f + .3f * Mathf.Sin(t * 7)); }
                for (float t = .02f; t < 2f;) { Clack(d, S(t), .12f + .4f * R * Mathf.Clamp01((2.05f - t) / .6f), R < .6f, .8f + .5f * R); t += -Mathf.Log(1 - R * .999f) / 55f; }
                Reverb(ref d, .14f); Add("tshuffle", Clip("tshuffle", d, .6f));
            }
            var cards = new AudioClip[3];
            for (int v = 0; v < cards.Length; v++) { var d = Buf(.1f); CardSnap(d, S(.002f), 1); Reverb(ref d, .08f, .15f); cards[v] = Clip("card" + v, d, .55f); }
            Add("card", cards);
            {
                var d = Buf(.24f); var f = Bq.BP(2600, .7f);
                for (int i = 0; i < d.Length; i++) { float t = i / (float)SR; float env = t < .035f ? t / .035f : Mathf.Exp(-(t - .035f) / .05f); d[i] += f.Tick(N) * env * .35f; }
                Reverb(ref d, .08f, .15f); Add("cslide", Clip("cslide", d, .4f));
            }
            {
                var d = Buf(1.7f);
                for (float t = .03f; t < 1.05f;) { Burst(d, S(t), .0022f, .25f + .2f * R, Bq.HP(2200)); t += 1f / Mathf.Lerp(32, 75, t); }
                var f = Bq.BP(3200, .8f);
                for (int i = S(1.08f); i < S(1.42f); i++) { float t = i / (float)SR - 1.08f; d[i] += f.Tick(N) * .35f * Mathf.Abs(Mathf.Sin(t * Tau * 45)) * Mathf.Exp(-t * 4); }
                CardSnap(d, S(1.48f), .8f);
                Reverb(ref d, .1f); Add("riffle", Clip("riffle", d, .55f));
            }
            { var d = Buf(.03f); Mode(d, 1, 1900, .0022f, .6f); Burst(d, 1, .0006f, .4f, Bq.HP(3000)); Add("click", Clip("click", d, .22f)); }
            { var d = Buf(.32f); for (int k = 0; k < 2; k++) { int a = S(.005f + k * .13f); float g = k == 0 ? 1 : .7f; Mode(d, a, 112, .045f, g); Mode(d, a, 236, .028f, g * .35f); Burst(d, a, .006f, g * .25f, Bq.LP(700)); } Reverb(ref d, .1f, .15f); Add("error", Clip("error", d, .5f)); }
            { var d = Buf(.42f); for (int k = 0; k < 2; k++) { int a = S(.005f + k * .165f); float g = k == 0 ? 1 : .85f; Mode(d, a, 186, .045f, g); Mode(d, a, 468, .024f, g * .5f); Mode(d, a, 905, .011f, g * .3f); Burst(d, a, .0015f, g * .5f, Bq.BP(2400, 1)); } Reverb(ref d, .14f, .2f); Add("turn", Clip("turn", d, .6f)); }
            Add("applause", Applause(4.6f, 14));
            Add("applause_small", Applause(2.6f, 6));
            Add("room", Room());
            room.clip = clips["room"][0]; room.mute = !on; room.Play();
            Log.I(M, "clips=" + clips.Count + " ms=" + (int)((Time.realtimeSinceStartup - t0) * 1000));
        }

        static AudioClip Applause(float dur, int people)
        {
            var d = Buf(dur);
            for (int p = 0; p < people; p++)
            {
                float rate = 3.6f + 1.8f * R, start = .05f + .45f * R, end = dur - .4f - 1.1f * R, amp = .5f + .5f * R;
                var f = Bq.BP(850 + 1600 * R, 1.4f + 1.6f * R);
                for (float t = start; t < end; t += 1f / rate * (.88f + .24f * R))
                {
                    float fade = Mathf.Clamp01((end - t) / .9f) * Mathf.Clamp01((t - start) / .25f + .4f);
                    Clap(d, S(t), amp * fade, f);
                }
            }
            Reverb(ref d, .3f, .6f);
            return Clip("applause" + people, d, .7f);
        }

        static AudioClip Room()
        {
            const float len = 12, xf = 1.2f;
            int n = S(len), x = S(xf);
            var d = new float[n + x];
            float b = 0; var lp = Bq.LP(260); var hiss = Bq.BP(3000, .5f);
            for (int i = 0; i < d.Length; i++) { b = b * .995f + N * .05f; d[i] = lp.Tick(b) * 1.2f + hiss.Tick(N) * .012f; }
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = d[i];
            for (int i = 0; i < x; i++) { float a = i / (float)x; o[i] = d[i] * a + d[n + i] * (1 - a); }
            float m = 1e-6f; foreach (var v in o) m = Mathf.Max(m, Mathf.Abs(v));
            for (int i = 0; i < n; i++) o[i] *= .6f / m;
            var c = AudioClip.Create("room", n, 1, SR, false);
            c.SetData(o, 0);
            return c;
        }

        public static void Play(string name, float vol = 1f, float minGap = .03f)
        {
            if (!on || !clips.TryGetValue(name, out var c)) return;
            float now = Time.unscaledTime;
            if (lastPlay.TryGetValue(name, out var t) && now - t < minGap) return;
            lastPlay[name] = now;
            var v = voices[next]; next = (next + 1) % voices.Length;
            v.clip = c[rnd.Next(c.Length)]; v.volume = Mathf.Clamp01(vol); v.pitch = .95f + .1f * R;
            v.Play();
        }
    }
}
