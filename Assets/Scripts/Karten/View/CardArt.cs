using System;
using System.Collections.Generic;
using RommeCup.Core;
using UnityEngine;

namespace RommeCup.Karten
{
    public static class CardArt
    {
        public const int W = 224, H = 314, BackRed = 53, BackBlue = 54;
        public static readonly Color Red = new Color(.78f, .06f, .1f), Black = new Color(.07f, .07f, .08f);
        static readonly Color Paper = new Color(.985f, .978f, .962f), Paper2 = new Color(.955f, .945f, .925f);
        static readonly Color CRed = new Color(.8f, .12f, .12f), CBlue = new Color(.16f, .26f, .6f), CYel = new Color(.96f, .78f, .24f), Skin = new Color(.99f, .86f, .72f);
        static readonly string[] Rk = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "B", "D", "K" };
        static Texture2D paperN;

        static readonly float[][] Pips =
        {
            new[] { .5f, .5f },
            new[] { .5f, 0f, .5f, 1f },
            new[] { .5f, 0f, .5f, .5f, .5f, 1f },
            new[] { 0f, 0f, 1f, 0f, 0f, 1f, 1f, 1f },
            new[] { 0f, 0f, 1f, 0f, .5f, .5f, 0f, 1f, 1f, 1f },
            new[] { 0f, 0f, 1f, 0f, 0f, .5f, 1f, .5f, 0f, 1f, 1f, 1f },
            new[] { 0f, 0f, 1f, 0f, 0f, .5f, 1f, .5f, 0f, 1f, 1f, 1f, .5f, .75f },
            new[] { 0f, 0f, 1f, 0f, 0f, .5f, 1f, .5f, 0f, 1f, 1f, 1f, .5f, .75f, .5f, .25f },
            new[] { 0f, 0f, 1f, 0f, 0f, .333f, 1f, .333f, 0f, .667f, 1f, .667f, 0f, 1f, 1f, 1f, .5f, .5f },
            new[] { 0f, 0f, 1f, 0f, 0f, .333f, 1f, .333f, 0f, .667f, 1f, .667f, 0f, 1f, 1f, 1f, .5f, .833f, .5f, .167f },
        };

        public static Color InkOf(int suit) => suit < 2 ? Red : Black;

        static float SuitSdf(int suit, float x, float y, float cx, float cy, float s, bool flip)
        {
            if (flip) { x = 2 * cx - x; y = 2 * cy - y; }
            switch (suit)
            {
                case 0: return Sdf.Heart(x, y, cx, cy - s * .05f, s * 1.55f);
                case 1: return Sdf.Poly(x, y, new[] { new Vector2(cx, cy + s * 1.18f), new Vector2(cx + s * .82f, cy), new Vector2(cx, cy - s * 1.18f), new Vector2(cx - s * .82f, cy) });
                case 2:
                    return Mathf.Min(Sdf.Heart(x, 2 * (cy + s * .2f) - y, cx, cy + s * .2f, s * 1.5f),
                        Sdf.Poly(x, y, new[] { new Vector2(cx - s * .46f, cy - s * 1.1f), new Vector2(cx + s * .46f, cy - s * 1.1f), new Vector2(cx, cy - s * .15f) }));
                default:
                    return Mathf.Min(Mathf.Min(Sdf.Circle(x, y, cx, cy + s * .48f, s * .44f), Sdf.Circle(x, y, cx - s * .5f, cy - s * .16f, s * .44f)),
                        Mathf.Min(Mathf.Min(Sdf.Circle(x, y, cx + s * .5f, cy - s * .16f, s * .44f), Sdf.Circle(x, y, cx, cy + s * .1f, s * .22f)), Sdf.Poly(x, y, new[] { new Vector2(cx - s * .42f, cy - s * 1.1f), new Vector2(cx + s * .42f, cy - s * 1.1f), new Vector2(cx, cy - s * .1f) })));
            }
        }

        static void Sym(Pix p, int suit, float cx, float cy, float s, bool flip, Color c)
        {
            float r = s * 1.45f;
            p.Draw((x, y) => SuitSdf(suit, x, y, cx, cy, s, flip), c, cx - r, cy - r, cx + r, cy + r, 1.1f);
        }

