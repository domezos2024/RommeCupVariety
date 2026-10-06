using System;
using System.Collections.Generic;
using UnityEngine;

namespace RommeCup.Core
{
    public class Pix
    {
        public readonly int W, H;
        public readonly Color[] C;
        public float[] Hg;

        public Pix(int w, int h, Color bg) { W = w; H = h; C = new Color[w * h]; for (int i = 0; i < C.Length; i++) C[i] = bg; }

        public void Fill(Func<float, float, Color> f)
        {
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) C[y * W + x] = f(x + .5f, y + .5f);
        }

        public void Draw(Func<float, float, float> sdf, Color c, float aa = 1.2f) => Draw(sdf, (x, y) => c, 0, 0, W, H, aa);
        public void Draw(Func<float, float, float> sdf, Func<float, float, Color> col, float aa = 1.2f) => Draw(sdf, col, 0, 0, W, H, aa);
        public void Draw(Func<float, float, float> sdf, Color c, float x0, float y0, float x1, float y1, float aa = 1.2f) => Draw(sdf, (x, y) => c, x0, y0, x1, y1, aa);

        public void Draw(Func<float, float, float> sdf, Func<float, float, Color> col, float x0, float y0, float x1, float y1, float aa = 1.2f)
        {
            int ix0 = Mathf.Max(0, (int)x0), iy0 = Mathf.Max(0, (int)y0), ix1 = Mathf.Min(W, Mathf.CeilToInt(x1)), iy1 = Mathf.Min(H, Mathf.CeilToInt(y1));
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                {
                    float px = x + .5f, py = y + .5f, d = sdf(px, py), a = Mathf.Clamp01(.5f - d / aa);
                    if (a <= 0) continue;
                    var c = col(px, py); a *= c.a;
                    int i = y * W + x; var o = C[i];
                    C[i] = new Color(Mathf.Lerp(o.r, c.r, a), Mathf.Lerp(o.g, c.g, a), Mathf.Lerp(o.b, c.b, a), a + o.a * (1 - a));
                }
        }

        public void Height(Func<float, float, float> sdf, float depth, float soft, float x0 = 0, float y0 = 0, float x1 = -1, float y1 = -1)
        {
            if (Hg == null) Hg = new float[W * H];
            if (x1 < 0) { x1 = W; y1 = H; }
            int ix0 = Mathf.Max(0, (int)x0), iy0 = Mathf.Max(0, (int)y0), ix1 = Mathf.Min(W, Mathf.CeilToInt(x1)), iy1 = Mathf.Min(H, Mathf.CeilToInt(y1));
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                {
                    float d = sdf(x + .5f, y + .5f), a = Mathf.SmoothStep(0, 1, Mathf.Clamp01(.5f - d / soft));
                    if (a <= 0) continue;
                    int i = y * W + x;
                    Hg[i] = Mathf.Lerp(Hg[i], depth, a);
                }
        }

        public void Noise(float amp, int seed)
        {
            var r = new System.Random(seed);
            for (int i = 0; i < C.Length; i++) { float n = ((float)r.NextDouble() - .5f) * amp; var c = C[i]; C[i] = new Color(c.r + n, c.g + n, c.b + n, c.a); }
        }

        public Texture2D Tex(bool mips = true, bool readable = false, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, mips) { wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels(C); t.Apply(mips, !readable);
            return t;
        }

        public Texture2D NormalTex(float strength, TextureWrapMode wrap = TextureWrapMode.Clamp) => Normal(Hg ?? new float[W * H], W, H, strength, wrap);

