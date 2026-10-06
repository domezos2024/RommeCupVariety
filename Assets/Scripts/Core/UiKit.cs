using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RommeCup.Core
{
    public static class Theme
    {
        public static readonly Color Panel = new Color(.085f, .075f, .065f, .93f), Dark = new Color(.05f, .045f, .04f, .82f),
            Text = new Color(.96f, .93f, .86f), Dim = new Color(.96f, .93f, .86f, .66f), Brass = new Color(.86f, .71f, .42f),
            Line = new Color(.78f, .63f, .37f, .55f), Green = new Color(.17f, .42f, .27f), Blue = new Color(.16f, .29f, .46f),
            Red = new Color(.56f, .15f, .17f), Grey = new Color(.25f, .235f, .22f), Glass = new Color(1, 1, 1, .07f),
            Ink = new Color(.11f, .09f, .07f), Good = new Color(.68f, .9f, .64f), Bad = new Color(1f, .62f, .58f);
    }

    public class SafeArea : MonoBehaviour
    {
        Rect last;
        RectTransform rt;
        void Awake() { rt = (RectTransform)transform; Apply(); }
        void Update() { if (Screen.safeArea != last) Apply(); }
        void Apply()
        {
            last = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(last.xMin / Screen.width, last.yMin / Screen.height);
            rt.anchorMax = new Vector2(last.xMax / Screen.width, last.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    public static class UiKit
    {
        public static Font Font, Serif;
        public static Sprite Round, Vignette;

        public static void Init()
        {
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Serif = Font;
            try
            {
                var have = new HashSet<string>(Font.GetOSInstalledFontNames());
                foreach (var n in new[] { "Noto Serif", "NotoSerif", "Georgia", "Cambria", "Times New Roman", "serif" })
                    if (have.Contains(n)) { Serif = Font.CreateDynamicFontFromOSFont(n, 64); break; }
            }
            catch (Exception e) { Log.W("UiKit", "serif " + e.Message); }
            const int s = 64, r = 12;
            var p = new Pix(s, s, new Color(1, 1, 1, 0));
            p.Draw((x, y) => Sdf.Box(x, y, s / 2f, s / 2f, s / 2f - 1, s / 2f - 1, r), Color.white);
            Round = Sprite.Create(p.Tex(false), new Rect(0, 0, s, s), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            const int v = 128;
            var vp = new Pix(v, v, new Color(0, 0, 0, 0));
            vp.Fill((x, y) => { float dx = (x - v / 2f) / (v / 2f), dy = (y - v / 2f) / (v / 2f); float d = Mathf.Sqrt(dx * dx * .85f + dy * dy); return new Color(0, 0, 0, Mathf.SmoothStep(0, 1, Mathf.Clamp01((d - .55f) / .75f)) * .62f); });
            Vignette = Sprite.Create(vp.Tex(false), new Rect(0, 0, v, v), new Vector2(.5f, .5f));
        }

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var s = go.AddComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920, 1080); s.matchWidthOrHeight = .75f;
            go.AddComponent<GraphicRaycaster>();
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null) { var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>(); }
            return c;
        }

        public static RectTransform SafeRoot(Canvas c)
        {
            var r = Full(c.transform, "Safe");
            r.gameObject.AddComponent<SafeArea>();
            return r;
        }

        public static void VignetteLayer()
        {
            var c = Canvas("Vignette", 1);
            UnityEngine.Object.Destroy(c.GetComponent<GraphicRaycaster>());
            var img = Full(c.transform, "Vignette").gameObject.AddComponent<Image>();
            img.sprite = Vignette; img.raycastTarget = false;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 oMin = default, Vector2 oMax = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }

        public static RectTransform Full(Transform parent, string name) => Rect(parent, name, Vector2.zero, Vector2.one);

        public static void Place(Component c, Vector2 aMin, Vector2 aMax, Vector2 oMin = default, Vector2 oMax = default)
        {
            var rt = (RectTransform)c.transform; rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        }

        public static void Center(Component c, Vector2 anchor, Vector2 size, Vector2 pos = default)
        {
            var rt = (RectTransform)c.transform; rt.anchorMin = rt.anchorMax = anchor; rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        public static Image Box(Transform parent, Color c, bool border = false, string name = "Box")
        {
            var rt = Full(parent, name);
            var img = rt.gameObject.AddComponent<Image>(); img.sprite = Round; img.type = Image.Type.Sliced; img.color = c;
            if (border) { var o = rt.gameObject.AddComponent<Outline>(); o.effectColor = Theme.Line; o.effectDistance = new Vector2(1.5f, -1.5f); }
            return img;
        }

        public static Image Shade(Transform parent, Color c)
        {
            var img = Full(parent, "Shade").gameObject.AddComponent<Image>(); img.color = c; return img;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle st = FontStyle.Bold, bool serif = false)
        {
            var rt = Full(parent, "Label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = serif ? Serif : Font; t.text = text; t.fontSize = size; t.color = c; t.alignment = anchor; t.fontStyle = st;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; t.supportRichText = true;
            var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .45f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        public static Button Button(Transform parent, string text, Action click, Color bg, int size = 34)
        {
            var img = Box(parent, bg, true, "Btn:" + text);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f); cb.pressedColor = new Color(.78f, .78f, .78f); cb.selectedColor = Color.white; cb.disabledColor = new Color(.5f, .5f, .5f, .45f); cb.fadeDuration = .08f; b.colors = cb;
            b.onClick.AddListener(() => { Sfx.Play("click"); Log.I("UI", "button " + text); click?.Invoke(); });
            Label(img.transform, text, size, Theme.Text).name = "Text";
            return b;
        }

        public static void SetText(Button b, string s) => b.transform.Find("Text").GetComponent<Text>().text = s;

        public static void SetColor(Button b, Color c) => b.GetComponent<Image>().color = c;

        public static InputField Input(Transform parent, string value, int size, int limit = 14)
        {
            var img = Box(parent, new Color(0, 0, 0, .35f), true, "Input");
            var inp = img.gameObject.AddComponent<InputField>();
            var t = Label(img.transform, "", size, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Normal);
            t.rectTransform.offsetMin = new Vector2(20, 0); t.rectTransform.offsetMax = new Vector2(-20, 0);
            t.supportRichText = false;
            inp.textComponent = t; inp.characterLimit = limit; inp.text = value; inp.caretColor = Theme.Brass; inp.selectionColor = new Color(.86f, .71f, .42f, .35f);
            return inp;
        }

        public class Stack
        {
            readonly RectTransform parent; float y; readonly float gap;
            public Stack(RectTransform p, float gap = 12, float top = 0) { parent = p; this.gap = gap; y = -top; }
            public T Add<T>(T c, float h) where T : Component
            {
                Place(c, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, y - h), new Vector2(0, y));
                if (c.gameObject.activeSelf) y -= h + gap;
                return c;
            }
            public void Skip(float h) => y -= h;
        }

        public static void Row(RectTransform row, float gap, params Component[] items)
        {
            int n = items.Length;
            for (int i = 0; i < n; i++)
            {
                var rt = (RectTransform)items[i].transform;
                rt.anchorMin = new Vector2(i / (float)n, 0); rt.anchorMax = new Vector2((i + 1) / (float)n, 1);
                rt.offsetMin = new Vector2(i == 0 ? 0 : gap / 2, 0); rt.offsetMax = new Vector2(i == n - 1 ? 0 : -gap / 2, 0);
            }
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) { var g = t.GetChild(i).gameObject; g.SetActive(false); g.transform.SetParent(null, false); UnityEngine.Object.Destroy(g); }
        }

        public static Image Modal(Transform root, Vector2 size, out RectTransform content)
        {
            var shade = Shade(root, new Color(0, 0, 0, .55f));
            var box = Box(shade.transform, Theme.Panel, true, "Modal");
            Center(box, new Vector2(.5f, .5f), size);
            content = (RectTransform)box.transform;
            return shade;
        }
    }
}