        static void Rank(Pix p, string s, float cx, float top, float h, bool flip, Color c, float sx = 1, float weight = .085f)
        {
            float w = Glyphs.Width(s) * h * sx, ox = cx - w / 2, oy = top - h, th = h * weight;
            float x0 = ox - 10, x1 = ox + w + 10, y0 = oy - 10, y1 = oy + h + 10;
            if (flip) p.Draw((x, y) => Glyphs.Dist(s, ox + (W - x - ox) / sx, H - y, ox, oy, h) - th, c, W - x1, H - y1, W - x0, H - y0, 1.1f);
            else p.Draw((x, y) => Glyphs.Dist(s, ox + (x - ox) / sx, y, ox, oy, h) - th, c, x0, y0, x1, y1, 1.1f);
        }

        static void Index(Pix p, int suit, string s, Color ink)
        {
            bool two = s.Length > 1;
            float h = two ? 48 : 52, sx = two ? .66f : 1, cx = 27, top = H - 12;
            foreach (var f in new[] { false, true })
            {
                Rank(p, s, cx, top, h, f, ink, sx, .12f);
                float sy = top - h - 22;
                Sym(p, suit, f ? W - cx : cx, f ? H - sy : sy, 15, f, ink);
            }
        }

        static Pix Blank(int seed)
        {
            var p = new Pix(W, H, Paper);
            p.Fill((x, y) => Color.Lerp(Paper2, Paper, .7f + .3f * Mathf.PerlinNoise(x * .03f + seed, y * .03f)));
            p.Noise(.008f, seed);
            return p;
        }

        public static Texture2D Face(int f)
        {
            var p = Blank(f);
            if (f == 52) { Joker(p); return p.Tex(); }
            int suit = f / 13, rank = f % 13;
            var ink = InkOf(suit);
            Index(p, suit, Rk[rank], ink);
            if (rank == 0)
            {
                float s = suit == 2 ? 50 : 38;
                Sym(p, suit, W / 2f, H / 2f, s, false, ink);
                if (suit == 2) p.Draw((x, y) => Mathf.Abs(Sdf.Ellipse(x, y, W / 2f, H / 2f - 6, 76, 92)) - 1.4f, ink * .9f, 20, 40, W - 20, H - 40);
            }
            else if (rank >= 10) Court(p, suit, rank, ink);
            else
            {
                var pp = Pips[rank];
                for (int i = 0; i < pp.Length; i += 2)
                {
                    float px = W / 2f + (pp[i] - .5f) * 70, py = 64 + pp[i + 1] * 186;
                    Sym(p, suit, px, py, 17, pp[i + 1] < .49f, ink);
                }
            }
            return p.Tex();
        }

        static void Mirror(Pix p, Func<float, float, float> sdf, Func<float, float, Color> col)
        {
            float mid = H / 2f;
            p.Draw((x, y) => Mathf.Max(sdf(x, y), mid - y), col, 30, mid - 2, W - 30, H - 30);
            p.Draw((x, y) => Mathf.Max(sdf(W - x, H - y), y - mid), (x, y) => col(W - x, H - y), 30, 30, W - 30, mid + 2);
        }

        static void Mirror(Pix p, Func<float, float, float> sdf, Color c) => Mirror(p, sdf, (x, y) => c);

        static Func<float, float, float> Outline(Func<float, float, float> f, float w) => (x, y) => Mathf.Abs(f(x, y)) - w;

