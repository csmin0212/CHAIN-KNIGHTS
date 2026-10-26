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
    /// <summary>손패 카드 한 장. 갈바테인의 "모험가 자격증" 카드 형식을 따른다.</summary>
    public static class CardWidget
    {
        public const float Width = 230;
        public const float Height = 322;

        public static RectTransform Build(Transform parent, CardInstance card, Job arthurJob, bool selected, bool usable, Action onClick)
        {
            var def = card.Card;
            bool awakened = def.IsAwakenedFor(arthurJob);
            var element = Ui.ElementColor(def.Element);

            var outer = Ui.Box(parent, def.NameKo, selected ? Ui.GoldHi : Ui.Gold, 14);
            var root = outer.rectTransform;
            root.sizeDelta = new Vector2(Width, Height);
            var body = Ui.Box(outer.transform, "Body", Ui.PanelColor, 12);
            body.rectTransform.Fill(inset: selected ? 4 : 2);
            body.raycastTarget = false;
            Art.TryApply(body, "Cards/card_frame", Color.white);

            // 머리띠: 속성 색, 코스트 보석, 별
            var band = Ui.Box(body.transform, "Band", element, 12);
            band.rectTransform.Fill(0, 1, 1, 1);
            band.rectTransform.offsetMin = new Vector2(0, -56);
            band.raycastTarget = false;

            var gem = Ui.Circle(band.transform, "Cost", Ui.PanelColor);
            gem.rectTransform.At(0, 0.5f, 34, 0, 44, 44);
            var gemRing = Ui.Circle(gem.transform, "Ring", Ui.Gold);
            gemRing.rectTransform.Fill(inset: -2);
            gemRing.transform.SetAsFirstSibling();
            Ui.Label(gem.transform, def.Cost.ToString(), 24, Ui.Text, TextAnchor.MiddleCenter, bold: true);

            var stars = Ui.Label(band.transform, CardText.Stars(def.Rarity), 16, Ui.Hex("#FFF8E6"), TextAnchor.MiddleLeft);
            stars.rectTransform.Fill(0, 0, 1, 1);
            stars.rectTransform.offsetMin = new Vector2(66, 0);

            // 금색 이름표
            Ui.Pill(body.transform, def.NameKo, def.NameKo.Length > 9 ? 14 : 16, out var pill);
            pill.rectTransform.At(0.5f, 1, 0, -64, Width - 30, 30);
            pill.raycastTarget = false;

            // 일러스트 자리
            var art = Ui.Box(body.transform, "Art", Ui.Panel2, 10);
            art.rectTransform.At(0.5f, 1, 0, -130, Width - 30, 86);
            art.raycastTarget = false;
            if (!Art.TryApply(art, $"Cards/card_{def.Id}", Color.white))
            {
                var jobMark = Ui.Label(art.transform, CardText.JobName(def.Job), 20, Ui.JobColor(def.Job), TextAnchor.MiddleCenter, bold: true);
                jobMark.color = Ui.JobColor(def.Job).WithAlpha(0.8f);
            }

            // 효과 (양피지)
            var paper = Ui.PaperBox(body.transform, "Effect");
            paper.rectTransform.Fill(0, 0, 1, 1);
            paper.rectTransform.offsetMin = new Vector2(12, 40);
            paper.rectTransform.offsetMax = new Vector2(-12, -180);
            paper.raycastTarget = false;
            var skill = def.SkillFor(arthurJob);
            var lines = CardText.DescribeSkill(skill, card.Level, def.MaxLevel).ToList();
            var effect = Ui.Label(paper.transform, string.Join("\n", lines), lines.Count > 2 ? 13 : 15, Ui.PaperInk, TextAnchor.MiddleCenter, serifFont: true);
            effect.rectTransform.Fill(inset: 6);

            // 바닥: 직업, 각성
            var job = Ui.Label(body.transform, $"{CardText.JobName(def.Job)} · Lv {card.Level}", 14, Ui.Dim, TextAnchor.MiddleLeft);
            job.rectTransform.Fill(0, 0, 1, 0);
            job.rectTransform.offsetMin = new Vector2(16, 8);
            job.rectTransform.offsetMax = new Vector2(-12, 34);
            if (awakened)
            {
                var awake = Ui.Label(body.transform, "◆ 각성", 15, Ui.GoldHi, TextAnchor.MiddleRight, bold: true);
                awake.rectTransform.Fill(0, 0, 1, 0);
                awake.rectTransform.offsetMin = new Vector2(16, 8);
                awake.rectTransform.offsetMax = new Vector2(-14, 34);
            }

            Ui.MakeButton(outer, onClick);
            // 낼 수 없는 카드는 반투명 대신 어둡게 덮는다. 겹친 카드가 비쳐 보이지 않게.
            if (!usable && !selected)
            {
                var shade = Ui.Box(outer.transform, "Shade", Ui.Ink.WithAlpha(0.55f), 14);
                shade.rectTransform.Fill();
                shade.raycastTarget = false;
            }
            return root;
        }

        /// <summary>뒷면 (공개 연출에서 뒤집기 전).</summary>
        public static RectTransform BuildBack(Transform parent, float width, float height)
        {
            var outer = Ui.Box(parent, "CardBack", Ui.Gold, 12);
            outer.rectTransform.sizeDelta = new Vector2(width, height);
            var inner = Ui.Box(outer.transform, "Inner", Ui.Ink, 10);
            inner.rectTransform.Fill(inset: 3);
            if (!Art.TryApply(inner, "Cards/card_back", Color.white))
            {
                var mark = Ui.Label(inner.transform, "◆", (int)(width * 0.3f), Ui.Gold, TextAnchor.MiddleCenter);
                mark.color = Ui.Gold.WithAlpha(0.6f);
            }
            return outer.rectTransform;
        }
    }

    /// <summary>파티 칸의 아서 한 명 (자격증 카드 형식).</summary>
    public sealed class PartyWidget
    {
        public RectTransform Root;
        private Image border;
        private Text nameLabel, hpLabel, readyLabel, statusLabel;
        private Image hpFill;

        public static PartyWidget Build(Transform parent, Arthur arthur, bool isPlayer, Action onClick)
        {
            var w = new PartyWidget();
            var outer = Ui.Box(parent, arthur.Name, Ui.Gold, 14);
            w.border = outer;
            w.Root = outer.rectTransform;
            var inner = Ui.Box(outer.transform, "Inner", Ui.PanelColor, 12);
            inner.rectTransform.Fill(inset: 2);
            inner.raycastTarget = false;

            var portrait = Ui.Box(inner.transform, "Portrait", Ui.JobColor(arthur.Job), 10);
            portrait.rectTransform.At(0, 0.5f, 46, 0, 68, 68);
            portrait.raycastTarget = false;
            if (!Art.TryApply(portrait, $"Arthurs/arthur_portrait_{arthur.Job.ToString().ToLowerInvariant()}", Color.white))
                Ui.Label(portrait.transform, CardText.JobName(arthur.Job).Substring(0, 1), 30, Ui.Ink, TextAnchor.MiddleCenter, bold: true);

            w.nameLabel = Ui.Label(inner.transform, arthur.Name, 19, isPlayer ? Ui.GoldHi : Ui.Text, TextAnchor.UpperLeft, bold: true);
            w.nameLabel.rectTransform.Fill();
            w.nameLabel.rectTransform.offsetMin = new Vector2(92, 0);
            w.nameLabel.rectTransform.offsetMax = new Vector2(-10, -10);

            w.readyLabel = Ui.Label(inner.transform, "", 14, Ui.Dim, TextAnchor.UpperRight, bold: true);
            w.readyLabel.rectTransform.Fill();
            w.readyLabel.rectTransform.offsetMin = new Vector2(92, 0);
            w.readyLabel.rectTransform.offsetMax = new Vector2(-12, -12);

            var barRect = Ui.Rect(inner.transform, "HpBar");
            barRect.Fill(0, 0.5f, 1, 0.5f);
            barRect.offsetMin = new Vector2(92, -8);
            barRect.offsetMax = new Vector2(-14, 6);
            w.hpFill = Ui.Bar(barRect, Ui.HpGreen);
            w.hpFill.transform.parent.GetComponent<RectTransform>().Fill();

            w.hpLabel = Ui.Label(inner.transform, "", 13, Ui.Dim, TextAnchor.LowerLeft);
            w.hpLabel.rectTransform.Fill();
            w.hpLabel.rectTransform.offsetMin = new Vector2(92, 10);
            w.statusLabel = Ui.Label(inner.transform, "", 13, Ui.GoldHi, TextAnchor.LowerRight);
            w.statusLabel.rectTransform.Fill();
            w.statusLabel.rectTransform.offsetMin = new Vector2(92, 10);
            w.statusLabel.rectTransform.offsetMax = new Vector2(-12, 0);

            Ui.MakeButton(outer, onClick);
            return w;
        }

        /// <summary>hp는 화면에 보여줄 값. 연출 중에는 실제 HP보다 늦게 따라간다.</summary>
        public void Refresh(Arthur arthur, int hp, bool submitted, bool planning, bool isAllyTarget)
        {
            float ratio = hp / (float)arthur.MaxHp;
            Ui.SetBar(hpFill, ratio);
            hpLabel.text = $"{hp:N0} / {arthur.MaxHp:N0}";
            statusLabel.text = StatusText(arthur);
            border.color = isAllyTarget ? Ui.GoldHi : Ui.Gold;

            if (hp <= 0)
            {
                readyLabel.text = "쓰러짐";
                readyLabel.color = Ui.Danger;
            }
            else if (!planning)
            {
                readyLabel.text = "";
            }
            else
            {
                readyLabel.text = submitted ? "◆ 선택 완료" : "선택 중…";
                readyLabel.color = submitted ? Ui.Good : Ui.Dim;
            }
        }

        public static string StatusText(BattleUnit unit)
        {
            var parts = new List<string>();
            foreach (var group in unit.Statuses.GroupBy(s => s.Kind))
            {
                switch (group.Key)
                {
                    case EffectKind.DamageUp: parts.Add("공↑"); break;
                    case EffectKind.DefenseUp: parts.Add("방↑"); break;
                    case EffectKind.DamageDown: parts.Add("공↓"); break;
                    case EffectKind.DefenseDown: parts.Add("방↓"); break;
                    case EffectKind.Poison: parts.Add("독"); break;
                    case EffectKind.HealOverTime: parts.Add("재생"); break;
                    case EffectKind.HealBoost: parts.Add("회복↑"); break;
                }
            }
            return string.Join(" ", parts);
        }
    }

    /// <summary>적의 본체 또는 부위 하나. 누르면 대상으로 고른다.</summary>
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

        public static PieceWidget Build(Transform parent, string name, Color color, string artPath, Color artTint, float w, float h, Action onClick)
        {
            var piece = new PieceWidget();
            piece.Root = Ui.Rect(parent, name);
            piece.Root.sizeDelta = new Vector2(w, h + 70);

            piece.Glow = Ui.Box(piece.Root, "Glow", Ui.GoldHi.WithAlpha(0.0f), 60);
            piece.Glow.rectTransform.At(0.5f, 0, 0, h / 2, w + 36, h + 36);
            piece.Glow.raycastTarget = false;

            piece.shapeImage = Ui.Box(piece.Root, "Shape", color, Mathf.RoundToInt(Mathf.Min(w, h) * 0.35f));
            piece.Shape = piece.shapeImage.rectTransform;
            piece.Shape.At(0.5f, 0, 0, h / 2, w, h);
            Art.TryApply(piece.shapeImage, artPath, artTint);
            Ui.MakeButton(piece.shapeImage, onClick);

            piece.nameLabel = Ui.Label(piece.Root, name, 18, Ui.Text, TextAnchor.MiddleCenter, bold: true);
            piece.nameLabel.rectTransform.At(0.5f, 0, 0, h + 52, Mathf.Max(w, 220), 26);
            var bar = Ui.Rect(piece.Root, "Bar");
            bar.At(0.5f, 0, 0, h + 30, Mathf.Clamp(w, 180, 260), 12);
            piece.hpFill = Ui.Bar(bar, Ui.HpGreen);
            piece.hpFill.transform.parent.GetComponent<RectTransform>().Fill();
            piece.hpLabel = Ui.Label(piece.Root, "", 14, Ui.Dim, TextAnchor.MiddleCenter);
            piece.hpLabel.rectTransform.At(0.5f, 0, 0, h + 12, 260, 20);

            piece.targetMark = Ui.Label(piece.Root, "▼ 대상", 18, Ui.GoldHi, TextAnchor.MiddleCenter, bold: true);
            piece.targetMark.rectTransform.At(0.5f, 0, 0, h + 80, 200, 26);

            var tokens = Ui.Rect(piece.Root, "Tokens");
            tokens.At(0.5f, 0, 0, h * 0.55f, 260, 10);
            var layout = tokens.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6;
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

        /// <summary>예고 토큰 (슬레이 더 스파이어의 의도 아이콘처럼 공격 수치와 대상을 보여준다).</summary>
        public void AddToken(string text, bool strong)
        {
            var inner = Ui.Frame(Tokens, "Token", border: strong ? Ui.GoldHi : Ui.Gold, radius: 10);
            var rect = inner.transform.parent.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(230, 40);
            Ui.Label(inner.transform, text, 17, strong ? Ui.GoldHi : Ui.Text, TextAnchor.MiddleCenter, bold: true);
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