        public static Texture2D Normal(float[] h, int w, int hh, float strength, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var c = new Color[w * hh];
            bool rep = wrap == TextureWrapMode.Repeat;
            int X(int x) => rep ? (x + w) % w : Mathf.Clamp(x, 0, w - 1);
            int Y(int y) => rep ? (y + hh) % hh : Mathf.Clamp(y, 0, hh - 1);
            for (int y = 0; y < hh; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (h[y * w + X(x + 1)] - h[y * w + X(x - 1)]) * .5f * strength, dy = (h[Y(y + 1) * w + x] - h[Y(y - 1) * w + x]) * .5f * strength;
                    var n = new Vector3(-dx, -dy, 1).normalized;
                    c[y * w + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                }
            var t = new Texture2D(w, hh, TextureFormat.RGBA32, true, true) { wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels(c); t.Apply(true, true);
            return t;
        }
    }

    public static class Sdf
    {
        public static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        public static float Box(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x - cx) - hw + r, qy = Mathf.Abs(y - cy) - hh + r, mx = Mathf.Max(qx, 0), my = Mathf.Max(qy, 0);
            return Mathf.Sqrt(mx * mx + my * my) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        public static float Seg(float x, float y, float ax, float ay, float bx, float by)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay + 1e-6f));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static float Arc(float x, float y, float cx, float cy, float r, float a0, float a1)
        {
            float dx = x - cx, dy = y - cy, a = Mathf.Atan2(dy, dx), tau = Mathf.PI * 2;
            while (a < a0) a += tau;
            while (a >= a0 + tau) a -= tau;
            if (a <= a1) return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r);
            float e0x = cx + r * Mathf.Cos(a0), e0y = cy + r * Mathf.Sin(a0), e1x = cx + r * Mathf.Cos(a1), e1y = cy + r * Mathf.Sin(a1);
            return Mathf.Min(Mathf.Sqrt((x - e0x) * (x - e0x) + (y - e0y) * (y - e0y)), Mathf.Sqrt((x - e1x) * (x - e1x) + (y - e1y) * (y - e1y)));
        }

        public static float Ellipse(float x, float y, float cx, float cy, float a, float b, float rotDeg = 0)
        {
            float r = rotDeg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r), dx = x - cx, dy = y - cy;
            float u = dx * cs + dy * sn, v = -dx * sn + dy * cs;
            return (Mathf.Sqrt(u * u / (a * a) + v * v / (b * b)) - 1) * Mathf.Min(a, b);
        }

        public static float Heart(float x, float y, float cx, float cy, float s)
        {
            float px = Mathf.Abs((x - cx) / s), py = (y - cy) / s + .55f, d;
            if (py + px > 1f) { float dx = px - .25f, dy = py - .75f; d = Mathf.Sqrt(dx * dx + dy * dy) - .35355339f; }
            else
            {
                float d1 = px * px + (py - 1) * (py - 1), m = .5f * Mathf.Max(px + py, 0), d2 = (px - m) * (px - m) + (py - m) * (py - m);
                d = Mathf.Sqrt(Mathf.Min(d1, d2)) * Mathf.Sign(px - py);
            }
            return d * s;
        }

        public static float Star(float x, float y, float cx, float cy, float r, float rf)
        {
            const float k1x = 0.809016994375f, k1y = -0.587785252292f, k2x = -k1x, k2y = k1y;
            float px = Mathf.Abs(x - cx), py = y - cy;
            float d1 = Mathf.Max(k1x * px + k1y * py, 0); px -= 2 * d1 * k1x; py -= 2 * d1 * k1y;
            float d2 = Mathf.Max(k2x * px + k2y * py, 0); px -= 2 * d2 * k2x; py -= 2 * d2 * k2y;
            px = Mathf.Abs(px); py -= r;
            float bax = rf * -k1y, bay = rf * k1x - 1;
            float h = Mathf.Clamp((px * bax + py * bay) / (bax * bax + bay * bay), 0, r);
            float dx = px - bax * h, dy = py - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) * Mathf.Sign(py * bax - px * bay);
        }

        public static float Poly(float x, float y, Vector2[] v)
        {
            float d = (x - v[0].x) * (x - v[0].x) + (y - v[0].y) * (y - v[0].y), s = 1;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y, wx = x - v[i].x, wy = y - v[i].y;
                float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey)), bx = wx - ex * t, by = wy - ey * t;
                d = Mathf.Min(d, bx * bx + by * by);
                bool c1 = y >= v[i].y, c2 = y < v[j].y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
    }

    public static class Noise
    {
        public static float Fbm(float x, float y, int oct = 4)
        {
            float s = 0, a = .5f, f = 1;
            for (int i = 0; i < oct; i++) { s += Mathf.PerlinNoise(x * f + i * 17.3f, y * f + i * 9.1f) * a; a *= .5f; f *= 2.03f; }
            return s;
        }

        public static float Hash(int a, int b = 0) { unchecked { uint h = (uint)(a * 374761393 + b * 668265263); h = (h ^ (h >> 13)) * 1274126177; return (h ^ (h >> 16)) / 4294967295f; } }
    }

    public static class Glyphs
    {
        struct P { public bool Arc; public float a, b, c, d, e; }
        static P S(float ax, float ay, float bx, float by) => new P { a = ax, b = ay, c = bx, d = by };
        static P A(float cx, float cy, float r, float a0, float a1) => new P { Arc = true, a = cx, b = cy, c = r, d = a0 * Mathf.Deg2Rad, e = a1 * Mathf.Deg2Rad };
        public const float W = .6f, Gap = .14f;

        static readonly Dictionary<char, P[]> G = new Dictionary<char, P[]>
        {
            ['0'] = new[] { A(.3f, .7f, .3f, 0, 180), A(.3f, .3f, .3f, 180, 360), S(0, .3f, 0, .7f), S(.6f, .3f, .6f, .7f) },
            ['1'] = new[] { S(.38f, 0, .38f, 1), S(.38f, 1, .1f, .76f) },
            ['2'] = new[] { A(.3f, .7f, .28f, -30, 165), S(.542f, .56f, .02f, 0), S(.02f, 0, .6f, 0) },
            ['3'] = new[] { A(.3f, .75f, .25f, -90, 165), A(.3f, .27f, .27f, -165, 90) },
            ['4'] = new[] { S(.45f, 1, 0, .32f), S(0, .32f, .6f, .32f), S(.45f, 1, .45f, 0) },
            ['5'] = new[] { S(.56f, 1, .1f, 1), S(.1f, 1, .05f, .58f), A(.3f, .31f, .3f, -150, 128) },
            ['6'] = new[] { A(.3f, .3f, .3f, 0, 360), S(.02f, .34f, .44f, 1) },
            ['7'] = new[] { S(0, 1, .6f, 1), S(.6f, 1, .18f, 0) },
            ['8'] = new[] { A(.3f, .77f, .23f, 0, 360), A(.3f, .28f, .28f, 0, 360) },
            ['9'] = new[] { A(.3f, .7f, .3f, 0, 360), S(.58f, .66f, .16f, 0) },
            ['A'] = new[] { S(.02f, 0, .3f, 1), S(.3f, 1, .58f, 0), S(.13f, .35f, .47f, .35f) },
            ['K'] = new[] { S(.06f, 0, .06f, 1), S(.56f, 1, .06f, .42f), S(.24f, .6f, .58f, 0) },
            ['D'] = new[] { S(.06f, 0, .06f, 1), S(.06f, 1, .22f, 1), A(.22f, .62f, .38f, 0, 90), S(.6f, .62f, .6f, .38f), A(.22f, .38f, .38f, -90, 0), S(.06f, 0, .22f, 0) },
            ['B'] = new[] { S(.06f, 0, .06f, 1), S(.06f, 1, .3f, 1), A(.3f, .76f, .24f, -90, 90), S(.3f, .52f, .06f, .52f), S(.06f, .52f, .32f, .52f), A(.32f, .26f, .26f, -90, 90), S(.32f, 0, .06f, 0) },
            ['J'] = new[] { S(.2f, 1, .52f, 1), S(.52f, 1, .52f, .26f), A(.28f, .26f, .24f, 180, 360) },
            ['O'] = new[] { A(.3f, .7f, .3f, 0, 180), A(.3f, .3f, .3f, 180, 360), S(0, .3f, 0, .7f), S(.6f, .3f, .6f, .7f) },
            ['E'] = new[] { S(.54f, 1, .06f, 1), S(.06f, 1, .06f, 0), S(.06f, 0, .54f, 0), S(.06f, .52f, .44f, .52f) },
            ['R'] = new[] { S(.06f, 0, .06f, 1), S(.06f, 1, .3f, 1), A(.3f, .75f, .25f, -90, 90), S(.3f, .5f, .06f, .5f), S(.26f, .5f, .58f, 0) },
        };

        public static float Width(string s) => s.Length * W + (s.Length - 1) * Gap;

        public static float Dist(string s, float x, float y, float ox, float oy, float h)
        {
            float gx = (x - ox) / h, gy = (y - oy) / h, best = 1e9f;
            for (int k = 0; k < s.Length; k++)
            {
                if (!G.TryGetValue(s[k], out var ps)) continue;
                float lx = gx - k * (W + Gap);
                if (lx < -.5f || lx > W + .5f) continue;
                foreach (var p in ps)
                {
                    float d = p.Arc ? Sdf.Arc(lx, gy, p.a, p.b, p.c, p.d, p.e) : Sdf.Seg(lx, gy, p.a, p.b, p.c, p.d);
                    if (d < best) best = d;
                }
            }
            return best * h;
        }
    }
}