        static void Court(Pix p, int suit, int rank, Color ink)
        {
            float fx0 = 50, fx1 = W - 50, fy0 = 40, fy1 = H - 40, cx = W / 2f, mid = H / 2f;
            p.Draw((x, y) => Mathf.Abs(Sdf.Box(x, y, cx, mid, (fx1 - fx0) / 2, (fy1 - fy0) / 2, 3)) - 1.1f, ink);
            Color a = suit % 2 == 0 ? CRed : CBlue, b = suit % 2 == 0 ? CBlue : CRed, line = Black;
            Func<float, float, float> robe = (x, y) => Sdf.Poly(x, y, new[] { new Vector2(cx - 58, mid), new Vector2(cx + 58, mid), new Vector2(cx + 40, mid + 54), new Vector2(cx - 40, mid + 54) });
            Mirror(p, robe, (x, y) => { float band = Mathf.Repeat((x - cx) * .7f + (y - mid) * .9f, 28); return band < 9 ? a : band < 18 ? CYel : b; });
            Mirror(p, Outline(robe, 1.2f), line);
            Func<float, float, float> collar = (x, y) => Sdf.Ellipse(x, y, cx, mid + 56, 34, 9);
            Mirror(p, collar, rank == 10 ? CYel : Color.white);
            Mirror(p, Outline(collar, 1f), line);
            Func<float, float, float> head = (x, y) => Sdf.Circle(x, y, cx, mid + 75, 19);
            if (rank == 12) Mirror(p, (x, y) => Sdf.Ellipse(x, y, cx, mid + 64, 15, 14), new Color(.55f, .38f, .2f));
            if (rank == 11) Mirror(p, (x, y) => Mathf.Min(Sdf.Ellipse(x, y, cx - 17, mid + 70, 8, 20), Sdf.Ellipse(x, y, cx + 17, mid + 70, 8, 20)), CYel);
            Mirror(p, head, Skin);
            Mirror(p, Outline(head, 1f), line);
            Mirror(p, (x, y) => Mathf.Min(Sdf.Circle(x, y, cx - 7, mid + 77, 1.9f), Sdf.Circle(x, y, cx + 7, mid + 77, 1.9f)), line);
            Mirror(p, (x, y) => Sdf.Seg(x, y, cx - 5, mid + 69, cx + 5, mid + 69) - .9f, CRed);
            if (rank == 12)
            {
                Func<float, float, float> crown = (x, y) => Sdf.Poly(x, y, new[] { new Vector2(cx - 20, mid + 87), new Vector2(cx + 20, mid + 87), new Vector2(cx + 23, mid + 108), new Vector2(cx + 11, mid + 97), new Vector2(cx, mid + 111), new Vector2(cx - 11, mid + 97), new Vector2(cx - 23, mid + 108) });
                Mirror(p, crown, CYel); Mirror(p, Outline(crown, 1f), line);
                Mirror(p, (x, y) => Sdf.Seg(x, y, cx + 46, mid + 7, cx + 34, mid + 104) - 2.2f, new Color(.72f, .72f, .76f));
                Mirror(p, (x, y) => Sdf.Seg(x, y, cx + 30, mid + 26, cx + 50, mid + 30) - 2.4f, CYel);
            }
            else if (rank == 11)
            {
                Func<float, float, float> crown = (x, y) => Sdf.Poly(x, y, new[] { new Vector2(cx - 15, mid + 88), new Vector2(cx + 15, mid + 88), new Vector2(cx + 17, mid + 101), new Vector2(cx + 6, mid + 95), new Vector2(cx, mid + 104), new Vector2(cx - 6, mid + 95), new Vector2(cx - 17, mid + 101) });
                Mirror(p, crown, CYel); Mirror(p, Outline(crown, 1f), line);
                Mirror(p, (x, y) => Sdf.Seg(x, y, cx - 36, mid + 17, cx - 44, mid + 45) - 1.4f, new Color(.2f, .5f, .25f));
                Mirror(p, (x, y) => Sdf.Circle(x, y, cx - 44, mid + 50, 7), CRed);
            }
            else
            {
                Func<float, float, float> cap = (x, y) => Mathf.Max(Sdf.Ellipse(x, y, cx + 2, mid + 89, 26, 11), mid + 86 - y);
                Mirror(p, cap, b); Mirror(p, Outline(cap, 1f), line);
                Mirror(p, (x, y) => Sdf.Seg(x, y, cx + 18, mid + 94, cx + 36, mid + 108) - 2.2f, CYel);
                Mirror(p, (x, y) => Sdf.Seg(x, y, cx - 44, mid + 3, cx - 44, mid + 103) - 1.8f, new Color(.45f, .3f, .15f));
                Mirror(p, (x, y) => Sdf.Poly(x, y, new[] { new Vector2(cx - 52, mid + 97), new Vector2(cx - 36, mid + 97), new Vector2(cx - 44, mid + 111) }), new Color(.72f, .72f, .76f));
            }
            Mirror(p, (x, y) => SuitSdf(suit, x, y, fx0 + 16, fy1 - 18, 9, false), ink);
            p.Draw((x, y) => Sdf.Seg(x, y, fx0, mid, fx1, mid) - .6f, new Color(0, 0, 0, .5f));
        }

