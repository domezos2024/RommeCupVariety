using System.Collections.Generic;
using RommeCup.Core;
using UnityEngine;

namespace RommeCup.Rummikub
{
    public static class TileArt
    {
        public const int W = 160, H = 224;
        public static readonly Color[] Ink = { new Color(.09f, .09f, .1f), new Color(.76f, .08f, .09f), new Color(.06f, .24f, .6f), new Color(.92f, .48f, .04f) };
        public static readonly Color Ivory = new Color(.95f, .925f, .86f), IvoryDark = new Color(.9f, .87f, .79f);
        static readonly Dictionary<int, Texture2D> normals = new Dictionary<int, Texture2D>();
        const float NumBase = 36;

        static void Base(Pix p, int seed)
        {
            p.Fill((x, y) =>
            {
                float n = Mathf.PerlinNoise(x * .045f + seed, y * .045f) * .5f + Mathf.PerlinNoise(x * .21f, y * .21f + seed) * .25f;
                return Color.Lerp(IvoryDark, Ivory, .55f + n * .45f - Mathf.Abs(y / H - .55f) * .12f);
            });
            p.Noise(.012f, seed);
        }

        static float Sx(string s) => s.Length > 1 ? .72f : 1f;

        static void NumberShape(string s, out float ox, out float h, out float th)
        {
            h = s.Length > 1 ? 152 : 156; float w = Glyphs.Width(s) * h * Sx(s);
            if (w > W - 28) { h *= (W - 28) / w; w = W - 28; }
            ox = W / 2f - w / 2; th = h * .12f;
        }

        static float NumSdf(string s, float x, float y, float ox, float h, float th) => Glyphs.Dist(s, ox + (x - ox) / Sx(s), y, ox, NumBase, h) - th;

        public static Texture2D Face(int kind)
        {
            var p = new Pix(W, H, Ivory);
            Base(p, kind);
            if (kind == 52) { Joker(p, false); return p.Tex(); }
            var ink = Ink[kind / 13]; string s = (kind % 13 + 1).ToString();
            NumberShape(s, out var ox, out var h, out var th);
            float x0 = 0, y0 = NumBase - 22, y1 = NumBase + h + 22;
            p.Draw((x, y) => NumSdf(s, x, y, ox, h, th) + 1.2f, new Color(0, 0, 0, .28f), x0, y0, W, y1, 2.5f);
            p.Draw((x, y) => NumSdf(s, x, y, ox, h, th), (x, y) => { float k = Mathf.Clamp01((y - NumBase) / h); return Color.Lerp(ink * .9f, ink, k * .5f + .3f); }, x0, y0, W, y1);
            Edge(p);
            return p.Tex();
        }

        static void Edge(Pix p) => p.Draw((x, y) => Mathf.Abs(Sdf.Box(x, y, W / 2f, H / 2f, W / 2f - 2, H / 2f - 2, 14)) - 2, new Color(.6f, .56f, .48f, .35f), 4);

        public static Texture2D Normal(int kind)
        {
            int key = kind == 52 ? 0 : kind % 13 + 1;
            if (normals.TryGetValue(key, out var t)) return t;
            var p = new Pix(W, H, Color.white);
            p.Hg = new float[W * H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) p.Hg[y * W + x] = (Mathf.PerlinNoise(x * .3f, y * .3f) - .5f) * .04f;
            if (key == 0) Joker(p, true);
            else
            {
                string s = key.ToString();
                NumberShape(s, out var ox, out var h, out var th);
                p.Height((x, y) => NumSdf(s, x, y, ox, h, th), -1.1f, 2.4f, 0, NumBase - 22, W, NumBase + h + 22);
            }
            t = p.NormalTex(1.8f);
            normals[key] = t;
            return t;
        }

