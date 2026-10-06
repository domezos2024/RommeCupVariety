using UnityEngine;
using UnityEngine.Rendering;

namespace RommeCup.Core
{
    public static class Env
    {
        const string M = "Env";
        public static Camera Cam;
        public static Light Key;
        public static readonly Color Room = new Color(.045f, .038f, .033f);
        static Texture2D woodA, woodN, feltA, feltN, floorA, floorN;

        public static void Init()
        {
            Cam = new GameObject("Camera").AddComponent<Camera>();
            Cam.tag = "MainCamera";
            Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = Room;
            Cam.fieldOfView = 34; Cam.nearClipPlane = .5f; Cam.farClipPlane = 160; Cam.allowMSAA = true; Cam.allowHDR = false;
            Cam.gameObject.AddComponent<AudioListener>();
            QualitySettings.antiAliasing = 4; QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.StableFit; QualitySettings.shadowCascades = 2; QualitySettings.shadowDistance = 55;
            QualitySettings.pixelLightCount = 2;
            Key = new GameObject("Lamp").AddComponent<Light>();
            Key.type = LightType.Directional; Key.color = new Color(1f, .9f, .78f); Key.intensity = 1.25f;
            Key.shadows = LightShadows.Soft; Key.shadowStrength = .82f; Key.shadowBias = .02f; Key.shadowNormalBias = .18f;
            Key.transform.rotation = Quaternion.Euler(57, 28, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.40f, .38f, .36f);
            RenderSettings.ambientEquatorColor = new Color(.24f, .21f, .18f);
            RenderSettings.ambientGroundColor = new Color(.07f, .06f, .05f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = Reflection();
            RenderSettings.reflectionIntensity = .5f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .018f; RenderSettings.fogColor = Room;
            Log.I(M, "lighting ready");
        }

        static Cubemap Reflection()
        {
            const int s = 64;
            var cube = new Cubemap(s, TextureFormat.RGBA32, true);
            var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
            var px = new Color[s * s];
            foreach (var f in faces)
            {
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + .5f) / s * 2 - 1, v = (y + .5f) / s * 2 - 1;
                        Vector3 d = f switch
                        {
                            CubemapFace.PositiveX => new Vector3(1, -v, -u),
                            CubemapFace.NegativeX => new Vector3(-1, -v, u),
                            CubemapFace.PositiveY => new Vector3(u, 1, v),
                            CubemapFace.NegativeY => new Vector3(u, -1, -v),
                            CubemapFace.PositiveZ => new Vector3(u, -v, 1),
                            _ => new Vector3(-u, -v, -1)
                        };
                        d.Normalize();
                        var lampDir = -(Quaternion.Euler(57, 28, 0) * Vector3.forward);
                        float lamp = Mathf.Pow(Mathf.Max(0, Vector3.Dot(d, lampDir)), 120) * 2.5f;
                        float win = d.x < -.55f && d.y > -.05f && d.y < .5f && Mathf.Abs(d.z) < .45f ? .9f : 0;
                        float wall = Mathf.Lerp(.05f, .16f, Mathf.Clamp01(d.y * .5f + .5f));
                        var c = new Color(wall * 1.05f, wall * .92f, wall * .8f) + new Color(1f, .9f, .75f) * lamp + new Color(.55f, .62f, .72f) * win;
                        if (d.y < -.2f) c = Color.Lerp(c, new Color(.09f, .06f, .04f), Mathf.Clamp01(-d.y * 2));
                        px[y * s + x] = c;
                    }
                cube.SetPixels(px, f);
            }
            cube.Apply(true, true);
            return cube;
        }

        public static void Textures()
        {
            if (woodA) return;
            WoodTex(512, out woodA, out woodN, 11);
            FeltTex(256, out feltA, out feltN);
            FloorTex(512, out floorA, out floorN);
        }

