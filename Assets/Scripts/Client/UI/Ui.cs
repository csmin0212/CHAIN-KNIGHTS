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
    /// 코드로 uGUI를 만드는 도우미. 톤은 Docs/아트_컨셉.md "모험가 길드의 서류"를 따른다:
    /// 차콜 패널 + 황동 테두리, 양피지에 잉크 글씨, 결정의 순간은 봉랍과 도장.
    /// 그림 에셋(Assets/Resources/Art)이 있으면 그림을 쓰고, 없으면 도형으로 대신한다.
    /// </summary>
    public static class Ui
    {
        public static readonly Color Ink = Hex("#17181F");
        public static readonly Color PanelColor = Hex("#1F2129");
        public static readonly Color Panel2 = Hex("#2A2C36");
        public static readonly Color Gold = Hex("#C9A96E");
        public static readonly Color GoldHi = Hex("#E6C98A");
        public static readonly Color GoldDark = Hex("#8A6A3A");
        public static readonly Color Paper = Hex("#EFE3C8");
        public static readonly Color Memo = Hex("#F4EAD2");
        public static readonly Color PaperInk = Hex("#3B3226");
        public static readonly Color PaperFaint = Hex("#7A6A50");
        public static readonly Color Wax = Hex("#9B2F25");
        public static readonly Color WaxDark = Hex("#6D1F18");
        public static readonly Color Text = Hex("#ECE6DA");
        public static readonly Color Dim = Hex("#A9A6B3");
        public static readonly Color Good = Hex("#7CCF95");
        public static readonly Color HpGreen = Hex("#5FB37A");
        public static readonly Color HpWarn = Hex("#E0A93B");
        public static readonly Color HpLow = Hex("#C8553D");
        public static readonly Color Danger = Hex("#E2735A");

        private static Font sans;
        private static Font serif;

        /// <summary>본문과 UI 글꼴. 정식 폰트(Noto Sans KR)가 들어오기 전까지 OS 글꼴(맑은 고딕)을 쓴다.</summary>
        public static Font Sans => sans != null ? sans : sans = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Arial" }, 32);

        /// <summary>양피지 글꼴. 정식 폰트(고운바탕)가 들어오기 전까지 바탕을 쓴다.</summary>
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

        public static string ElementKey(Element element) => element.ToString().ToLowerInvariant();

        public static string JobKey(Job job) => job.ToString().ToLowerInvariant();

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

        public static RectTransform Rotate(this RectTransform rect, float degrees)
        {
            rect.localEulerAngles = new Vector3(0, 0, degrees);
            return rect;
        }

        // ───────────── 패널과 종이 ─────────────

        public static Image Box(Transform parent, string name, Color color, int radius = 14)
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Rounded(radius);
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        /// <summary>
        /// 황동 테두리 차콜 패널 (ui_panel). 안쪽 영역(내용을 넣는 곳)을 돌려준다.
        /// fill이나 border를 따로 주면 그림 대신 그 색의 도형으로 그린다.
        /// </summary>
        public static Image Frame(Transform parent, string name, Color? fill = null, Color? border = null, int radius = 14, int borderWidth = 2)
        {
            var outer = Box(parent, name, border ?? Gold, radius);
            if (fill == null && border == null && Art.TryApply(outer, "UI/ui_panel", Color.white, 2f))
            {
                var content = Rect(outer.transform, "Inner").gameObject.AddComponent<Image>();
                content.rectTransform.Fill(inset: 8);
                content.color = Color.clear;
                content.raycastTarget = false;
                return content;
            }
            var inner = Box(outer.transform, "Inner", fill ?? PanelColor, Mathf.Max(1, radius - borderWidth));
            inner.rectTransform.Fill(inset: borderWidth);
            inner.raycastTarget = false;
            return inner;
        }

        /// <summary>양피지 (ui_panel_paper). 글을 적는 종이.</summary>
        public static Image PaperBox(Transform parent, string name, int radius = 12)
        {
            var image = Box(parent, name, Paper, radius);
            Art.TryApply(image, "UI/ui_panel_paper", Color.white, 2f);
            return image;
        }

        /// <summary>
        /// 길드 종이 한 장. 전용 그림(ui_quest_paper, ui_tag_paper, ui_memo_note)이 오면 그것을,
        /// 아직이면 양피지 그림이나 색 상자로 대신한다.
        /// </summary>
        public static Image GuildPaper(Transform parent, string name, string artName, Color fallback, int radius = 4)
        {
            var image = Box(parent, name, fallback, radius);
            if (!Art.TryApply(image, "UI/" + artName, Color.white, 2f) && fallback == Paper)
                Art.TryApply(image, "UI/ui_panel_paper", Color.white, 3f);
            return image;
        }

        /// <summary>황동 압정 (ui_pin). 종이 위에 꽂는다.</summary>
        public static Image Pin(Transform parent, float x, float y, float size = 22)
        {
            var pin = Rect(parent, "Pin").gameObject.AddComponent<Image>();
            pin.rectTransform.At(0.5f, 1, x, y, size, size);
            pin.raycastTarget = false;
            if (!Art.TryApply(pin, "UI/ui_pin", Color.white))
            {
                pin.sprite = UiSprites.Circle;
                pin.color = GoldDark;
                var head = Circle(pin.transform, "Head", Gold);
                head.rectTransform.Fill(inset: 3);
                head.raycastTarget = false;
            }
            return pin;
        }

        /// <summary>
        /// 고무 도장 (ui_stamp_ring + 글자). 연계, 격파, 완료, 랭크 표시.
        /// 반환값은 도장 전체 (회전, 크기 연출용).
        /// </summary>
        public static RectTransform Stamp(Transform parent, string text, Color color, float size, int fontSize)
        {
            var ring = Rect(parent, "Stamp").gameObject.AddComponent<Image>();
            ring.rectTransform.sizeDelta = new Vector2(size, size);
            ring.raycastTarget = false;
            if (!Art.TryApply(ring, "UI/ui_stamp_ring", color))
            {
                ring.sprite = UiSprites.Ring(10);
                ring.color = color;
            }
            var label = Label(ring.transform, text, fontSize, color, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            label.rectTransform.Fill(inset: size * 0.12f);
            return ring.rectTransform;
        }

        public static Image Circle(Transform parent, string name, Color color)
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Circle;
            image.color = color;
            return image;
        }

        /// <summary>아이콘 그림. 없으면 null (호출한 쪽에서 글자로 대신한다).</summary>
        public static Image Icon(Transform parent, string path, float size)
        {
            var sprite = Art.Get(path);
            if (sprite == null)
                return null;
            var image = Rect(parent, "Icon").gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        /// <summary>황동 주화 (ui_cost_coin, 없으면 카드 코스트 배지).</summary>
        public static Image Coin(Transform parent, float size)
        {
            var coin = Rect(parent, "Coin").gameObject.AddComponent<Image>();
            coin.rectTransform.sizeDelta = new Vector2(size, size);
            coin.raycastTarget = false;
            if (!Art.TryApply(coin, "UI/ui_cost_coin", Color.white) && !Art.TryApply(coin, "Cards/card_cost_badge", Color.white))
            {
                coin.sprite = UiSprites.Circle;
                coin.color = Gold;
            }
            coin.preserveAspect = true;
            return coin;
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

        /// <summary>황동 이름표 (카드 이름, 작은 머리표).</summary>
        public static Text Pill(Transform parent, string text, int size, out Image background)
        {
            background = Box(parent, "Plate", GoldHi, 5);
            var label = Label(background.transform, text, size, Hex("#2B2114"), TextAnchor.MiddleCenter, bold: true, serifFont: true);
            label.rectTransform.Fill(inset: 2);
            return label;
        }

        public static Outline AddOutline(this Graphic graphic, float alpha = 0.85f, float distance = 2)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, alpha);
            outline.effectDistance = new Vector2(distance, -distance);
            return outline;
        }

        public static Shadow AddShadow(this Graphic graphic, float alpha = 0.55f, float distance = 4)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, alpha);
            shadow.effectDistance = new Vector2(distance, -distance);
            return shadow;
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
            button.onClick.AddListener(() =>
            {
                Sound.Play("sfx_button");
                onClick();
            });
            return button;
        }

        /// <summary>황동 테두리 버튼 (ui_button_normal / pressed / disabled).</summary>
        public static Button FrameButton(Transform parent, string text, int size, Action onClick, out Text label)
        {
            var outer = Box(parent, text, Gold, 12);
            Image inner = null;
            if (!Art.TryApply(outer, "UI/ui_button_normal", Color.white, 1.5f))
            {
                inner = Box(outer.transform, "Inner", PanelColor, 10);
                inner.rectTransform.Fill(inset: 2);
                inner.raycastTarget = false;
            }
            label = Label(outer.transform, text, size, Text, TextAnchor.MiddleCenter, bold: true);
            var button = MakeButton(outer, onClick);
            var pressed = Art.Get("UI/ui_button_pressed");
            var disabled = Art.Get("UI/ui_button_disabled");
            if (inner == null && pressed != null && disabled != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { pressedSprite = pressed, disabledSprite = disabled, highlightedSprite = outer.sprite, selectedSprite = outer.sprite };
            }
            return button;
        }

        /// <summary>
        /// 봉랍 도장 버튼 (ui_wax_seal). 확정, 출격처럼 결정을 내리는 버튼에만 쓴다.
        /// 그림이 없으면 빨간 원 두 겹으로 그린다.
        /// </summary>
        public static Button SealButton(Transform parent, string text, int size, Action onClick, out Text label)
        {
            var seal = Rect(parent, text).gameObject.AddComponent<Image>();
            if (!Art.TryApply(seal, "UI/ui_wax_seal", Color.white))
            {
                seal.sprite = UiSprites.Circle;
                seal.color = WaxDark;
                var face = Circle(seal.transform, "Face", Wax);
                face.rectTransform.Fill(inset: 6);
                face.raycastTarget = false;
                var rim = Rect(face.transform, "Rim").gameObject.AddComponent<Image>();
                rim.sprite = UiSprites.Ring(5);
                rim.color = Memo.WithAlpha(0.22f);
                rim.rectTransform.Fill(inset: 10);
                rim.raycastTarget = false;
            }
            seal.preserveAspect = true;
            label = Label(seal.transform, text, size, Memo, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            label.AddOutline(0.35f, 1);
            return MakeButton(seal, onClick);
        }

        // ───────────── HP 바 ─────────────

        /// <summary>
        /// HP 바: 황동 테두리 + 어두운 홈 + 채움. 채움 Image를 돌려준다.
        /// (ui_hp_frame / ui_hp_fill 그림은 위아래 여백이 커서 얇은 바에서는 선처럼 보여 도형으로 그린다.)
        /// </summary>
        public static Image Bar(Transform parent, Color fill)
        {
            var rim = Box(parent, "Bar", GoldDark, 6);
            rim.rectTransform.Fill();
            var back = Box(rim.transform, "Groove", Hex("#0C0D12"), 5);
            back.rectTransform.Fill(inset: 1);
            back.raycastTarget = false;
            var fillImage = Box(back.transform, "Fill", fill, 5);
            fillImage.rectTransform.Fill(inset: 1);
            fillImage.raycastTarget = false;
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

        /// <summary>
        /// 그림이 있으면 그림을 얹고 true. 흰색 계열 그림은 tint로 물들인다.
        /// borderScale은 9-slice 테두리를 몇 배 얇게 그릴지 (작은 칸에 큰 테두리 그림을 쓸 때).
        /// </summary>
        public static bool TryApply(Image image, string path, Color tint, float borderScale = 1f)
        {
            var sprite = Get(path);
            if (sprite == null)
                return false;
            image.sprite = sprite;
            bool sliced = sprite.border != Vector4.zero;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = sliced ? borderScale : 1f;
            image.preserveAspect = !sliced;
            image.color = tint;
            return true;
        }
    }
}
