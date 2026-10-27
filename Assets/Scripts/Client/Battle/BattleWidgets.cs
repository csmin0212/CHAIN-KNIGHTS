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
                glow.rectTransform.Fill(inset: -9);
                glow.raycastTarget = false;
                glow.gameObject.AddComponent<CardPulse>();
            }

            // 황동 테두리 그림이 없으면 금색 판을 깔고 그 위에 몸통을 얹는다.
            var frameArt = Art.Get("Cards/card_frame");
            var edge = Ui.Box(root, "Edge", frameArt != null ? Color.clear : (selected ? Ui.GoldHi : Ui.Gold), 14);
            edge.rectTransform.Fill();
            edge.raycastTarget = false;

            var body = Ui.Box(root, "Body", Ui.PanelColor, 12);
            body.rectTransform.Fill(inset: 3);

            // 일러스트 창: 카드 위쪽 절반. 높은 성 카드는 빛이 한 번씩 훑고 지나간다.
            var window = Ui.Rect(body.transform, "Art");
            window.Fill(0, 1, 1, 1);
            window.offsetMin = new Vector2(12, -170);
            window.offsetMax = new Vector2(-12, -12);
            window.gameObject.AddComponent<RectMask2D>();
            BuildArt(window, def);
            if (def.Rarity >= 4)
                window.gameObject.AddComponent<CardSheen>();

            var strip = Ui.Box(body.transform, "Element", Ui.ElementColor(def.Element), 2);
            strip.rectTransform.Fill(0, 1, 1, 1);
            strip.rectTransform.offsetMin = new Vector2(12, -176);
            strip.rectTransform.offsetMax = new Vector2(-12, -170);
            strip.raycastTarget = false;

            // 별 (오른쪽 위 리본)
            var ribbon = Ui.Box(body.transform, "Stars", Ui.Ink.WithAlpha(0.65f), 6);
            ribbon.rectTransform.At(1, 1, -50, -27, 76, 24);
            ribbon.raycastTarget = false;
            Ui.Label(ribbon.transform, CardText.Stars(def.Rarity), 14, def.Rarity >= 4 ? Ui.GoldHi : Ui.Hex("#FFF8E6"), TextAnchor.MiddleCenter);

            // 속성 메달 (일러스트 오른쪽 아래)
            var medal = Ui.Circle(body.transform, "Medal", Ui.Ink.WithAlpha(0.75f));
            medal.rectTransform.At(1, 1, -34, -146, 40, 40);
            medal.raycastTarget = false;
            var ring = Ui.Rect(medal.transform, "Ring").gameObject.AddComponent<Image>();
            ring.sprite = UiSprites.Ring(6);
            ring.color = Ui.ElementColor(def.Element);
            ring.rectTransform.Fill(inset: -2);
            ring.raycastTarget = false;
            var elementIcon = Ui.Icon(medal.transform, $"Icons/icon_element_{Ui.ElementKey(def.Element)}", 28);
            if (elementIcon != null)
                elementIcon.rectTransform.At(0.5f, 0.5f, 0, 0, 28, 28);
            else
                Ui.Label(medal.transform, CardText.ElementName(def.Element), 16, Ui.Text, TextAnchor.MiddleCenter, bold: true);

            Ui.Pill(body.transform, def.NameKo, def.NameKo.Length > 9 ? 14 : 16, out var plate);
            plate.rectTransform.At(0.5f, 1, 0, -176, Width - 34, 30);
            plate.raycastTarget = false;

            var paper = Ui.PaperBox(body.transform, "Effect", 6);
            paper.rectTransform.Fill();
            paper.rectTransform.offsetMin = new Vector2(16, 46);
            paper.rectTransform.offsetMax = new Vector2(-16, -196);
            paper.raycastTarget = false;
            var effectLabel = Ui.Label(paper.transform, effect, effectSize, Ui.PaperInk, TextAnchor.MiddleCenter, bold: effectSize > 18, serifFont: true);
            effectLabel.rectTransform.Fill(inset: 6);

            // 바닥 줄은 황동 테두리 장식에 가리지 않게 안쪽으로 들인다.
            var foot = Ui.Label(body.transform, footer, 14, Ui.Dim, TextAnchor.MiddleLeft);
            foot.rectTransform.Fill(0, 0, 1, 0);
            foot.rectTransform.offsetMin = new Vector2(24, 16);
            foot.rectTransform.offsetMax = new Vector2(-20, 42);
            if (awakened)
            {
                var awake = Ui.Label(body.transform, "◆ 각성", 15, Ui.GoldHi, TextAnchor.MiddleRight, bold: true);
                awake.rectTransform.Fill(0, 0, 1, 0);
                awake.rectTransform.offsetMin = new Vector2(24, 16);
                awake.rectTransform.offsetMax = new Vector2(-24, 42);
            }

            if (frameArt != null)
            {
                // 테두리 그림은 카드와 비율이 같아서 통째로 늘려 쓴다. 성이 높을수록 밝은 황동.
                var frame = Ui.Rect(root, "Frame").gameObject.AddComponent<Image>();
                frame.sprite = frameArt;
                frame.type = Image.Type.Simple;
                frame.rectTransform.Fill();
                frame.raycastTarget = false;
                frame.color = selected ? new Color(1f, 0.95f, 0.8f)
                    : def.Rarity >= 4 ? Color.white
                    : def.Rarity >= 3 ? new Color(0.9f, 0.88f, 0.84f)
                    : new Color(0.78f, 0.76f, 0.72f);
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

        /// <summary>
        /// 카드 일러스트. 찾는 순서: card_{번호} → card_art_{직업}_{속성} → card_art_{직업}.
        /// 아무것도 없으면 속성 색 배경 + 큰 직업 문장으로 그린다. 공용(구골) 카드는 구골 그림을 쓴다.
        /// </summary>
        private static void BuildArt(RectTransform window, CardDefinition def)
        {
            string job = def.Job == Job.None ? "none" : Ui.JobKey(def.Job);
            string element = Ui.ElementKey(def.Element);
            foreach (var path in new[] { $"Cards/card_{def.Id}", $"Cards/card_art_{job}_{element}", $"Cards/card_art_{job}" })
            {
                var sprite = Art.Get(path);
                if (sprite == null)
                    continue;
                var art = Ui.Rect(window, "Illustration").gameObject.AddComponent<Image>();
                art.sprite = sprite;
                art.rectTransform.Fill();
                art.raycastTarget = false;
                return;
            }

            var color = Ui.ElementColor(def.Element);
            var back = Ui.Box(window, "Back", Color.Lerp(color, Ui.Ink, 0.55f), 2);
            back.rectTransform.Fill();
            back.raycastTarget = false;
            var light = Ui.Circle(window, "Light", Color.Lerp(color, Color.white, 0.15f).WithAlpha(0.55f));
            light.rectTransform.At(0.5f, 0.45f, 0, 0, 230, 230);
            light.raycastTarget = false;
            var watermark = Ui.Icon(window, $"Icons/icon_element_{element}", 150);
            if (watermark != null)
            {
                watermark.rectTransform.At(1, 0, -30, 40, 150, 150).Rotate(-18);
                watermark.color = Color.white.WithAlpha(0.16f);
            }

            Image figure = def.Job == Job.None
                ? Ui.Icon(window, "Enemies/enemy_golem", 128)
                : Ui.Icon(window, $"Icons/icon_job_{job}", 104);
            if (figure != null)
            {
                float size = def.Job == Job.None ? 128 : 104;
                figure.rectTransform.At(0.5f, 0.5f, 0, -4, size, size);
                if (def.Job == Job.None)
                    figure.color = Color.Lerp(Color.white, color, 0.45f);
                figure.AddShadow();
            }
            else
            {
                Ui.Label(window, CardText.JobName(def.Job), 26, Ui.Text, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            }
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

    /// <summary>적 이름과 HP를 그림의 어느 쪽에 둘지. 본체에 붙은 부위는 옆으로 빼서 본체와 겹치지 않게 한다.</summary>
    public enum LabelSide
    {
        Above,
        Left,
        Right,
    }

    /// <summary>적의 본체 또는 부위 하나. 누르면 대상으로 고른다. 행동 예고는 압정에 꽂힌 꼬리표.</summary>
    public sealed class PieceWidget
    {
        public RectTransform Root;
        /// <summary>누르는 영역이자 흔들기·튕기기 연출의 대상.</summary>
        public RectTransform Shape;
        /// <summary>그림 (맞으면 붉게 물든다). 그림이 없으면 Shape의 도형.</summary>
        public Image ArtImage;
        public Color ArtTint;
        public Image Glow;
        public Transform Tokens;
        private Image hpFill;
        private Text hpLabel, nameLabel, targetMark;
        private RectTransform bar;

        /// <summary>HP가 0일 때 보여줄 말. 부위는 "파괴됨", 본체는 "쓰러짐".</summary>
        public string DestroyedText = "파괴됨";

        /// <summary>예고 빛 그림(fx_part_warning)을 쓰는지. 그림이면 발밑 고리, 아니면 뒤쪽 빛 상자.</summary>
        public bool GlowIsArt { get; private set; }

        /// <param name="flipArt">그림을 좌우로 뒤집는다 (부위를 본체 방향에 맞출 때).</param>
        /// <param name="side">이름과 HP를 둘 자리.</param>
        /// <param name="tokensAt">예고 꼬리표 높이 (그림 높이 대비 0~1).</param>
        public static PieceWidget Build(Transform parent, string name, Color color, string artPath, Color artTint, float w, float h, Action onClick,
            bool flipArt = false, LabelSide side = LabelSide.Above, float tokensAt = 0.5f)
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
                // 땅에 선 본체는 발밑 고리, 몸에 붙은 부위는 부위 한가운데를 두르는 고리
                if (side == LabelSide.Above)
                    piece.Glow.rectTransform.At(0.5f, 0, 0, h * 0.12f, w + 90, (w + 90) * 0.34f);
                else
                    piece.Glow.rectTransform.At(0.5f, 0, 0, h / 2, w + 40, (w + 40) * 0.5f);
            }
            else
            {
                piece.Glow.sprite = UiSprites.Rounded(60);
                piece.Glow.type = Image.Type.Sliced;
                piece.Glow.color = Color.clear;
                piece.Glow.rectTransform.At(0.5f, 0, 0, h / 2, w + 36, h + 36);
            }

            var shape = Ui.Box(piece.Root, "Shape", color, Mathf.RoundToInt(Mathf.Min(w, h) * 0.35f));
            piece.Shape = shape.rectTransform;
            piece.Shape.At(0.5f, 0, 0, h / 2, w, h);
            var sprite = Art.Get(artPath);
            if (sprite != null)
            {
                // 그림은 자식에 따로 둔다: 뒤집기와 붉게 물들기가 누르는 영역(Shape)과 섞이지 않게.
                shape.color = Color.clear;
                piece.ArtImage = Ui.Rect(shape.transform, "Art").gameObject.AddComponent<Image>();
                piece.ArtImage.sprite = sprite;
                piece.ArtImage.preserveAspect = true;
                piece.ArtImage.raycastTarget = false;
                piece.ArtImage.rectTransform.Fill();
                if (flipArt)
                    piece.ArtImage.rectTransform.localScale = new Vector3(-1, 1, 1);
                piece.ArtTint = artTint;
            }
            else
            {
                piece.ArtImage = shape;
                piece.ArtTint = color;
            }
            piece.ArtImage.color = piece.ArtTint;
            Ui.MakeButton(shape, onClick);

            // 위: 그림 위 가운데. 옆: 그림 가운데 높이에서 왼쪽이나 오른쪽 바깥.
            bool beside = side != LabelSide.Above;
            float x = side == LabelSide.Left ? -(w / 2 + 100) : side == LabelSide.Right ? w / 2 + 100 : 0;
            float top = beside ? h / 2 + 30 : h + 52;
            float labelWidth = beside ? 190 : Mathf.Max(w, 240);
            piece.nameLabel = Ui.Label(piece.Root, name, beside ? 18 : 20, Ui.Text, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            piece.nameLabel.rectTransform.At(0.5f, 0, x, top, labelWidth, 28);
            piece.nameLabel.AddOutline(0.8f);
            piece.bar = Ui.Rect(piece.Root, "Bar");
            piece.bar.At(0.5f, 0, x, top - 22, beside ? 170 : Mathf.Clamp(w, 160, 260), beside ? 12 : 14);
            piece.hpFill = Ui.Bar(piece.bar, Ui.HpGreen);
            piece.hpLabel = Ui.Label(piece.Root, "", 14, Ui.Text, TextAnchor.MiddleCenter);
            piece.hpLabel.rectTransform.At(0.5f, 0, x, top - 40, 260, 20);
            piece.hpLabel.AddOutline(0.8f, 1);

            piece.targetMark = Ui.Label(piece.Root, "▼ 대상", 18, Ui.GoldHi, TextAnchor.MiddleCenter, bold: true);
            piece.targetMark.rectTransform.At(0.5f, 0, x, top + 30, 200, 26);
            piece.targetMark.AddOutline(0.8f);

            var tokens = Ui.Rect(piece.Root, "Tokens");
            tokens.At(0.5f, 0, 0, h * tokensAt, 270, 10);
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

        /// <summary>
        /// 이름, HP, 대상 표시, 예고 꼬리표를 위쪽 층으로 옮긴다.
        /// 부위 그림이 본체 뒤에 깔려도(날개) 글씨는 본체에 가리지 않는다.
        /// </summary>
        public void LiftLabels(Transform layer)
        {
            foreach (var rect in new[] { nameLabel.rectTransform, bar, hpLabel.rectTransform, targetMark.rectTransform, (RectTransform)Tokens })
                rect.SetParent(layer, true);
        }
    }

    /// <summary>높은 성 카드의 일러스트 위를 비스듬한 빛줄기가 몇 초마다 훑고 지나간다.</summary>
    public sealed class CardSheen : MonoBehaviour
    {
        private const float Period = 3.2f;
        private RectTransform beam;
        private float offset;

        private void Start()
        {
            var image = Ui.Rect(transform, "Sheen").gameObject.AddComponent<Image>();
            image.color = new Color(1f, 0.97f, 0.88f, 0.28f);
            image.raycastTarget = false;
            beam = image.rectTransform;
            beam.anchorMin = beam.anchorMax = new Vector2(0.5f, 0.5f);
            beam.sizeDelta = new Vector2(36, 420);
            beam.localEulerAngles = new Vector3(0, 0, -24);
            offset = UnityEngine.Random.value * Period;
        }

        private void Update()
        {
            float t = (Time.time + offset) % Period / 0.9f;
            beam.gameObject.SetActive(t <= 1f);
            if (t <= 1f)
                beam.anchoredPosition = new Vector2(Mathf.Lerp(-200f, 200f, t), 0);
        }
    }

    /// <summary>고른 카드 뒤의 빛이 천천히 숨 쉬듯 밝아졌다 어두워진다.</summary>
    public sealed class CardPulse : MonoBehaviour
    {
        private Image image;
        private Color baseColor;

        private void Start()
        {
            image = GetComponent<Image>();
            baseColor = image.color;
        }

        private void Update()
        {
            image.color = baseColor.WithAlpha(0.35f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.time * 4f)));
        }
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