        static void Joker(Pix p, bool height)
        {
            float cx = W / 2f, cy = 104, r = 36;
            var red = Ink[1]; var blk = Ink[0];
            System.Func<float, float, float> ring = (x, y) => Mathf.Abs(Sdf.Circle(x, y, cx, cy, r)) - 4.2f;
            System.Func<float, float, float> eyes = (x, y) => Mathf.Min(Sdf.Ellipse(x, y, cx - 13, cy + 9, 4.5f, 7), Sdf.Ellipse(x, y, cx + 13, cy + 9, 4.5f, 7));
            System.Func<float, float, float> smile = (x, y) => Sdf.Arc(x, y, cx, cy + 4, 21, Mathf.PI * 1.17f, Mathf.PI * 1.83f) - 3.4f;
            System.Func<float, float, float> nose = (x, y) => Sdf.Circle(x, y, cx, cy - 2, 4.5f);
            var hatL = new[] { new Vector2(cx - 34, cy + 22), new Vector2(cx - 4, cy + 36), new Vector2(cx - 50, cy + 76) };
            var hatM = new[] { new Vector2(cx - 18, cy + 34), new Vector2(cx + 18, cy + 34), new Vector2(cx, cy + 88) };
            var hatR = new[] { new Vector2(cx + 34, cy + 22), new Vector2(cx + 4, cy + 36), new Vector2(cx + 50, cy + 76) };
            System.Func<float, float, float> hatSide = (x, y) => Mathf.Min(Sdf.Poly(x, y, hatL), Sdf.Poly(x, y, hatR));
            System.Func<float, float, float> hatMid = (x, y) => Sdf.Poly(x, y, hatM);
            System.Func<float, float, float> bells = (x, y) => Mathf.Min(Mathf.Min(Sdf.Circle(x, y, cx - 50, cy + 78, 6), Sdf.Circle(x, y, cx + 50, cy + 78, 6)), Sdf.Circle(x, y, cx, cy + 92, 6));
            System.Func<float, float, float> collar = (x, y) => Mathf.Max(Sdf.Circle(x, y, cx, cy - 20, 44), -Sdf.Circle(x, y, cx, cy - 6, 40));
            if (height)
            {
                foreach (var f in new[] { ring, eyes, smile, nose, hatSide, hatMid, bells }) p.Height(f, -1f, 2.2f);
                p.Height((x, y) => Mathf.Max(collar(x, y), y - (cy - 30)), -1f, 2.2f);
                return;
            }
            p.Draw((x, y) => Mathf.Max(collar(x, y), y - (cy - 30)), red);
            p.Draw(hatSide, red); p.Draw(hatMid, blk); p.Draw(bells, (x, y) => y > cy + 88 ? red : blk);
            p.Draw(ring, red); p.Draw(eyes, blk); p.Draw(smile, blk); p.Draw(nose, red);
            Edge(p);
        }

        public static Texture2D Icon(int size = 512)
        {
            float k = size / 512f;
            var p = new Pix(size, size, Color.black);
            p.Fill((x, y) => { float n = Mathf.PerlinNoise(x * .02f / k, y * .02f / k) * .5f; float v = Mathf.Clamp01(1.15f - Vector2.Distance(new Vector2(x, y), new Vector2(size * .5f, size * .55f)) / size); return Color.Lerp(new Color(.03f, .14f, .08f), new Color(.08f, .34f, .19f), v * .9f + n * .15f); });
            for (int i = 0; i < 2; i++)
            {
                float cx = (i == 0 ? 178 : 336) * k, cy = (i == 0 ? 262 : 240) * k, rot = i == 0 ? 9 : -7;
                float cs = Mathf.Cos(rot * Mathf.Deg2Rad), sn = Mathf.Sin(rot * Mathf.Deg2Rad);
                System.Func<float, float, Vector2> L = (x, y) => new Vector2((x - cx) * cs + (y - cy) * sn, -(x - cx) * sn + (y - cy) * cs);
                p.Draw((x, y) => { var v = L(x, y); return Sdf.Box(v.x + 10 * k, v.y + 14 * k, 0, 0, 108 * k, 152 * k, 22 * k); }, new Color(0, 0, 0, .45f), 14 * k);
                p.Draw((x, y) => { var v = L(x, y); return Sdf.Box(v.x, v.y, 0, 0, 108 * k, 152 * k, 22 * k); }, (x, y) => { var v = L(x, y); return Color.Lerp(IvoryDark, Ivory, .5f + v.y / (300 * k)); });
                string s = i == 0 ? "13" : "7"; float h = (i == 0 ? 132 : 150) * k, w = Glyphs.Width(s) * h;
                var ink = Ink[i == 0 ? 0 : 1];
                p.Draw((x, y) => { var v = L(x, y); return Glyphs.Dist(s, v.x, v.y, -w / 2, -42 * k, h) - h * .095f; }, ink);
                p.Draw((x, y) => { var v = L(x, y); return Sdf.Circle(v.x, v.y, 0, -88 * k, 12 * k); }, ink);
            }
            return p.Tex(false, true);
        }
    }
}
