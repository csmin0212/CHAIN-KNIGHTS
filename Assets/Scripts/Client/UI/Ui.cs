using System;
using System.Collections.Generic;
using ChainKnights.Core.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ChainKnights.Client.UI
{
    /// <summary>
    /// 코드로 uGUI를 만드는 도우미. 톤은 Docs/UI_목업_전투.html을 따른다:
    /// 짙은 차콜 패널 + 얇은 금색 테두리, 크림색 양피지, 금색 알약 라벨.
    /// </summary>
    public static class Ui
    {
        public static readonly Color Ink = Hex("#17181F");
        public static readonly Color PanelColor = Hex("#1F2129");
        public static readonly Color Panel2 = Hex("#2A2C36");
        public static readonly Color Gold = Hex("#C9A96E");
        public static readonly Color GoldHi = Hex("#E6C98A");
        public static readonly Color GoldDark = Hex("#B8945A");
        public static readonly Color Paper = Hex("#EFE3C8");
        public static readonly Color PaperInk = Hex("#3B3226");
        public static readonly Color Text = Hex("#ECE6DA");
        public static readonly Color Dim = Hex("#A9A6B3");
        public static readonly Color Good = Hex("#7CCF95");
        public static readonly Color HpGreen = Hex("#5FB37A");
        public static readonly Color HpWarn = Hex("#E0A93B");
        public static readonly Color HpLow = Hex("#C8553D");
        public static readonly Color Danger = Hex("#E2735A");

        private static Font sans;
        private static Font serif;

        /// <summary>본문과 UI 글꼴. 정식 폰트가 들어오기 전까지 OS 글꼴(맑은 고딕)을 쓴다.</summary>
        public static Font Sans => sans != null ? sans : sans = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Arial" }, 32);

        /// <summary>양피지 글꼴 (바탕).</summary>
        public static Font Serif => serif != null ? serif : serif = Font.CreateDynamicFontFromOSFont(new[] { "Batang", "바탕", "AppleMyungjo", "Malgun Gothic" }, 32);

        public static Color ElementColor(Element element)
        {
            switch (element)
            {
                case Element.Fire: return Hex("#D9583B");
                case Element.Ice: return Hex("#4C9BD6");
                case Element.Wind: return Hex("#4FA36B");
                case Element.Light: return Hex("#E3B53B");
                default: return Hex("#8A72C4");
            }
        }

        public static Color JobColor(Job job)
        {
            switch (job)
            {
                case Job.Warrior: return Hex("#D9583B");
                case Job.Guardian: return Hex("#4C9BD6");
                case Job.Mage: return Hex("#8A72C4");
                case Job.Priest: return Hex("#E3B53B");
                default: return Hex("#7A7D8C");
            }
        }

        public static Color HpColor(float ratio) => ratio > 0.5f ? HpGreen : ratio > 0.25f ? HpWarn : HpLow;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }

        public static Color WithAlpha(this Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        // ───────────── 캔버스 ─────────────

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // ───────────── 자리 잡기 ─────────────

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>부모를 비율(0~1)로 채운다. inset만큼 안쪽으로 들인다.</summary>
        public static RectTransform Fill(this RectTransform rect, float xMin = 0, float yMin = 0, float xMax = 1, float yMax = 1, float inset = 0)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        /// <summary>부모의 기준점(anchor, 0~1)에서 (x, y)만큼 떨어진 곳에 w×h 크기로 놓는다. 중심 기준.</summary>
        public static RectTransform At(this RectTransform rect, float anchorX, float anchorY, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, anchorY);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        // ───────────── 패널 ─────────────

        public static Image Box(Transform parent, string name, Color color, int radius = 14)
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Rounded(radius);
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        /// <summary>금색 테두리가 있는 차콜 패널. 안쪽 패널(내용을 넣는 곳)을 돌려준다.</summary>
        public static Image Frame(Transform parent, string name, Color? fill = null, Color? border = null, int radius = 14, int borderWidth = 2)
        {
            var outer = Box(parent, name, border ?? Gold, radius);
            var inner = Box(outer.transform, "Inner", fill ?? PanelColor, Mathf.Max(1, radius - borderWidth));
            inner.rectTransform.Fill(inset: borderWidth);
            inner.raycastTarget = false;
            return inner;
        }

        public static Image PaperBox(Transform parent, string name, int radius = 12) => Box(parent, name, Paper, radius);

        public static Image Circle(Transform parent, string name, Color color)
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Circle;
            image.color = color;
            return image;
        }

        // ───────────── 글자 ─────────────

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, bool serifFont = false)
        {
            var label = Rect(parent, "Label").gameObject.AddComponent<Text>();
            label.font = serifFont ? Serif : Sans;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.rectTransform.Fill();
            return label;
        }

        /// <summary>금색 알약 라벨 (이름표, 턴 배너).</summary>
        public static Text Pill(Transform parent, string text, int size, out Image background)
        {
            background = Box(parent, "Pill", GoldHi, 30);
            var label = Label(background.transform, text, size, Hex("#2B2114"), TextAnchor.MiddleCenter, bold: true);
            label.rectTransform.Fill(inset: 2);
            return label;
        }

        // ───────────── 버튼 ─────────────

        public static Button MakeButton(Image target, Action onClick)
        {
            var button = target.gameObject.AddComponent<Button>();
            target.raycastTarget = true;
            button.targetGraphic = target;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>차콜 + 금 테두리 버튼.</summary>
        public static Button FrameButton(Transform parent, string text, int size, Action onClick, out Text label)
        {
            var outer = Box(parent, text, Gold, 12);
            var inner = Box(outer.transform, "Inner", PanelColor, 10);
            inner.rectTransform.Fill(inset: 2);
            inner.raycastTarget = false;
            label = Label(inner.transform, text, size, Text, TextAnchor.MiddleCenter, bold: true);
            return MakeButton(outer, onClick);
        }

        /// <summary>금색 알약 버튼 (확정, 출격 같은 주요 행동).</summary>
        public static Button GoldButton(Transform parent, string text, int size, Action onClick, out Text label)
        {
            label = Pill(parent, text, size, out var background);
            return MakeButton(background, onClick);
        }

        // ───────────── HP 바 ─────────────

        public static Image Bar(Transform parent, Color fill)
        {
            var back = Box(parent, "Bar", Hex("#0C0D12"), 6);
            var fillImage = Box(back.transform, "Fill", fill, 6);
            fillImage.rectTransform.Fill(inset: 1);
            return fillImage;
        }

        public static void SetBar(Image fill, float ratio)
        {
            var rect = fill.rectTransform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);
            fill.color = HpColor(ratio);
            fill.enabled = ratio > 0.001f;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 그림 에셋. Assets/Resources/Art/ 아래에 Docs/에셋_목록.md의 파일 이름대로 넣으면 자동으로 쓰인다.
    /// 없으면 null을 돌려주고, 화면은 도형(임시 그래픽)으로 그린다.
    /// </summary>
    public static class Art
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string path)
        {
            if (!cache.TryGetValue(path, out var sprite))
            {
                sprite = Resources.Load<Sprite>("Art/" + path);
                cache[path] = sprite;
            }
            return sprite;
        }

        /// <summary>그림이 있으면 그림을 얹고 true. 흰색 계열 그림은 tint로 물들인다.</summary>
        public static bool TryApply(Image image, string path, Color tint)
        {
            var sprite = Get(path);
            if (sprite == null)
                return false;
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = sprite.border == Vector4.zero;
            image.color = tint;
            return true;
        }
    }
}