        static void WoodTex(int s, out Texture2D a, out Texture2D n, int seed)
        {
            var p = new Pix(s, s, Color.black);
            var h = new float[s * s];
            var dark = new Color(.2f, .11f, .06f); var light = new Color(.33f, .19f, .1f);
            float ox = seed * 13.7f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = x / (float)s, fy = y / (float)s;
                    float warp = (Mathf.PerlinNoise(fx * 2 + ox, fy * 1.5f) - .5f) * .14f + (Mathf.PerlinNoise(fx * 7, fy * 4 + ox) - .5f) * .06f;
                    float ring = Mathf.Repeat((fy + warp) * 14, 1f);
                    float band = Mathf.SmoothStep(0, 1, Mathf.Abs(ring - .5f) * 2);
                    float fiber = Mathf.PerlinNoise(fx * 4 + ox, (fy + warp) * 260) * .6f + Mathf.PerlinNoise(fx * 12, (fy + warp) * 520) * .4f;
                    float pore = Mathf.Clamp01((Mathf.PerlinNoise(fx * 90 + ox, (fy + warp) * 700) - .62f) * 4);
                    float tone = Mathf.PerlinNoise(fx * 1.2f + ox, fy * 2.5f) * .25f;
                    float v = Mathf.Clamp01(band * .16f + fiber * .6f + tone - pore * .3f);
                    p.C[y * s + x] = Color.Lerp(dark, light, v);
                    h[y * s + x] = fiber * .5f - pore;
                }
            a = p.Tex(true, false, TextureWrapMode.Repeat);
            n = Pix.Normal(h, s, s, 1.2f, TextureWrapMode.Repeat);
        }

        static void FeltTex(int s, out Texture2D a, out Texture2D n)
        {
            var p = new Pix(s, s, Color.black);
            var h = new float[s * s];
            var r = new System.Random(5);
            var c0 = new Color(.05f, .24f, .14f); var c1 = new Color(.075f, .31f, .18f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = x / (float)s, fy = y / (float)s, fib = (float)r.NextDouble();
                    float big = Mathf.PerlinNoise(fx * 4 + 3, fy * 4 + 7) * .5f + Mathf.PerlinNoise(fx * 4 * 4 + 1, fy * 16) * .2f;
                    float v = Mathf.Clamp01(big * .8f + fib * .35f);
                    p.C[y * s + x] = Color.Lerp(c0, c1, v);
                    h[y * s + x] = fib;
                }
            a = p.Tex(true, false, TextureWrapMode.Repeat);
            n = Pix.Normal(h, s, s, .9f, TextureWrapMode.Repeat);
        }

        static void FloorTex(int s, out Texture2D a, out Texture2D n)
        {
            var p = new Pix(s, s, Color.black);
            var h = new float[s * s];
            const int planks = 6;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fy = y / (float)s * planks; int pi = (int)fy;
                    float off = Noise.Hash(pi, 3) * 3, fx = x / (float)s * 1.0f + off;
                    int seg = Mathf.FloorToInt(fx * 2);
                    float lx = Mathf.Repeat(fx * 2, 1), ly = fy - pi;
                    float g = Mathf.Repeat(ly * 3 + Noise.Fbm(fx * 4, ly * 2 + pi) * 2.5f, 1);
                    float tone = Noise.Hash(pi, seg) * .25f;
                    var c = Color.Lerp(new Color(.2f, .12f, .07f), new Color(.34f, .21f, .12f), Mathf.Clamp01(Mathf.Abs(g - .5f) * 1.4f + tone));
                    bool gap = ly < .02f || ly > .98f || lx < .006f;
                    if (gap) c *= .35f;
                    p.C[y * s + x] = c;
                    h[y * s + x] = gap ? -1 : g * .2f;
                }
            a = p.Tex(true, false, TextureWrapMode.Repeat);
            n = Pix.Normal(h, s, s, 1.5f, TextureWrapMode.Repeat);
        }

        public static GameObject Cube(Transform parent, string n, Vector3 pos, Vector3 size, Material m, bool shadows = true)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = n; c.transform.SetParent(parent); c.transform.position = pos; c.transform.localScale = size;
            Object.Destroy(c.GetComponent<Collider>());
            var r = c.GetComponent<Renderer>(); r.material = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return c;
        }

        public static Material Wood(float tile) { Textures(); var m = Mats.Lit(Color.white, 0, .62f, woodA, woodN, .6f); m.mainTextureScale = new Vector2(tile, tile); return m; }

        public static void BuildTable(Transform parent, Vector3 feltCenter, float feltW, float feltD, Rect top)
        {
            Textures();
            var felt = Mats.Lit(Color.white, 0, .08f, feltA, feltN, .45f);
            felt.mainTextureScale = new Vector2(feltW / 2.2f, feltD / 2.2f);
            var wood = Mats.Lit(Color.white, 0, .7f, woodA, woodN, .55f);
            wood.mainTextureScale = new Vector2(top.width / 16f, top.height / 16f);
            var edge = Mats.Lit(Color.white, 0, .7f, woodA, woodN, .55f);
            Cube(parent, "TableTop", new Vector3(top.center.x, -.35f, top.center.y), new Vector3(top.width, .7f, top.height), wood).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Cube(parent, "Felt", feltCenter + new Vector3(0, -.03f, 0), new Vector3(feltW, .07f, feltD), felt);
            var inlay = Mats.Lit(new Color(.62f, .48f, .26f), .7f, .55f);
            float t = .07f;
            Cube(parent, "InlayN", feltCenter + new Vector3(0, -.005f, feltD / 2 + t / 2), new Vector3(feltW + 2 * t, .015f, t), inlay, false);
            Cube(parent, "InlayS", feltCenter + new Vector3(0, -.005f, -feltD / 2 - t / 2), new Vector3(feltW + 2 * t, .015f, t), inlay, false);
            Cube(parent, "InlayW", feltCenter + new Vector3(-feltW / 2 - t / 2, -.005f, 0), new Vector3(t, .015f, feltD), inlay, false);
            Cube(parent, "InlayE", feltCenter + new Vector3(feltW / 2 + t / 2, -.005f, 0), new Vector3(t, .015f, feltD), inlay, false);
            Cube(parent, "Apron", new Vector3(top.center.x, -1.2f, top.center.y), new Vector3(top.width - 1, 1, top.height - 1), edge);
            var fm = Mats.Lit(Color.white, 0, .55f, floorA, floorN, .8f); fm.mainTextureScale = new Vector2(12, 12);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor"; floor.transform.SetParent(parent); floor.transform.position = new Vector3(0, -9f, 0); floor.transform.localScale = new Vector3(16, 1, 16);
            Object.Destroy(floor.GetComponent<Collider>());
            floor.GetComponent<Renderer>().material = fm;
        }

        public static void Fit(Vector3[] pts, float pitch, Vector2 ax, Vector2 ay, out Vector3 pos, out Quaternion rot, out Vector3 focus, out float distance, float yaw = 0, float fov = 34)
        {
            var target = Vector3.zero; foreach (var p in pts) target += p; target /= pts.Length; target.y = 0;
            float dist = 25;
            Cam.fieldOfView = fov; RenderSettings.fogDensity = .018f * Mathf.Tan(fov * .5f * Mathf.Deg2Rad) / Mathf.Tan(17 * Mathf.Deg2Rad);
            rot = Quaternion.Euler(pitch, yaw, 0);
            Cam.transform.rotation = rot;
            var right = rot * Vector3.right; var fwd = Vector3.ProjectOnPlane(rot * Vector3.forward, Vector3.up).normalized;
            for (int it = 0; it < 70; it++)
            {
                Cam.transform.position = target - rot * Vector3.forward * dist;
                float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f;
                foreach (var p in pts) { var v = Cam.WorldToViewportPoint(p); x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y); }
                float s = Mathf.Max((x1 - x0) / (ax.y - ax.x), (y1 - y0) / (ay.y - ay.x));
                dist *= Mathf.Lerp(1, s, .5f);
                target += right * (((x0 + x1) / 2 - (ax.x + ax.y) / 2) * dist * .6f) + fwd * (((y0 + y1) / 2 - (ay.x + ay.y) / 2) * dist * .6f);
            }
            pos = target - rot * Vector3.forward * dist;
            Cam.transform.position = pos;
            focus = target; distance = dist;
            QualitySettings.shadowDistance = dist + 22;
            Log.I(M, "fit pitch=" + pitch + " fov=" + fov + " dist=" + dist.ToString("0.0"));
        }
    }
}
