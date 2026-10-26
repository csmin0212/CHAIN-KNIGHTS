using System;
using System.Collections.Generic;
using System.Linq;
using ChainKnights.Client.UI;
using ChainKnights.Core.Battle;
using ChainKnights.Core.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChainKnights.Client.Battle
{
    /// <summary>카드 한 장. 길드가 발급한 기술 증서: 황동 테두리 + 속성 띠 + 양피지 효과문.</summary>
    public static class CardWidget
    {
        public const float Width = 230;
        public const float Height = 322;

        /// <summary>손패 카드.</summary>
        public static RectTransform Build(Transform parent, CardInstance card, Job arthurJob, bool selected, bool usable, Action onClick)
        {
            var def = card.Card;
            var lines = CardText.DescribeSkill(def.SkillFor(arthurJob), card.Level, def.MaxLevel).ToList();
            var root = BuildFace(parent, def, string.Join("\n", lines), lines.Count > 2 ? 13 : 15,
                $"{CardText.JobName(def.Job)} · Lv {card.Level}", def.IsAwakenedFor(arthurJob), selected);

            var body = root.Find("Body").GetComponent<Image>();
            Ui.MakeButton(body, onClick);
            // 낼 수 없는 카드는 반투명 대신 어둡게 덮는다. 겹친 카드가 비쳐 보이지 않게.
            if (!usable && !selected)
            {
                var shade = Ui.Box(root, "Shade", Ui.Ink.WithAlpha(0.55f), 14);
                shade.rectTransform.Fill();
                shade.raycastTarget = false;
            }
            return root;
        }

        /// <summary>공개 연출의 카드 앞면. 효과문 자리에 누가 냈는지 적는다. scale로 줄여 쓴다.</summary>
        public static RectTransform BuildReveal(Transform parent, CardDefinition def, string owner, bool awakened, float scale)
        {
            var root = BuildFace(parent, def, owner, 22, CardText.JobName(def.Job), awakened, false);
            root.localScale = Vector3.one * scale;
            return root;
        }

        private static RectTransform BuildFace(Transform parent, CardDefinition def, string effect, int effectSize, string footer, bool awakened, bool selected)
        {
            var root = Ui.Rect(parent, def.NameKo);
            root.sizeDelta = new Vector2(Width, Height);

            if (selected)
            {
                var glow = Ui.Box(root, "Selected", Ui.GoldHi.WithAlpha(0.6f), 20);
                glow.rectTransform.Fill(inset: -8);
                glow.raycastTarget = false;
            }

            // 황동 테두리 그림이 없으면 금색 판을 깔고 그 위에 몸통을 얹는다.
            var frameArt = Art.Get("Cards/card_frame");
            var edge = Ui.Box(root, "Edge", frameArt != null ? Color.clear : (selected ? Ui.GoldHi : Ui.Gold), 14);
            edge.rectTransform.Fill();
            edge.raycastTarget = false;

            var body = Ui.Box(root, "Body", Ui.PanelColor, 12);
            body.rectTransform.Fill(inset: 3);

            var band = Ui.Box(body.transform, "Band", Ui.ElementColor(def.Element), 10);
            band.rectTransform.Fill(0, 1, 1, 1);
            band.rectTransform.offsetMin = new Vector2(0, -52);
            band.raycastTarget = false;
            var stars = Ui.Label(band.transform, CardText.Stars(def.Rarity), 16, Ui.Hex("#FFF8E6"), TextAnchor.MiddleRight);
            stars.rectTransform.offsetMax = new Vector2(-16, 0);
            stars.AddOutline(0.35f, 1);
            var elementIcon = Ui.Icon(band.transform, $"Icons/icon_element_{Ui.ElementKey(def.Element)}", 34);
            if (elementIcon != null)
                elementIcon.rectTransform.At(0.5f, 0.5f, 0, 0, 34, 34);

            Ui.Pill(body.transform, def.NameKo, def.NameKo.Length > 9 ? 14 : 16, out var plate);
            plate.rectTransform.At(0.5f, 1, 0, -68, Width - 40, 30);
            plate.raycastTarget = false;

            var art = Ui.Box(body.transform, "Art", Ui.Panel2, 8);
            art.rectTransform.At(0.5f, 1, 0, -130, Width - 40, 80);
            art.raycastTarget = false;
            if (!Art.TryApply(art, $"Cards/card_{def.Id}", Color.white))
            {
                var jobIcon = def.Job != Job.None ? Ui.Icon(art.transform, $"Icons/icon_job_{Ui.JobKey(def.Job)}", 62) : null;
                if (jobIcon != null)
                    jobIcon.rectTransform.At(0.5f, 0.5f, 0, 0, 62, 62);
                else
                    Ui.Label(art.transform, CardText.JobName(def.Job), 20, Ui.JobColor(def.Job).WithAlpha(0.8f), TextAnchor.MiddleCenter, bold: true);
            }

            var paper = Ui.PaperBox(body.transform, "Effect", 6);
            paper.rectTransform.Fill();
            paper.rectTransform.offsetMin = new Vector2(16, 50);
            paper.rectTransform.offsetMax = new Vector2(-14, -178);
            paper.raycastTarget = false;
            var effectLabel = Ui.Label(paper.transform, effect, effectSize, Ui.PaperInk, TextAnchor.MiddleCenter, bold: effectSize > 18, serifFont: true);
            effectLabel.rectTransform.Fill(inset: 8);

            // 바닥 줄은 황동 테두리 장식에 가리지 않게 안쪽으로 들인다.
            var foot = Ui.Label(body.transform, footer, 14, Ui.Dim, TextAnchor.MiddleLeft);
            foot.rectTransform.Fill(0, 0, 1, 0);
            foot.rectTransform.offsetMin = new Vector2(24, 18);
            foot.rectTransform.offsetMax = new Vector2(-20, 44);
            if (awakened)
            {
                var awake = Ui.Label(body.transform, "◆ 각성", 15, Ui.GoldHi, TextAnchor.MiddleRight, bold: true);
                awake.rectTransform.Fill(0, 0, 1, 0);
                awake.rectTransform.offsetMin = new Vector2(24, 18);
                awake.rectTransform.offsetMax = new Vector2(-24, 44);
            }

            if (frameArt != null)
            {
                // 테두리 그림은 카드와 비율이 같아서 통째로 늘려 쓴다.
                var frame = Ui.Rect(root, "Frame").gameObject.AddComponent<Image>();
                frame.sprite = frameArt;
                frame.type = Image.Type.Simple;
                frame.rectTransform.Fill();
                frame.raycastTarget = false;
                if (selected)
                    frame.color = new Color(1f, 0.95f, 0.8f);
            }
            if (awakened)
            {
                var glow = Ui.Rect(root, "Awaken").gameObject.AddComponent<Image>();
                // 내 직업 카드는 대부분 각성이라 빛은 은은하게만.
                if (Art.TryApply(glow, "Cards/card_awaken_glow", Color.white.WithAlpha(0.4f)))
                {
                    glow.type = Image.Type.Simple;
                    glow.preserveAspect = false;
                    glow.rectTransform.Fill(inset: -4);
                    glow.raycastTarget = false;
                }
                else
                {
                    UnityEngine.Object.Destroy(glow.gameObject);
                }
            }

            // 코스트 주화 (왼쪽 위 모서리)
            var coin = Ui.Coin(root, 56);
            coin.rectTransform.At(0, 1, 30, -30, 56, 56);
            var cost = Ui.Label(coin.transform, def.Cost.ToString(), 26, Ui.PaperInk, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            cost.rectTransform.offsetMin = new Vector2(0, 2);
            return root;
        }

        /// <summary>뒷면 (길드 문장이 찍힌 봉투). 공개 연출에서 뒤집기 전.</summary>
        public static RectTransform BuildBack(Transform parent, float width, float height)
        {
            var outer = Ui.Box(parent, "CardBack", Ui.Gold, 12);
            outer.rectTransform.sizeDelta = new Vector2(width, height);
            if (Art.TryApply(outer, "Cards/card_back", Color.white))
            {
                outer.preserveAspect = false;
                return outer.rectTransform;
            }
            var inner = Ui.Box(outer.transform, "Inner", Ui.Ink, 10);
            inner.rectTransform.Fill(inset: 3);
            var mark = Ui.Label(inner.transform, "◆", (int)(width * 0.3f), Ui.Gold, TextAnchor.MiddleCenter);
            mark.color = Ui.Gold.WithAlpha(0.6f);
            return outer.rectTransform;
        }
    }

    /// <summary>파티 칸의 아서 한 명. 모험가 등록증: 초상화, 이름, HP, 선택을 마치면 초록 봉랍.</summary>
    public sealed class PartyWidget
    {
        public RectTransform Root;
        private Image border, highlight, readySeal;
        private Text nameLabel, hpLabel, readyLabel, statusLabel;
        private Image hpFill;
        private RectTransform statusRow;
        private CanvasGroup group;

        public static PartyWidget Build(Transform parent, Arthur arthur, bool isPlayer, Action onClick)
        {
            var w = new PartyWidget();
            w.Root = Ui.Rect(parent, arthur.Name);
            w.group = w.Root.gameObject.AddComponent<CanvasGroup>();

            w.highlight = Ui.Box(w.Root, "Target", Ui.GoldHi.WithAlpha(0.55f), 18);
            w.highlight.rectTransform.Fill(inset: -6);
            w.highlight.raycastTarget = false;

            var outer = Ui.Box(w.Root, "Card", isPlayer ? Ui.GoldHi : Ui.Gold, 14);
            outer.rectTransform.Fill();
            w.border = outer;
            if (!Art.TryApply(outer, "UI/ui_panel", Color.white, 2f))
            {
                var inner = Ui.Box(outer.transform, "Inner", Ui.PanelColor, 12);
                inner.rectTransform.Fill(inset: 2);
                inner.raycastTarget = false;
            }

            var edge = Ui.Box(outer.transform, "PortraitEdge", Ui.GoldDark, 8);
            edge.rectTransform.At(0, 0.5f, 52, 0, 78, 78);
            edge.raycastTarget = false;
            var portrait = Ui.Box(edge.transform, "Portrait", Ui.JobColor(arthur.Job), 6);
            portrait.rectTransform.Fill(inset: 2);
            portrait.raycastTarget = false;
            if (Art.TryApply(portrait, $"Arthurs/arthur_portrait_{Ui.JobKey(arthur.Job)}", Color.white))
                portrait.preserveAspect = false;
            else
                Ui.Label(portrait.transform, CardText.JobName(arthur.Job).Substring(0, 1), 30, Ui.Ink, TextAnchor.MiddleCenter, bold: true);

            w.nameLabel = Ui.Label(outer.transform, arthur.Name, 20, isPlayer ? Ui.GoldHi : Ui.Text, TextAnchor.UpperLeft, bold: true, serifFont: true);
            w.nameLabel.rectTransform.offsetMin = new Vector2(104, 0);
            w.nameLabel.rectTransform.offsetMax = new Vector2(-10, -14);

            w.readyLabel = Ui.Label(outer.transform, "", 14, Ui.Dim, TextAnchor.UpperRight, bold: true);
            w.readyLabel.rectTransform.offsetMin = new Vector2(104, 0);
            w.readyLabel.rectTransform.offsetMax = new Vector2(-16, -16);

            var barRect = Ui.Rect(outer.transform, "HpBar");
            barRect.Fill(0, 0.5f, 1, 0.5f);
            barRect.offsetMin = new Vector2(104, -9);
            barRect.offsetMax = new Vector2(-16, 5);
            w.hpFill = Ui.Bar(barRect, Ui.HpGreen);

            w.hpLabel = Ui.Label(outer.transform, "", 13, Ui.Dim, TextAnchor.LowerLeft);
            w.hpLabel.rectTransform.offsetMin = new Vector2(104, 12);

            w.statusRow = Ui.Rect(outer.transform, "Status");
            w.statusRow.anchorMin = w.statusRow.anchorMax = new Vector2(1, 0);
            w.statusRow.pivot = new Vector2(1, 0);
            w.statusRow.anchoredPosition = new Vector2(-14, 9);
            w.statusRow.sizeDelta = new Vector2(150, 22);
            var row = w.statusRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleRight;
            row.spacing = 2;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            w.statusLabel = Ui.Label(outer.transform, "", 13, Ui.GoldHi, TextAnchor.LowerRight);
            w.statusLabel.rectTransform.offsetMin = new Vector2(104, 10);
            w.statusLabel.rectTransform.offsetMax = new Vector2(-14, 0);

            // 선택 완료 봉랍: 카드 오른쪽 위 모서리에 걸쳐 찍힌다.
            w.readySeal = Ui.Rect(w.Root, "Ready").gameObject.AddComponent<Image>();
            w.readySeal.rectTransform.At(1, 1, -4, -2, 46, 46);
            w.readySeal.raycastTarget = false;
            if (!Art.TryApply(w.readySeal, "UI/ui_ready_check", Color.white))
            {
                w.readySeal.sprite = UiSprites.Circle;
                w.readySeal.color = Ui.Hex("#3E7A50");
                Ui.Label(w.readySeal.transform, "◆", 18, Ui.Memo, TextAnchor.MiddleCenter, bold: true);
            }

            Ui.MakeButton(outer, onClick);
            return w;
        }

        /// <summary>hp는 화면에 보여줄 값. 연출 중에는 실제 HP보다 늦게 따라간다.</summary>
        public void Refresh(Arthur arthur, int hp, bool submitted, bool planning, bool isAllyTarget)
        {
            float ratio = hp / (float)arthur.MaxHp;
            Ui.SetBar(hpFill, ratio);
            hpLabel.text = $"{hp:N0} / {arthur.MaxHp:N0}";
            highlight.enabled = isAllyTarget && hp > 0;
            group.alpha = hp > 0 ? 1f : 0.55f;
            RefreshStatus(arthur);

            readySeal.gameObject.SetActive(planning && submitted && hp > 0);
            if (hp <= 0)
            {
                readyLabel.text = "쓰러짐";
                readyLabel.color = Ui.Danger;
            }
            else
            {
                readyLabel.text = planning && !submitted ? "선택 중…" : "";
                readyLabel.color = Ui.Dim;
            }
        }

        private void RefreshStatus(BattleUnit unit)
        {
            Ui.Clear(statusRow);
            var missing = new List<string>();
            foreach (var kind in unit.Statuses.Select(s => s.Kind).Distinct())
            {
                var icon = Ui.Icon(statusRow, StatusIconPath(kind), 22);
                if (icon == null)
                    missing.Add(StatusName(kind));
            }
            statusLabel.text = string.Join(" ", missing);
        }

        public static string StatusIconPath(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.DamageUp: return "Icons/icon_status_atk_up";
                case EffectKind.DefenseUp: return "Icons/icon_status_def_up";
                case EffectKind.DamageDown: return "Icons/icon_status_atk_down";
                case EffectKind.DefenseDown: return "Icons/icon_status_def_down";
                case EffectKind.Poison: return "Icons/icon_status_poison";
                case EffectKind.HealOverTime: return "Icons/icon_status_regen";
                case EffectKind.HealBoost: return "Icons/icon_skill_heal";
                default: return "";
            }
        }

        public static string StatusName(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.DamageUp: return "공격↑";
                case EffectKind.DefenseUp: return "방어↑";
                case EffectKind.DamageDown: return "공격↓";
                case EffectKind.DefenseDown: return "방어↓";
                case EffectKind.Poison: return "독";
                case EffectKind.HealOverTime: return "재생";
                case EffectKind.HealBoost: return "회복↑";
                default: return kind.ToString();
            }
        }

        public static string StatusText(BattleUnit unit)
        {
            return string.Join(" ", unit.Statuses.Select(s => s.Kind).Distinct().Select(StatusName));
        }
    }

    /// <summary>적의 본체 또는 부위 하나. 누르면 대상으로 고른다. 행동 예고는 압정에 꽂힌 꼬리표.</summary>
    public sealed class PieceWidget
    {
        public RectTransform Root;
        public RectTransform Shape;
        public Image Glow;
        public Transform Tokens;
        private Image shapeImage;
        private Image hpFill;
        private Text hpLabel, nameLabel, targetMark;

        /// <summary>HP가 0일 때 보여줄 말. 부위는 "파괴됨", 본체는 "쓰러짐".</summary>
        public string DestroyedText = "파괴됨";

        /// <summary>예고 빛 그림(fx_part_warning)을 쓰는지. 그림이면 발밑 고리, 아니면 뒤쪽 빛 상자.</summary>
        public bool GlowIsArt { get; private set; }

        public static PieceWidget Build(Transform parent, string name, Color color, string artPath, Color artTint, float w, float h, Action onClick)
        {
            var piece = new PieceWidget();
            piece.Root = Ui.Rect(parent, name);
            piece.Root.sizeDelta = new Vector2(w, h + 70);

            piece.Glow = Ui.Rect(piece.Root, "Glow").gameObject.AddComponent<Image>();
            piece.Glow.raycastTarget = false;
            if (Art.TryApply(piece.Glow, "Effects/fx_part_warning", Color.clear))
            {
                piece.GlowIsArt = true;
                piece.Glow.preserveAspect = false;
                piece.Glow.rectTransform.At(0.5f, 0, 0, h * 0.12f, w + 90, (w + 90) * 0.34f);
            }
            else
            {
                piece.Glow.sprite = UiSprites.Rounded(60);
                piece.Glow.type = Image.Type.Sliced;
                piece.Glow.color = Color.clear;
                piece.Glow.rectTransform.At(0.5f, 0, 0, h / 2, w + 36, h + 36);
            }

            piece.shapeImage = Ui.Box(piece.Root, "Shape", color, Mathf.RoundToInt(Mathf.Min(w, h) * 0.35f));
            piece.Shape = piece.shapeImage.rectTransform;
            piece.Shape.At(0.5f, 0, 0, h / 2, w, h);
            Art.TryApply(piece.shapeImage, artPath, artTint);
            Ui.MakeButton(piece.shapeImage, onClick);

            piece.nameLabel = Ui.Label(piece.Root, name, 20, Ui.Text, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            piece.nameLabel.rectTransform.At(0.5f, 0, 0, h + 52, Mathf.Max(w, 240), 28);
            piece.nameLabel.AddOutline(0.8f);
            var bar = Ui.Rect(piece.Root, "Bar");
            bar.At(0.5f, 0, 0, h + 30, Mathf.Clamp(w, 180, 260), 14);
            piece.hpFill = Ui.Bar(bar, Ui.HpGreen);
            piece.hpLabel = Ui.Label(piece.Root, "", 14, Ui.Text, TextAnchor.MiddleCenter);
            piece.hpLabel.rectTransform.At(0.5f, 0, 0, h + 12, 260, 20);
            piece.hpLabel.AddOutline(0.8f, 1);

            piece.targetMark = Ui.Label(piece.Root, "▼ 대상", 18, Ui.GoldHi, TextAnchor.MiddleCenter, bold: true);
            piece.targetMark.rectTransform.At(0.5f, 0, 0, h + 82, 200, 26);
            piece.targetMark.AddOutline(0.8f);

            var tokens = Ui.Rect(piece.Root, "Tokens");
            tokens.At(0.5f, 0, 0, h * 0.5f, 270, 10);
            var layout = tokens.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            piece.Tokens = tokens;
            return piece;
        }

        public void Refresh(int hp, int maxHp, string status, bool destroyed, bool isTarget)
        {
            Ui.SetBar(hpFill, hp / (float)maxHp);
            hpLabel.text = destroyed ? DestroyedText : $"{hp:N0} / {maxHp:N0}" + (status.Length > 0 ? $"  {status}" : "");
            targetMark.enabled = isTarget && !destroyed;
            var group = Root.GetComponent<CanvasGroup>();
            if (group == null)
                group = Root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = destroyed ? 0.35f : 1f;
        }

        /// <summary>예고 꼬리표: 압정에 꽂힌 양피지 쪽지. 전체 공격이나 강화(strong)는 봉랍 테두리.</summary>
        public void AddToken(string text, bool strong)
        {
            var tag = Ui.Rect(Tokens, "Tag");
            tag.sizeDelta = new Vector2(250, 40);
            if (strong)
            {
                var wax = Ui.Box(tag, "Wax", Ui.Wax, 6);
                wax.rectTransform.Fill(inset: -3);
                wax.raycastTarget = false;
            }
            var paper = Ui.GuildPaper(tag, "Paper", "ui_tag_paper", Ui.Paper);
            paper.rectTransform.Fill();
            paper.raycastTarget = false;
            Ui.Label(paper.transform, text, 16, strong ? Ui.Wax : Ui.PaperInk, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            Ui.Pin(tag, -112, 0, 16);
        }

        public void ClearTokens() => Ui.Clear(Tokens);
    }

    /// <summary>손패 카드에 마우스를 올리면 살짝 들려서 똑바로 선다 (슬레이 더 스파이어 손패처럼).</summary>
    public sealed class HoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Vector2 basePosition;
        private float baseRotation;
        private int baseSibling;
        private bool onTop;

        public bool KeepOnTop => onTop;
        private bool hover;
        private RectTransform rect;

        /// <summary>onTop이면 (고른 카드) 이웃 카드에 가려지지 않게 늘 맨 위에 둔다.</summary>
        public void Place(Vector2 position, float rotation, int sibling, bool keepOnTop = false)
        {
            rect = (RectTransform)transform;
            basePosition = position;
            baseRotation = rotation;
            baseSibling = sibling;
            onTop = keepOnTop;
            rect.anchoredPosition = position;
            rect.localEulerAngles = new Vector3(0, 0, rotation);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hover = true;
            transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hover = false;
            if (onTop)
                transform.SetAsLastSibling();
            else
                transform.SetSiblingIndex(baseSibling);
        }

        private void Update()
        {
            if (rect == null)
                return;
            float k = 1 - Mathf.Exp(-18 * Time.deltaTime);
            var position = hover ? basePosition + new Vector2(0, 34) : basePosition;
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, position, k);
            float rotation = Mathf.LerpAngle(rect.localEulerAngles.z, hover ? 0 : baseRotation, k);
            rect.localEulerAngles = new Vector3(0, 0, rotation);
            rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * (hover ? 1.08f : 1f), k);
        }
    }
}