        static void Joker(Pix p)
        {
            float cx = W / 2f + 12, cy = H / 2f;
            const string word = "JOKER";
            for (int i = 0; i < word.Length; i++)
            {
                string ch = word[i].ToString(); float h = 26, top = H - 18 - i * 34;
                Rank(p, ch, 24, top, h, false, Red);
                Rank(p, ch, 24, top, h, true, Black);
            }
            Func<float, float, float> face = (x, y) => Sdf.Ellipse(x, y, cx, cy + 6, 30, 36);
            var hatL = new[] { new Vector2(cx - 30, cy + 30), new Vector2(cx, cy + 46), new Vector2(cx - 62, cy + 100) };
            var hatM = new[] { new Vector2(cx - 16, cy + 42), new Vector2(cx + 16, cy + 42), new Vector2(cx + 4, cy + 112) };
            var hatR = new[] { new Vector2(cx + 30, cy + 30), new Vector2(cx, cy + 46), new Vector2(cx + 60, cy + 92) };
            p.Draw((x, y) => Sdf.Poly(x, y, hatL), CRed); p.Draw((x, y) => Sdf.Poly(x, y, hatM), CYel); p.Draw((x, y) => Sdf.Poly(x, y, hatR), CBlue);
            foreach (var v in new[] { hatL, hatM, hatR }) p.Draw((x, y) => Mathf.Abs(Sdf.Poly(x, y, v)) - 1f, Black);
            foreach (var b in new[] { new Vector2(cx - 62, cy + 100), new Vector2(cx + 4, cy + 112), new Vector2(cx + 60, cy + 92) }) { p.Draw((x, y) => Sdf.Circle(x, y, b.x, b.y, 7), CYel); p.Draw((x, y) => Mathf.Abs(Sdf.Circle(x, y, b.x, b.y, 7)) - .9f, Black); }
            Func<float, float, float> ruff = (x, y) => Sdf.Ellipse(x, y, cx, cy - 34, 52, 14);
            p.Draw((x, y) => Sdf.Poly(x, y, new[] { new Vector2(cx - 50, cy - 140), new Vector2(cx + 50, cy - 140), new Vector2(cx + 40, cy - 40), new Vector2(cx - 40, cy - 40) }), (x, y) => (Mathf.FloorToInt((x - cx) / 20 + 10) + Mathf.FloorToInt((y - cy) / 20 + 10)) % 2 == 0 ? CRed : CBlue);
            p.Draw(ruff, Color.white); p.Draw(Outline(ruff, 1f), Black);
            p.Draw(face, Skin); p.Draw(Outline(face, 1.1f), Black);
            p.Draw((x, y) => Mathf.Min(Sdf.Circle(x, y, cx - 11, cy + 14, 2.4f), Sdf.Circle(x, y, cx + 11, cy + 14, 2.4f)), Black);
            p.Draw((x, y) => Sdf.Arc(x, y, cx, cy + 4, 15, Mathf.PI * 1.15f, Mathf.PI * 1.85f) - 1.6f, CRed);
            p.Draw((x, y) => Sdf.Circle(x, y, cx, cy + 4, 4), CRed);
        }

        public static Texture2D CardBack(bool blue)
        {
            var p = Blank(blue ? 77 : 66);
            var c = blue ? new Color(.12f, .24f, .52f) : new Color(.66f, .1f, .12f);
            const float m = 12;
            p.Draw((x, y) => Sdf.Box(x, y, W / 2f, H / 2f, W / 2f - m, H / 2f - m, 6), c);
            var line = new Color(.98f, .97f, .94f, .8f);
            p.Draw((x, y) => Mathf.Max(Mathf.Min(Mathf.Abs(Mathf.Repeat(x + y, 14) - 7), Mathf.Abs(Mathf.Repeat(x - y + 700, 14) - 7)) * .7071f - .55f, Sdf.Box(x, y, W / 2f, H / 2f, W / 2f - m - 8, H / 2f - m - 8, 4)), line, 1);
            p.Draw((x, y) => Mathf.Abs(Sdf.Box(x, y, W / 2f, H / 2f, W / 2f - m - 4, H / 2f - m - 4, 5)) - 1.1f, line);
            p.Draw((x, y) => Sdf.Ellipse(x, y, W / 2f, H / 2f, 40, 52), c);
            p.Draw((x, y) => Mathf.Abs(Sdf.Ellipse(x, y, W / 2f, H / 2f, 40, 52)) - 1.4f, line);
            for (int k = 0; k < 4; k++) Sym(p, k, W / 2f + (k % 2 == 0 ? -14 : 14), H / 2f + (k < 2 ? 15 : -15), 9, false, line);
            return p.Tex();
        }

        public static Texture2D PaperNormal()
        {
            if (paperN) return paperN;
            var h = new float[W * H];
            var r = new System.Random(3);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) h[y * W + x] = Mathf.Sin(x * 1.6f) * Mathf.Sin(y * 1.6f) * .25f + (float)r.NextDouble() * .2f;
            paperN = Pix.Normal(h, W, H, .5f);
            return paperN;
        }

        public static Texture2D EdgeStripes()
        {
            var p = new Pix(16, 128, Color.white);
            p.Fill((x, y) => { float v = Mathf.Repeat(y, 2) < 1 ? .92f : .78f; v += (Noise.Hash((int)y, 5) - .5f) * .08f; return new Color(v, v * .99f, v * .96f); });
            return p.Tex(true, false, TextureWrapMode.Repeat);
        }
    }

    public static class CardMesh
    {
        public static Mesh Build(float w, float d, float h, float r, int seg = 5)
        {
            var outl = new List<Vector3>(); var dirs = new List<Vector3>();
            float hx = w / 2 - r, hz = d / 2 - r;
            var cc = new[] { new Vector2(hx, hz), new Vector2(-hx, hz), new Vector2(-hx, -hz), new Vector2(hx, -hz) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = (c * 90 + 90f * i / seg) * Mathf.Deg2Rad;
                    dirs.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                    outl.Add(new Vector3(cc[c].x, 0, cc[c].y));
                }
            int n = outl.Count;
            var v = new List<Vector3>(); var nm = new List<Vector3>(); var uv = new List<Vector2>();
            var t0 = new List<int>(); var t1 = new List<int>(); var t2 = new List<int>();
            Vector2 UV(Vector3 p, bool mirror) { float u = (p.x + w / 2) / w; return new Vector2(mirror ? 1 - u : u, (p.z + d / 2) / d); }
            int ct = v.Count; v.Add(new Vector3(0, h, 0)); nm.Add(Vector3.up); uv.Add(new Vector2(.5f, .5f));
            int top = v.Count;
            for (int i = 0; i < n; i++) { var p = outl[i] + dirs[i] * r; v.Add(p + Vector3.up * h); nm.Add(Vector3.up); uv.Add(UV(p, false)); }
            for (int i = 0; i < n; i++) { t0.Add(ct); t0.Add(top + (i + 1) % n); t0.Add(top + i); }
            int cb = v.Count; v.Add(Vector3.zero); nm.Add(Vector3.down); uv.Add(new Vector2(.5f, .5f));
            int bot = v.Count;
            for (int i = 0; i < n; i++) { var p = outl[i] + dirs[i] * r; v.Add(p); nm.Add(Vector3.down); uv.Add(UV(p, true)); }
            for (int i = 0; i < n; i++) { t1.Add(cb); t1.Add(bot + i); t1.Add(bot + (i + 1) % n); }
            int sa = v.Count;
            for (int i = 0; i < n; i++) { var p = outl[i] + dirs[i] * r; v.Add(p + Vector3.up * h); nm.Add(dirs[i]); uv.Add(new Vector2(i / (float)n, 1)); }
            int sb = v.Count;
            for (int i = 0; i < n; i++) { var p = outl[i] + dirs[i] * r; v.Add(p); nm.Add(dirs[i]); uv.Add(new Vector2(i / (float)n, 0)); }
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; t2.AddRange(new[] { sa + i, sa + j, sb + j, sa + i, sb + j, sb + i }); }
            var m = new Mesh { name = "Card" };
            m.SetVertices(v); m.SetNormals(nm); m.SetUVs(0, uv);
            m.subMeshCount = 3; m.SetTriangles(t0, 0); m.SetTriangles(t1, 1); m.SetTriangles(t2, 2);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }
    }
}
