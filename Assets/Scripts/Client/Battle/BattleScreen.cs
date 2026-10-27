using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChainKnights.Client.UI;
using ChainKnights.Core.Battle;
using ChainKnights.Core.Cards;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChainKnights.Client.Battle
{
    /// <summary>
    /// 혼자 해 보는 전투 화면. 내 아서 1명 + AI 동료 3명.
    /// 그림 방향은 Docs/아트_컨셉.md "모험가 길드의 서류" (화면 요소별 대응표 참고).
    /// 판정은 모두 BattleSession이 하고, 이 클래스는 보여주기와 입력만 맡는다.
    /// </summary>
    public sealed partial class BattleScreen : MonoBehaviour
    {
        private const float PlanSeconds = 20f;
        private const int Me = 0;
        private const int ArthurHp = 6000; // 임시 수치. 직업 기본 능력치가 정해지면 바꾼다.
        private const int ArthurDefense = 100;
        private const float RevealWidth = 150, RevealHeight = 210;
        private static readonly Job[] Jobs = { Job.Warrior, Job.Guardian, Job.Mage, Job.Priest };
        private static readonly Key[] CardKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
        private static readonly Color Bronze = Ui.Hex("#8A5A1A");

        private enum Quest
        {
            Golems,
            Glacies,
        }

        // 출격 준비에서 고른 것
        private Job myJob = Job.Warrior;
        private Quest quest = Quest.Golems;
        private RaidDifficulty raidLevel = RaidDifficulty.Medium;

        private string RaidLevelName => raidLevel == RaidDifficulty.Extreme ? "최상급" : raidLevel == RaidDifficulty.Hard ? "상급" : "중급";

        // 층: 배경 → 전투 → 연출 → 덮개(준비, 결과)
        private RectTransform root, battleLayer, fxLayer, overlayLayer;
        private Image background;

        // 전투 화면 조각
        private Text turnLabel, timerLabel, costLabel, deckLabel, logLabel, signalHeader, confirmLabel, toastLabel;
        private Image timerFill;
        private RectTransform handArea, enemyArea, coinRow;
        private CanvasGroup handGroup, controlsGroup;
        private Button confirmButton, passButton;
        private readonly List<Button> signalButtons = new List<Button>();
        private readonly List<PartyWidget> party = new List<PartyWidget>();
        private readonly List<RectTransform> partyFloatAt = new List<RectTransform>();
        private readonly List<RectTransform> bubbles = new List<RectTransform>();
        private readonly List<Text> bubbleLabels = new List<Text>();
        private readonly List<Image> bubbleIcons = new List<Image>();
        private readonly Dictionary<(int enemy, int part), PieceWidget> pieces = new Dictionary<(int, int), PieceWidget>();
        private readonly HashSet<(int enemy, int part)> telegraphed = new HashSet<(int, int)>();
        private readonly List<string> log = new List<string>();

        // 전투 상태
        private BattleSession session;
        private bool resolving;
        private float timeLeft, planElapsed;
        private readonly List<int> picked = new List<int>();
        private TargetRef enemyTarget;
        private int allyTarget;
        private readonly float[] aiSubmitAt = new float[BattleRules.MaxArthurs];
        private readonly float[] aiSignalAt = new float[BattleRules.MaxArthurs];
        private Coroutine toastRoutine;
        private bool shaking;
        private int damageDealt;
        private RectTransform currentSlot; // 지금 효과를 처리 중인 공개 카드 (투사체 출발점)

        // 연출 중에 화면에 보이는 HP. 이벤트를 하나씩 보여주면서 실제 값까지 따라간다.
        private int[] shownArthurHp, shownEnemyHp;
        private int[][] shownPartHp;

        private void Start()
        {
            Application.targetFrameRate = 60;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Ui.Ink;
            }

            Ui.EnsureEventSystem();
            var canvas = Ui.CreateCanvas("BattleCanvas");
            root = (RectTransform)canvas.transform;

            background = Ui.Rect(root, "Background").gameObject.AddComponent<Image>();
            background.rectTransform.Fill();
            background.color = Ui.Ink;
            background.raycastTarget = false;

            battleLayer = Ui.Rect(root, "Battle").Fill();
            fxLayer = Ui.Rect(root, "Fx").Fill();
            overlayLayer = Ui.Rect(root, "Overlay").Fill();

            ShowSetup();
            StartAutoShotIfRequested();
        }

        // ───────────────────────── 출격 준비 (의뢰 게시판) ─────────────────────────

        private void ShowSetup()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            session = null;
            Ui.Clear(battleLayer);
            Ui.Clear(fxLayer);
            Ui.Clear(overlayLayer);
            ApplyBackground(Art.Get("Backgrounds/bg_title") != null ? "Backgrounds/bg_title" : null);
            Sound.Music("bgm_lobby");

            var dim = Ui.Box(overlayLayer, "Dim", Ui.Ink.WithAlpha(0.45f), 1);
            dim.rectTransform.Fill();

            var panel = Ui.Frame(overlayLayer, "Board");
            ((RectTransform)panel.transform.parent).At(0.5f, 0.5f, 0, 0, 1120, 720);

            var title = Ui.Label(panel.transform, "출격 준비", 42, Ui.GoldHi, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            title.rectTransform.At(0.5f, 1, 0, -54, 600, 60);

            SectionLabel(panel.transform, "내 등록증", -118);
            for (int i = 0; i < Jobs.Length; i++)
            {
                var job = Jobs[i];
                var card = RegistrationChoice(panel.transform, job, job == myJob, () =>
                {
                    myJob = job;
                    ShowSetup();
                });
                card.At(0.5f, 1, (i - 1.5f) * 240, -224, 220, 164);
            }

            SectionLabel(panel.transform, "의뢰", -330);
            var golems = QuestChoice(panel.transform, "노멀 퀘스트", "시작의 대지 · 구골 무리", "동", Bronze, quest == Quest.Golems, () =>
            {
                quest = Quest.Golems;
                ShowSetup();
            });
            golems.At(0.5f, 1, -240, -420, 440, 120).Rotate(1.2f);
            var raid = QuestChoice(panel.transform, $"레이드 · {RaidLevelName}", "빙결룡 글라키에스", "금", Ui.Wax, quest == Quest.Glacies, () =>
            {
                quest = Quest.Glacies;
                ShowSetup();
            });
            raid.At(0.5f, 1, 240, -420, 440, 120).Rotate(-1.2f);

            var note = Ui.Label(panel.transform, "남은 세 직업은 AI 동료 · 동료가 낸 카드는 공개 전까지 보이지 않음", 18, Ui.Dim, TextAnchor.MiddleCenter);
            note.rectTransform.At(0.5f, 0, 0, 170, 900, 30);

            var go = Ui.SealButton(panel.transform, "출격", 34, StartBattle, out _);
            ((RectTransform)go.transform).At(0.5f, 0, 0, 82, 136, 136);
        }

        private static void SectionLabel(Transform parent, string text, float y)
        {
            var label = Ui.Label(parent, text, 20, Ui.Gold, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            label.rectTransform.At(0.5f, 1, 0, y, 400, 30);
        }

        /// <summary>직업 고르기: 모험가 등록증. 고른 것은 밝은 황동 빛과 초록 봉랍.</summary>
        private static RectTransform RegistrationChoice(Transform parent, Job job, bool selected, Action onClick)
        {
            var rootRect = Ui.Rect(parent, CardText.JobName(job));
            if (selected)
            {
                var glow = Ui.Box(rootRect, "Glow", Ui.GoldHi.WithAlpha(0.6f), 18);
                glow.rectTransform.Fill(inset: -6);
                glow.raycastTarget = false;
            }
            var card = Ui.Box(rootRect, "Card", Ui.PanelColor, 12);
            card.rectTransform.Fill();
            if (!Art.TryApply(card, "UI/ui_panel", selected ? Color.white : new Color(0.75f, 0.75f, 0.75f), 2f))
                card.color = selected ? Ui.Panel2 : Ui.PanelColor;

            var edge = Ui.Box(card.transform, "PortraitEdge", Ui.GoldDark, 8);
            edge.rectTransform.At(0.5f, 1, 0, -52, 80, 80);
            edge.raycastTarget = false;
            var portrait = Ui.Box(edge.transform, "Portrait", Ui.JobColor(job), 6);
            portrait.rectTransform.Fill(inset: 2);
            portrait.raycastTarget = false;
            if (Art.TryApply(portrait, $"Arthurs/arthur_portrait_{Ui.JobKey(job)}", Color.white))
                portrait.preserveAspect = false;

            var name = Ui.Label(card.transform, CardText.JobName(job), 22, selected ? Ui.GoldHi : Ui.Text, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            name.rectTransform.At(0.5f, 0, 0, 46, 200, 28);
            var sub = Ui.Label(card.transform, selected ? "내가 맡음" : "AI 동료", 14, selected ? Ui.Text : Ui.Dim, TextAnchor.MiddleCenter);
            sub.rectTransform.At(0.5f, 0, 0, 20, 200, 20);

            if (selected)
            {
                var seal = Ui.Icon(rootRect, "UI/ui_ready_check", 48);
                if (seal != null)
                    seal.rectTransform.At(1, 1, -6, -4, 48, 48);
            }
            Ui.MakeButton(card, onClick);
            return rootRect;
        }

        /// <summary>의뢰 고르기: 압정에 꽂힌 의뢰서와 랭크 도장.</summary>
        private static RectTransform QuestChoice(Transform parent, string kind, string name, string rank, Color rankColor, bool selected, Action onClick)
        {
            var rootRect = Ui.Rect(parent, name);
            if (selected)
            {
                var glow = Ui.Box(rootRect, "Glow", Ui.GoldHi.WithAlpha(0.6f), 10);
                glow.rectTransform.Fill(inset: -7);
                glow.raycastTarget = false;
            }
            var paper = Ui.GuildPaper(rootRect, "Paper", "ui_quest_paper", Ui.Paper);
            paper.rectTransform.Fill();
            if (!selected)
                paper.color = new Color(0.82f, 0.8f, 0.76f);

            var kindLabel = Ui.Label(paper.transform, kind, 16, rankColor, TextAnchor.UpperLeft, bold: true);
            kindLabel.rectTransform.offsetMin = new Vector2(28, 0);
            kindLabel.rectTransform.offsetMax = new Vector2(-110, -24);
            var nameLabel = Ui.Label(paper.transform, name, 26, Ui.PaperInk, TextAnchor.LowerLeft, bold: true, serifFont: true);
            nameLabel.rectTransform.offsetMin = new Vector2(28, 26);
            nameLabel.rectTransform.offsetMax = new Vector2(-110, 0);

            var stamp = Ui.Stamp(paper.transform, rank, rankColor, 76, 30);
            stamp.At(1, 0.5f, -56, 0, 76, 76).Rotate(12);
            Ui.Pin(rootRect, 0, -2, 22);
            Ui.MakeButton(paper, onClick);
            return rootRect;
        }

        private void ApplyBackground(string path = null)
        {
            path = path ?? (quest == Quest.Glacies ? "Backgrounds/bg_raid_ice_cave" : "Backgrounds/bg_field_start");
            if (Art.TryApply(background, path, Color.white))
            {
                background.preserveAspect = false;
            }
            else
            {
                background.sprite = null;
                background.color = Ui.Ink;
            }
        }

        // ───────────────────────── 전투 시작 ─────────────────────────

        private void StartBattle()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            Ui.Clear(battleLayer);
            Ui.Clear(fxLayer);
            Ui.Clear(overlayLayer);
            ApplyBackground();
            Sound.Music(quest == Quest.Glacies ? "bgm_boss" : "bgm_battle");
            battleLayer.anchoredPosition = Vector2.zero;
            shaking = false;
            resolving = false;
            damageDealt = 0;
            log.Clear();
            picked.Clear();
            enemyTarget = TargetRef.None;
            allyTarget = Me;

            var jobs = new List<Job> { myJob };
            jobs.AddRange(Jobs.Where(j => j != myJob));
            var arthurs = jobs.Select((job, i) => new ArthurSetup
            {
                Name = i == Me ? $"나 · {CardText.JobName(job)}" : CardText.JobName(job),
                Job = job,
                MaxHp = ArthurHp,
                PhysicalDefense = ArthurDefense,
                MagicDefense = ArthurDefense,
                Deck = StarterDecks.For(job, maxLevel: true),
            }).ToList();

            var enemies = quest == Quest.Glacies
                ? new List<EnemySetup> { Encounters.Glacies(raidLevel) }
                : Encounters.GolemPack().ToList();

            session = new BattleSession(new BattleSetup
            {
                Arthurs = arthurs,
                Enemies = enemies,
                Random = new SeededBattleRandom(Environment.TickCount),
            });

            BuildBattleView();
            Log(quest == Quest.Glacies ? "<b>레이드 의뢰: 빙결룡 글라키에스</b>" : "<b>노멀 퀘스트: 구골 무리</b>");
            BeginTurn();
        }

        private void BuildBattleView()
        {
            party.Clear();
            partyFloatAt.Clear();
            bubbles.Clear();
            bubbleLabels.Clear();
            bubbleIcons.Clear();
            pieces.Clear();
            signalButtons.Clear();

            // 배경은 살짝 어둡게, 아래쪽 손패 자리는 더 어둡게 깔아서 글씨가 잘 보이게 한다.
            var shade = Ui.Box(battleLayer, "Shade", Ui.Ink.WithAlpha(background.sprite != null ? 0.38f : 0f), 1);
            shade.rectTransform.Fill();
            shade.raycastTarget = false;
            var floor = Ui.Box(battleLayer, "Floor", Ui.Hex("#101116").WithAlpha(0.9f), 1);
            floor.rectTransform.Fill(0, 0, 1, 0.33f);
            floor.raycastTarget = false;
            var floorLine = Ui.Box(battleLayer, "FloorLine", Ui.GoldDark, 1);
            floorLine.rectTransform.Fill(0, 0.33f, 1, 0.33f);
            floorLine.rectTransform.offsetMax = new Vector2(0, 2);
            floorLine.raycastTarget = false;

            BuildTopBar();
            BuildEnemies();
            BuildParty();
            BuildLog();
            BuildControls();

            toastLabel = Ui.Label(battleLayer, "", 22, Ui.Danger, TextAnchor.MiddleCenter, bold: true);
            toastLabel.rectTransform.At(0.5f, 0, 0, 440, 1000, 40);
            toastLabel.AddOutline(0.8f);
        }

        private void BuildTopBar()
        {
            // 의뢰서: 압정에 꽂힌 양피지 + 랭크 도장
            var plate = Ui.Rect(battleLayer, "Quest");
            plate.At(0, 1, 205, -62, 350, 88).Rotate(-1.5f);
            var paper = Ui.GuildPaper(plate, "Paper", "ui_quest_paper", Ui.Paper);
            paper.rectTransform.Fill();
            paper.raycastTarget = false;
            bool raid = quest == Quest.Glacies;
            var kind = Ui.Label(paper.transform, raid ? $"레이드 의뢰 · {RaidLevelName}" : "노멀 퀘스트", 15, raid ? Ui.Wax : Bronze, TextAnchor.UpperLeft, bold: true);
            kind.rectTransform.offsetMin = new Vector2(22, 0);
            kind.rectTransform.offsetMax = new Vector2(-80, -16);
            var name = Ui.Label(paper.transform, raid ? "빙결룡 글라키에스" : "시작의 대지 · 구골 무리", 22, Ui.PaperInk, TextAnchor.LowerLeft, bold: true, serifFont: true);
            name.rectTransform.offsetMin = new Vector2(22, 14);
            name.rectTransform.offsetMax = new Vector2(-80, 0);
            var stamp = Ui.Stamp(paper.transform, raid ? "금" : "동", raid ? Ui.Wax : Bronze, 56, 22);
            stamp.At(1, 0.5f, -42, 0, 56, 56).Rotate(12);
            Ui.Pin(plate, 0, -2, 20);

            // 턴 명판
            var turnPlate = Ui.Frame(battleLayer, "Turn");
            ((RectTransform)turnPlate.transform.parent).At(0.5f, 1, -56, -54, 220, 62);
            turnLabel = Ui.Label(turnPlate.transform, "TURN 1", 30, Ui.GoldHi, TextAnchor.MiddleCenter, bold: true, serifFont: true);

            // 황동 회중시계
            var clock = Ui.Rect(battleLayer, "Timer");
            clock.At(0.5f, 1, 96, -54, 88, 88);
            var back = Ui.Circle(clock, "Back", Ui.Panel2);
            back.rectTransform.Fill(inset: 8);
            back.raycastTarget = false;
            timerFill = Ui.Circle(clock, "Fill", Ui.Gold);
            timerFill.rectTransform.Fill(inset: 10);
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Radial360;
            timerFill.fillOrigin = (int)Image.Origin360.Top;
            timerFill.fillClockwise = false;
            timerFill.raycastTarget = false;
            var hole = Ui.Circle(clock, "Hole", Ui.PanelColor);
            hole.rectTransform.Fill(inset: 19);
            hole.raycastTarget = false;
            var ring = Ui.Icon(clock, "UI/ui_timer_ring", 88);
            if (ring != null)
                ring.rectTransform.Fill();
            timerLabel = Ui.Label(clock, "20", 26, Ui.Memo, TextAnchor.MiddleCenter, bold: true, serifFont: true);
        }

        private void BuildEnemies()
        {
            enemyArea = Ui.Rect(battleLayer, "Enemies");
            enemyArea.At(0.5f, 1, 0, -380, 1100, 520);

            var enemies = session.Enemies;
            if (enemies.Count == 1 && enemies[0].Parts.Count > 0)
            {
                // 부위가 있는 보스: 따로 그린 부위를 본체에 붙여 한 몸으로 보이게 한다.
                // 본체는 왼쪽(파티 쪽)을 본다. 날개는 등 뒤에서 위로 펼치고, 발톱은 가슴에서 앞으로 뻗는다.
                // 두 그림 다 원본이 반대 방향이라 좌우로 뒤집는다. 좌표는 본체 그림(480×360) 중심 기준.
                var boss = enemies[0];
                var body = new Vector2(30, -15);
                var wing = boss.Part(1);
                if (wing != null)
                    AddPiece(0, 1, wing.Name, Ui.Hex("#6FA8D6"), "Enemies/boss_glacies_wing", Color.white,
                        body + new Vector2(126, 133), 300, 225, flip: true, side: LabelSide.Right, tokensAt: 0.55f);
                AddPiece(0, -1, boss.Name, Ui.Hex("#3E6F98"), "Enemies/boss_glacies_body", Color.white, body, 480, 360, tokensAt: 0.72f);
                var claw = boss.Part(0);
                if (claw != null)
                    AddPiece(0, 0, claw.Name, Ui.Hex("#6FA8D6"), "Enemies/boss_glacies_claw", Color.white,
                        body + new Vector2(-132, -101), 240, 180, flip: true, side: LabelSide.Left, tokensAt: 0.2f);
                // 글씨는 모든 그림 위에
                var labels = Ui.Rect(enemyArea, "Labels").Fill();
                foreach (var piece in pieces.Values)
                    piece.LiftLabels(labels);
                return;
            }

            // 여러 마리: 가로로 늘어놓는다. 이름이 겹치면 A, B를 붙인다.
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                string name = enemy.Name;
                if (enemies.Count(e => e.Name == enemy.Name) > 1)
                    name += " " + (char)('A' + enemies.Take(i).Count(e => e.Name == enemy.Name));
                var color = Color.Lerp(Ui.ElementColor(enemy.Element), Ui.Ink, 0.3f);
                var tint = Color.Lerp(Color.white, Ui.ElementColor(enemy.Element), 0.45f);
                AddPiece(i, -1, name, color, "Enemies/enemy_golem", tint, new Vector2((i - (enemies.Count - 1) / 2f) * 320, -55), 240, 240);
            }
        }

        /// <summary>적 그림 하나를 놓는다. center는 그림(Shape)의 중심 위치 (적 구역 중심 기준).</summary>
        private void AddPiece(int enemyIndex, int partIndex, string name, Color color, string art, Color artTint, Vector2 center, float w, float h,
            bool flip = false, LabelSide side = LabelSide.Above, float tokensAt = 0.5f)
        {
            var piece = PieceWidget.Build(enemyArea, name, color, art, artTint, w, h, () => OnPieceClicked(enemyIndex, partIndex), flip, side, tokensAt);
            if (partIndex < 0)
                piece.DestroyedText = "쓰러짐";
            // 위젯 바닥에서 그림 중심까지 h/2, 위젯 높이는 h+70이라 그림 중심은 위젯 중심보다 35 아래에 있다.
            piece.Root.At(0.5f, 0.5f, center.x, center.y + 35, w, h + 70);
            pieces[(enemyIndex, partIndex)] = piece;
        }

        private void BuildParty()
        {
            for (int i = 0; i < session.Arthurs.Count; i++)
            {
                int index = i;
                var arthur = session.Arthurs[i];
                var widget = PartyWidget.Build(battleLayer, arthur, i == Me, () => OnPartyClicked(index));
                float y = -180 - i * 126;
                widget.Root.At(0, 1, 185, y, 330, 108);
                party.Add(widget);
                // 아군 위의 숫자는 카드 글자를 가리지 않게 카드 오른쪽 바깥에서 떠오른다.
                var floatAt = Ui.Rect(widget.Root, "FloatAt");
                floatAt.At(1, 0.5f, 70, -10, 10, 10);
                partyFloatAt.Add(floatAt);

                // 수신호 쪽지: 등록증 오른쪽에 붙는다.
                var note = Ui.GuildPaper(battleLayer, "Memo", "ui_memo_note", Ui.Memo, 3);
                note.rectTransform.At(0, 1, 452, y + 8, 196, 48).Rotate(i % 2 == 0 ? 2f : -2f);
                note.raycastTarget = false;
                var icon = Ui.Rect(note.transform, "Icon").gameObject.AddComponent<Image>();
                icon.rectTransform.At(0, 0.5f, 26, 0, 32, 32);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                var text = Ui.Label(note.transform, "", 17, Ui.PaperInk, TextAnchor.MiddleLeft, bold: true, serifFont: true);
                text.rectTransform.offsetMin = new Vector2(48, 0);
                text.rectTransform.offsetMax = new Vector2(-8, 0);
                note.gameObject.SetActive(false);
                bubbles.Add(note.rectTransform);
                bubbleLabels.Add(text);
                bubbleIcons.Add(icon);
            }
        }

        private void BuildLog()
        {
            // 모험 일지: 양피지에 잉크로 한 줄씩
            var paper = Ui.PaperBox(battleLayer, "Journal", 8);
            paper.rectTransform.At(1, 1, -184, -336, 326, 432);
            paper.raycastTarget = false;
            var title = Ui.Label(paper.transform, "모험 일지", 21, Ui.PaperInk, TextAnchor.UpperLeft, bold: true, serifFont: true);
            title.rectTransform.offsetMin = new Vector2(22, 0);
            title.rectTransform.offsetMax = new Vector2(-16, -16);
            var rule = Ui.Box(paper.transform, "Rule", Ui.Hex("#C9B48A"), 1);
            rule.rectTransform.Fill(0, 1, 1, 1);
            rule.rectTransform.offsetMin = new Vector2(20, -50);
            rule.rectTransform.offsetMax = new Vector2(-20, -48);
            rule.raycastTarget = false;

            var page = Ui.Rect(paper.transform, "Page");
            page.Fill();
            page.offsetMin = new Vector2(20, 18);
            page.offsetMax = new Vector2(-16, -56);
            page.gameObject.AddComponent<RectMask2D>();
            logLabel = Ui.Label(page, "", 15, Ui.PaperInk, TextAnchor.LowerLeft, serifFont: true);
            logLabel.lineSpacing = 1.1f;
        }

        private void BuildControls()
        {
            // 손패
            handArea = Ui.Rect(battleLayer, "Hand");
            handArea.At(0.5f, 0, 0, 222, 1200, 340);
            handGroup = handArea.gameObject.AddComponent<CanvasGroup>();

            var controls = Ui.Rect(battleLayer, "Controls").Fill();
            controlsGroup = controls.gameObject.AddComponent<CanvasGroup>();

            // 코스트: 황동 주화 (왼쪽 아래)
            costLabel = Ui.Label(controls, "", 24, Ui.GoldHi, TextAnchor.MiddleLeft, bold: true, serifFont: true);
            costLabel.rectTransform.At(0, 0, 200, 250, 320, 34);
            coinRow = Ui.Rect(controls, "Coins");
            coinRow.anchorMin = coinRow.anchorMax = new Vector2(0, 0);
            coinRow.pivot = new Vector2(0, 0.5f);
            coinRow.anchoredPosition = new Vector2(40, 200);
            coinRow.sizeDelta = new Vector2(380, 44);
            var row = coinRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 2;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            deckLabel = Ui.Label(controls, "", 15, Ui.Dim, TextAnchor.MiddleLeft);
            deckLabel.rectTransform.At(0, 0, 200, 150, 320, 26);

            // 신호: 수신호 쪽지 (오른쪽 아래)
            signalHeader = Ui.Label(controls, "", 16, Ui.Gold, TextAnchor.MiddleCenter, bold: true);
            signalHeader.rectTransform.At(1, 0, -250, 348, 460, 26);
            var signals = new[]
            {
                (SignalKind.Attack, "공격 갈게"), (SignalKind.NeedDefense, "방어 부탁"), (SignalKind.NeedHeal, "회복 부탁"),
                (SignalKind.Buff, "버프 걸게"), (SignalKind.FocusPart, "부위 노리자"), (SignalKind.Chain, "체인 가자"),
            };
            for (int i = 0; i < signals.Length; i++)
            {
                var kind = signals[i].Item1;
                var memo = Ui.GuildPaper(controls, signals[i].Item2, "ui_memo_note", Ui.Memo, 3);
                memo.rectTransform.At(1, 0, -400 + (i % 3) * 150, 302 - (i / 3) * 58, 142, 50);
                var icon = Ui.Icon(memo.transform, SignalIcon(kind), 30);
                if (icon != null)
                    icon.rectTransform.At(0, 0.5f, 22, 0, 30, 30);
                var label = Ui.Label(memo.transform, signals[i].Item2, 15, Ui.PaperInk, TextAnchor.MiddleLeft, bold: true, serifFont: true);
                label.rectTransform.offsetMin = new Vector2(icon != null ? 40 : 10, 0);
                signalButtons.Add(Ui.MakeButton(memo, () => SendMySignal(kind)));
            }

            confirmButton = Ui.SealButton(controls, "확정", 32, Confirm, out confirmLabel);
            ((RectTransform)confirmButton.transform).At(1, 0, -140, 118, 150, 150);
            passButton = Ui.FrameButton(controls, "패스", 16, Pass, out _);
            ((RectTransform)passButton.transform).At(1, 0, -350, 110, 150, 46);
        }

        private static string SignalIcon(SignalKind kind)
        {
            switch (kind)
            {
                case SignalKind.Attack: return "Icons/icon_signal_attack";
                case SignalKind.NeedDefense: return "Icons/icon_signal_defense";
                case SignalKind.NeedHeal: return "Icons/icon_signal_heal";
                case SignalKind.Buff: return "Icons/icon_signal_buff";
                case SignalKind.FocusPart: return "Icons/icon_signal_part";
                default: return "Icons/icon_signal_chain";
            }
        }

        // ───────────────────────── 턴 진행 ─────────────────────────

        private void BeginTurn()
        {
            session.StartTurn();
            picked.Clear();
            timeLeft = PlanSeconds;
            planElapsed = 0;
            resolving = false;

            for (int i = 0; i < session.Arthurs.Count; i++)
            {
                if (i == Me)
                    continue;
                aiSubmitAt[i] = UnityEngine.Random.Range(1.5f, 6f);
                aiSignalAt[i] = UnityEngine.Random.value < 0.7f ? UnityEngine.Random.Range(0.6f, aiSubmitAt[i] - 0.3f) : -1;
            }

            FixTargets();
            foreach (var bubble in bubbles)
                bubble.gameObject.SetActive(false);

            Log($"<color=#8A6A3A>— {session.Turn}턴 · 코스트 {session.Cost} —</color>");
            SyncShown();
            RebuildIntents();
            RebuildHand();
            RefreshView();
            StartCoroutine(FadeGroup(handGroup, 1, 0.25f));
            StartCoroutine(FadeGroup(controlsGroup, 1, 0.25f));
            handGroup.interactable = controlsGroup.interactable = true;
            StartCoroutine(Banner($"TURN {session.Turn}", Ui.Gold, 0.45f, $"코스트 {session.Cost}", small: true));
        }

        private void Update()
        {
            PulseTelegraphs();
            if (session == null || resolving || session.Phase != BattlePhase.Planning)
                return;

            float dt = Time.deltaTime;
            planElapsed += dt;
            timeLeft -= dt;

            for (int i = 0; i < session.Arthurs.Count; i++)
            {
                if (i == Me || !session.Arthurs[i].IsAlive || session.HasSubmitted(i))
                    continue;
                if (aiSignalAt[i] >= 0 && planElapsed >= aiSignalAt[i])
                {
                    aiSignalAt[i] = -1;
                    SendAiSignal(i);
                }
                if (planElapsed >= aiSubmitAt[i])
                {
                    session.Submit(i, BattleAI.ChoosePlays(session, i));
                    RefreshView();
                }
            }

            timerFill.fillAmount = Mathf.Clamp01(timeLeft / PlanSeconds);
            timerFill.color = timeLeft < 5 ? Ui.Wax : Ui.Gold;
            timerLabel.text = Mathf.CeilToInt(Mathf.Max(timeLeft, 0)).ToString();

            HandleKeys();

            bool everyoneReady = session.Arthurs.Where(a => a.IsAlive).All(a => session.HasSubmitted(a.Index));
            if (everyoneReady || timeLeft <= 0)
                Resolve();
        }

        private void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || MyTurnLocked)
                return;
            var hand = session.Arthurs[Me].Hand;
            for (int i = 0; i < CardKeys.Length && i < hand.Count; i++)
            {
                if (keyboard[CardKeys[i]].wasPressedThisFrame)
                    ToggleCard(hand[i].InstanceId);
            }
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                Confirm();
        }

        private bool MyTurnLocked => resolving || session == null || session.Phase != BattlePhase.Planning
            || !session.Arthurs[Me].IsAlive || session.HasSubmitted(Me);

        private void Resolve()
        {
            if (timeLeft <= 0)
            {
                Log("<color=#9B2F25>시간 초과</color>");
                // 시간이 끝나면 고르던 카드를 그대로 내고, 낼 수 없으면 넘긴다.
                if (session.Arthurs[Me].IsAlive && !session.HasSubmitted(Me) && !session.Submit(Me, BuildPlays()).Success)
                    session.Submit(Me, Array.Empty<CardPlay>());
                for (int i = 0; i < session.Arthurs.Count; i++)
                {
                    if (i != Me && session.Arthurs[i].IsAlive && !session.HasSubmitted(i))
                        session.Submit(i, BattleAI.ChoosePlays(session, i));
                }
            }

            resolving = true;
            handGroup.interactable = controlsGroup.interactable = false;
            timerFill.fillAmount = 0;
            timerLabel.text = "";
            SyncShown();
            var events = session.ResolveTurn();
            StartCoroutine(PlayResolution(events));
        }

        // ───────────────────────── 입력 ─────────────────────────

        private void ToggleCard(int instanceId)
        {
            if (MyTurnLocked)
                return;
            if (picked.Remove(instanceId))
            {
                RebuildHand();
                RefreshView();
                return;
            }

            var card = session.Arthurs[Me].FindInHand(instanceId);
            if (card == null)
                return;
            if (PickedCost + card.Card.Cost > session.Cost)
            {
                Toast($"코스트가 부족합니다 ({PickedCost + card.Card.Cost}/{session.Cost})");
                return;
            }
            picked.Add(instanceId);
            RebuildHand();
            RefreshView();
        }

        private int PickedCost => picked.Sum(id => session.Arthurs[Me].FindInHand(id)?.Card.Cost ?? 0);

        private List<CardPlay> BuildPlays()
        {
            var me = session.Arthurs[Me];
            var plays = new List<CardPlay>();
            foreach (int id in picked)
            {
                var card = me.FindInHand(id);
                if (card == null)
                    continue;
                var skill = card.Card.SkillFor(me.Job);
                var target = skill.Effects.Any(e => e.Target == EffectTarget.EnemySingle) ? enemyTarget
                    : skill.Effects.Any(e => e.Target == EffectTarget.AllySingle) ? TargetRef.Ally(allyTarget)
                    : TargetRef.None;
                plays.Add(new CardPlay(id, target));
            }
            return plays;
        }

        private void Confirm()
        {
            if (MyTurnLocked)
                return;
            if (picked.Count == 0)
            {
                Toast("낼 카드를 고르세요 · 넘기려면 패스");
                return;
            }
            var result = session.Submit(Me, BuildPlays());
            if (!result.Success)
            {
                Toast(result.Error);
                return;
            }
            Sound.Play("sfx_stamp", "sfx_card_play");
            StartCoroutine(Punch(confirmButton.transform, -0.12f, 0.2f));
            RefreshView();
            StartCoroutine(SendCardsAway());
        }

        /// <summary>확정한 카드가 위로 날아가며 작아지고, 나머지 손패는 어둡게 다시 깔린다.</summary>
        private IEnumerator SendCardsAway()
        {
            var flying = handArea.GetComponentsInChildren<HoverLift>().Where(l => l.KeepOnTop).ToList();
            foreach (var lift in flying)
            {
                lift.enabled = false;
                var rect = (RectTransform)lift.transform;
                var start = rect.anchoredPosition;
                var group = lift.gameObject.AddComponent<CanvasGroup>();
                StartCoroutine(Tween(0.32f, t =>
                {
                    rect.anchoredPosition = start + new Vector2(0, 260 * t);
                    rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, t);
                    group.alpha = 1 - t * t;
                }));
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitForSeconds(0.3f);
            if (!resolving)
                RebuildHand();
        }

        private void Pass()
        {
            if (MyTurnLocked)
                return;
            picked.Clear();
            session.Submit(Me, Array.Empty<CardPlay>());
            RebuildHand();
            RefreshView();
        }

        private void OnPieceClicked(int enemyIndex, int partIndex)
        {
            if (session == null || resolving)
                return;
            var enemy = session.Enemies[enemyIndex];
            if (!enemy.IsAlive)
            {
                Toast("이미 쓰러진 적입니다");
                return;
            }
            var part = enemy.Part(partIndex);
            if (part != null && part.IsDestroyed)
            {
                Toast("이미 파괴된 부위입니다");
                return;
            }
            enemyTarget = TargetRef.Enemy(enemyIndex, partIndex);
            RefreshView();
        }

        private void OnPartyClicked(int index)
        {
            if (session == null || resolving)
                return;
            if (!session.Arthurs[index].IsAlive)
            {
                Toast("쓰러진 아군은 고를 수 없습니다");
                return;
            }
            allyTarget = index;
            RefreshView();
        }

        private void FixTargets()
        {
            var enemy = enemyTarget.Side == BattleSide.Enemy && enemyTarget.Index >= 0 ? session.Enemies[enemyTarget.Index] : null;
            var part = enemy?.Part(enemyTarget.Part);
            if (enemy == null || !enemy.IsAlive || (part != null && part.IsDestroyed))
                enemyTarget = TargetRef.Enemy(session.Enemies.First(e => e.IsAlive).Index);
            if (!session.Arthurs[allyTarget].IsAlive)
                allyTarget = session.Arthurs.First(a => a.IsAlive).Index;
        }

        // ───────────────────────── 신호 ─────────────────────────

        private void SendMySignal(SignalKind kind)
        {
            if (session == null || resolving)
                return;
            Element? element = null;
            int part = -1;
            if (kind == SignalKind.Chain)
            {
                var last = picked.Count > 0 ? session.Arthurs[Me].FindInHand(picked[picked.Count - 1]) : null;
                if (last == null)
                {
                    Toast("체인할 속성의 카드를 먼저 고르세요");
                    return;
                }
                element = last.Card.Element;
            }
            if (kind == SignalKind.FocusPart)
            {
                part = enemyTarget.Part >= 0 ? enemyTarget.Part : FirstAlivePart();
                if (part < 0)
                {
                    Toast("노릴 부위가 없습니다");
                    return;
                }
            }
            Signal(Me, kind, element, part);
        }

        private void SendAiSignal(int index)
        {
            var arthur = session.Arthurs[index];
            if (arthur.Hp < arthur.MaxHp * 0.45f)
            {
                Signal(index, SignalKind.NeedHeal);
                return;
            }
            var cards = BattleAI.ChoosePlays(session, index).Select(p => arthur.FindInHand(p.InstanceId).Card).ToList();
            if (cards.Count == 0)
                return;

            float roll = UnityEngine.Random.value;
            if (roll < 0.4f)
                Signal(index, SignalKind.Chain, cards[0].Element);
            else if (cards.Any(c => c.Category == SkillCategory.Support))
                Signal(index, SignalKind.Buff);
            else if (roll < 0.7f && FirstAlivePart() >= 0)
                Signal(index, SignalKind.FocusPart, partIndex: FirstAlivePart());
            else
                Signal(index, SignalKind.Attack);
        }

        private void Signal(int index, SignalKind kind, Element? element = null, int partIndex = -1)
        {
            var result = session.SendSignal(index, kind, element, partIndex);
            if (!result.Success)
            {
                if (index == Me)
                    Toast(result.Error);
                return;
            }
            var signal = session.Signals[session.Signals.Count - 1];
            string text = SignalText(signal);
            Log($"<color=#7A6A50>{session.Arthurs[index].Name} ▸ {text}</color>");

            var bubble = bubbles[index];
            bubbleLabels[index].text = text;
            var icon = Art.Get(SignalIcon(kind));
            bubbleIcons[index].sprite = icon;
            bubbleIcons[index].enabled = icon != null;
            bubbleLabels[index].rectTransform.offsetMin = new Vector2(icon != null ? 48 : 12, 0);
            bubble.gameObject.SetActive(true);
            Sound.Play("sfx_memo", "sfx_button", 0.6f);
            StartCoroutine(Pop(bubble));
            RefreshView();
        }

        private string SignalText(BattleSignal signal)
        {
            switch (signal.Kind)
            {
                case SignalKind.Attack: return "공격 갈게";
                case SignalKind.NeedDefense: return "방어 부탁";
                case SignalKind.NeedHeal: return "회복 부탁";
                case SignalKind.Buff: return "버프 걸게";
                case SignalKind.FocusPart:
                    var part = session.Enemies.Select(e => e.Part(signal.PartIndex)).FirstOrDefault(p => p != null);
                    return $"{(part != null ? part.Name : "부위")} 노리자";
                default:
                    return $"{CardText.ElementName(signal.Element ?? Element.Fire)} 체인 가자";
            }
        }

        private int FirstAlivePart()
        {
            foreach (var enemy in session.Enemies.Where(e => e.IsAlive))
            {
                var part = enemy.Parts.FirstOrDefault(p => !p.IsDestroyed);
                if (part != null)
                    return part.Index;
            }
            return -1;
        }

        // ───────────────────────── 화면 갱신 ─────────────────────────

        private void SyncShown()
        {
            shownArthurHp = session.Arthurs.Select(a => a.Hp).ToArray();
            shownEnemyHp = session.Enemies.Select(e => e.Hp).ToArray();
            shownPartHp = session.Enemies.Select(e => e.Parts.Select(p => p.Hp).ToArray()).ToArray();
        }

        private void RefreshView()
        {
            bool planning = session.Phase == BattlePhase.Planning && !resolving;
            for (int i = 0; i < party.Count; i++)
                party[i].Refresh(session.Arthurs[i], shownArthurHp[i], session.HasSubmitted(i), planning, i == allyTarget);

            foreach (var pair in pieces)
            {
                var enemy = session.Enemies[pair.Key.enemy];
                bool isTarget = planning && enemyTarget.Index == pair.Key.enemy && enemyTarget.Part == pair.Key.part;
                if (pair.Key.part < 0)
                {
                    int hp = shownEnemyHp[pair.Key.enemy];
                    string status = PartyWidget.StatusText(enemy) + (enemy.StunTurns > 0 ? " 정지" : "");
                    pair.Value.Refresh(hp, enemy.MaxHp, status.Trim(), hp <= 0, isTarget);
                }
                else
                {
                    var part = enemy.Parts[pair.Key.part];
                    int hp = shownPartHp[pair.Key.enemy][pair.Key.part];
                    pair.Value.Refresh(hp, part.MaxHp, "", hp <= 0 || shownEnemyHp[pair.Key.enemy] <= 0, isTarget);
                }
            }

            turnLabel.text = $"TURN {session.Turn}";
            int remaining = session.Cost - PickedCost;
            costLabel.text = $"코스트 {remaining} / {session.Cost}";
            RebuildCoins(remaining, session.Cost);
            var me = session.Arthurs[Me];
            deckLabel.text = $"덱 {me.DrawPileCount} · 버림 {me.DiscardCount}";
            int signalsLeft = BattleRules.SignalsPerTurn - session.Signals.Count(s => s.ArthurIndex == Me);
            signalHeader.text = $"신호 · 남은 {signalsLeft}";

            bool locked = MyTurnLocked;
            confirmButton.interactable = !locked;
            passButton.interactable = !locked;
            foreach (var button in signalButtons)
                button.interactable = planning && me.IsAlive && signalsLeft > 0;
            confirmLabel.text = !me.IsAlive ? "쓰러짐"
                : session.HasSubmitted(Me) && planning ? "대기"
                : "확정";
        }

        /// <summary>남은 코스트만큼 밝은 주화, 고른 카드에 쓴 만큼은 흐린 주화.</summary>
        private void RebuildCoins(int remaining, int cost)
        {
            Ui.Clear(coinRow);
            float size = cost > 8 ? 34 : 42;
            for (int i = 0; i < cost; i++)
            {
                var coin = Ui.Coin(coinRow, size);
                if (i >= remaining)
                    coin.color = new Color(1, 1, 1, 0.22f);
            }
        }

        /// <summary>적이 이번 턴에 할 행동을 부위 위에 꼬리표로 꽂는다. 전체 공격과 강화는 봉랍 테두리에 부위 발밑이 빛난다.</summary>
        private void RebuildIntents()
        {
            telegraphed.Clear();
            foreach (var piece in pieces.Values)
                piece.ClearTokens();

            foreach (var enemy in session.Enemies.Where(e => e.IsAlive))
            {
                if (enemy.StunTurns > 0)
                {
                    pieces[(enemy.Index, -1)].AddToken("행동 정지", false);
                    continue;
                }
                foreach (var intent in enemy.Intents)
                {
                    var action = intent.Action;
                    var key = (enemy.Index, pieces.ContainsKey((enemy.Index, action.SourcePart)) ? action.SourcePart : -1);
                    bool strong = action.TargetsAll || action.Kind == EnemyActionKind.PowerUp;
                    pieces[key].AddToken(IntentText(action, intent.TargetArthurIndex), strong);
                    if (strong)
                        telegraphed.Add(key);
                }
            }
        }

        private string IntentText(EnemyAction action, int targetIndex)
        {
            if (action.Kind == EnemyActionKind.PowerUp)
                return $"▲ {action.Name} +{action.Power}";
            string stat = action.Stat == StatType.Magic ? "마법" : "물리";
            string target = action.TargetsAll ? "전체"
                : targetIndex == Me ? "나"
                : targetIndex >= 0 ? CardText.JobName(session.Arthurs[targetIndex].Job)
                : "?";
            return $"{stat} {action.Power:N0} → {target}";
        }

        private void PulseTelegraphs()
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.5f);
            foreach (var pair in pieces)
            {
                bool on = telegraphed.Contains(pair.Key);
                var glow = pair.Value.Glow;
                if (!on)
                    glow.color = Color.clear;
                else if (pair.Value.GlowIsArt)
                    glow.color = new Color(1f, 0.5f, 0.4f, 0.45f + 0.55f * wave);
                else
                    glow.color = Ui.Danger.WithAlpha(0.22f + 0.18f * wave);
            }
        }

        /// <summary>손패를 부채꼴로 다시 깐다. 고른 카드는 위로 올라가고 고른 순서 번호(봉랍)가 붙는다.</summary>
        private void RebuildHand()
        {
            Ui.Clear(handArea);
            var me = session.Arthurs[Me];
            var hand = me.Hand;
            bool locked = MyTurnLocked;
            int budget = session.Cost - PickedCost;
            float mid = (hand.Count - 1) / 2f;

            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i];
                int id = card.InstanceId;
                int order = picked.IndexOf(id);
                bool selected = order >= 0;
                bool usable = !locked && card.Card.Cost <= budget;
                var rect = CardWidget.Build(handArea, card, me.Job, selected, usable, () => ToggleCard(id));
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

                float offset = i - mid;
                var position = new Vector2(offset * 185, -Mathf.Abs(offset) * 12 + (selected ? 60 : 0));
                rect.gameObject.AddComponent<HoverLift>().Place(position, selected ? 0 : -offset * 4, i, selected);

                if (selected)
                {
                    var badge = Ui.Circle(rect, "Order", Ui.Wax);
                    badge.rectTransform.At(1, 1, -4, -4, 42, 42);
                    badge.raycastTarget = false;
                    var rim = Ui.Circle(badge.transform, "Rim", Ui.WaxDark);
                    rim.rectTransform.Fill(inset: -3);
                    rim.transform.SetAsFirstSibling();
                    rim.raycastTarget = false;
                    Ui.Label(badge.transform, (order + 1).ToString(), 22, Ui.Memo, TextAnchor.MiddleCenter, bold: true, serifFont: true);
                }
                var key = Ui.Label(rect, (i + 1).ToString(), 13, Ui.Dim.WithAlpha(0.7f), TextAnchor.LowerCenter);
                key.rectTransform.Fill();
                key.rectTransform.offsetMin = new Vector2(0, -22);
            }

            // 고른 카드는 이웃 카드에 가려지지 않게 맨 위로
            foreach (var lift in handArea.GetComponentsInChildren<HoverLift>())
            {
                if (lift.KeepOnTop)
                    lift.transform.SetAsLastSibling();
            }

            if (hand.Count == 0)
                Ui.Label(handArea, "손패 없음", 22, Ui.Dim, TextAnchor.MiddleCenter);
        }

        private void Log(string line)
        {
            log.Add(line);
            if (log.Count > 30)
                log.RemoveAt(0);
            if (logLabel != null)
                logLabel.text = string.Join("\n", log);
        }

        private void Toast(string message)
        {
            if (toastLabel == null)
                return;
            if (toastRoutine != null)
                StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            toastLabel.text = message;
            toastLabel.color = Ui.Danger;
            yield return new WaitForSeconds(1.4f);
            yield return Tween(0.3f, t => toastLabel.color = Ui.Danger.WithAlpha(1 - t));
            toastLabel.text = "";
        }

        // ───────────────────────── 턴 처리 연출 ─────────────────────────

        /// <summary>
        /// 1) 모두의 카드를 길드 봉투(뒷면)로 깔고 한 장씩 뒤집는다 (아군이 뭘 냈는지 여기서 처음 안다).
        /// 2) 체인이 터지면 같은 속성 카드에 "연계" 도장. 3) 카드 순서대로 효과. 4) 적 행동. 5) 독·재생.
        /// </summary>
        private IEnumerator PlayResolution(IReadOnlyList<BattleEvent> events)
        {
            StartCoroutine(FadeGroup(handGroup, 0, 0.2f));
            StartCoroutine(FadeGroup(controlsGroup, 0.45f, 0.2f));
            telegraphed.Clear();
            RefreshView();

            var played = events.Where(e => e.Type == BattleEventType.CardPlayed).ToList();
            var slots = new List<RectTransform>();
            RectTransform row = null;
            if (played.Count > 0)
            {
                row = Ui.Rect(fxLayer, "Reveal");
                row.At(0.5f, 0, 0, 200, 10, 10);
                float spacing = Mathf.Min(RevealWidth + 14, 1500f / played.Count);
                float mid = (played.Count - 1) / 2f;
                var faces = new List<(GameObject back, GameObject face)>();
                for (int i = 0; i < played.Count; i++)
                {
                    var e = played[i];
                    var slot = Ui.Rect(row, "Slot");
                    slot.At(0.5f, 0.5f, (i - mid) * spacing, 0, RevealWidth, RevealHeight);
                    var back = CardWidget.BuildBack(slot, RevealWidth, RevealHeight);
                    back.At(0.5f, 0.5f, 0, 0, RevealWidth, RevealHeight);
                    var face = CardWidget.BuildReveal(slot, CardCatalog.Get(e.CardId), session.Arthurs[e.ActorIndex].Name, e.IsAwakened, RevealWidth / CardWidget.Width);
                    face.anchorMin = face.anchorMax = new Vector2(0.5f, 0.5f);
                    face.anchoredPosition = Vector2.zero;
                    face.gameObject.SetActive(false);
                    slot.gameObject.AddComponent<CanvasGroup>().alpha = 0;
                    slots.Add(slot);
                    faces.Add((back.gameObject, face.gameObject));
                }

                // 봉투가 차례로 내려앉는다
                foreach (var slot in slots)
                {
                    StartCoroutine(DropIn(slot));
                    yield return new WaitForSeconds(0.06f);
                }
                yield return new WaitForSeconds(0.35f);

                // 한 장씩 뒤집기
                for (int i = 0; i < slots.Count; i++)
                {
                    Sound.Play("sfx_card_flip", "sfx_card_play", 0.7f);
                    yield return Flip(slots[i], faces[i].back, faces[i].face);
                    // 높은 성 카드는 뒤집을 때 빛이 터진다
                    int rarity = CardCatalog.Get(played[i].CardId).Rarity;
                    if (rarity >= 4)
                    {
                        Burst(slots[i], "Effects/fx_critical", Ui.GoldHi, rarity >= 5 ? 340 : 260);
                        Particles(slots[i], Ui.GoldHi, rarity >= 5 ? 18 : 10, 260);
                        Sound.Play("sfx_critical", volume: 0.5f);
                    }
                }
                yield return new WaitForSeconds(0.25f);

                // 체인: 같은 속성 카드마다 "연계" 도장이 쾅
                foreach (var chain in events.Where(e => e.Type == BattleEventType.Chain))
                {
                    Sound.Play("sfx_chain");
                    var linked = Enumerable.Range(0, played.Count).Where(i => played[i].Element == chain.Element).ToList();
                    for (int k = 0; k + 1 < linked.Count; k++)
                        StartCoroutine(ChainLinks(row, slots[linked[k]], slots[linked[k + 1]], Ui.ElementColor(chain.Element)));
                    foreach (int i in linked)
                    {
                        StartCoroutine(SlamStamp(slots[i], "연계", Ui.Wax, 104, 28, UnityEngine.Random.Range(-14f, 14f)));
                        yield return new WaitForSeconds(0.12f);
                    }
                    Log($"<color=#8A5A1A><b>{CardText.ElementName(chain.Element)} {chain.Chain}체인 ×{BattleRules.ChainMultiplier(chain.Chain):0.0}</b></color>");
                    yield return Banner($"{CardText.ElementName(chain.Element)}속성  {chain.Chain} CHAIN", Ui.ElementColor(chain.Element), 0.75f,
                        $"피해·회복 ×{BattleRules.ChainMultiplier(chain.Chain):0.0}", fx: "Effects/fx_chain");
                }
            }

            int current = -1;
            foreach (var e in events)
            {
                if (row != null && IsAfterPlayerPhase(e.Type))
                {
                    currentSlot = null;
                    yield return FadeOutAndDestroy(row);
                    row = null;
                }

                if (e.Type == BattleEventType.CardPlayed)
                {
                    if (current >= 0)
                        slots[current].GetComponent<CanvasGroup>().alpha = 0.4f;
                    current++;
                    var slot = slots[current];
                    currentSlot = slot;
                    slot.SetAsLastSibling();
                    var home = slot.anchoredPosition;
                    StartCoroutine(Tween(0.15f, t =>
                    {
                        slot.localScale = Vector3.one * (1 + 0.18f * t);
                        slot.anchoredPosition = home + new Vector2(0, 34 * t);
                    }));
                    Log($"<b>{e.Message}</b>");
                    yield return new WaitForSeconds(0.22f);
                    continue;
                }
                yield return ShowEvent(e);
                RefreshView();
            }
            if (row != null)
                yield return FadeOutAndDestroy(row);

            SyncShown();
            RefreshView();
            yield return new WaitForSeconds(0.3f);

            if (session.Phase == BattlePhase.Ended)
                ShowResult();
            else
                BeginTurn();
        }

        private static bool IsAfterPlayerPhase(BattleEventType type)
        {
            return type == BattleEventType.EnemyAction || type == BattleEventType.EnemyActionCanceled
                || type == BattleEventType.PoisonTick || type == BattleEventType.HealTick || type == BattleEventType.BattleEnded;
        }

        private IEnumerator ShowEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.Damage:
                    if (e.TargetSide == BattleSide.Enemy)
                    {
                        shownEnemyHp[e.TargetIndex] = Mathf.Max(0, shownEnemyHp[e.TargetIndex] - e.Value);
                        damageDealt += e.Value;
                        var piece = PieceFor(e.TargetIndex, e.PartIndex);
                        if (e.PartIndex >= 0)
                            shownPartHp[e.TargetIndex][e.PartIndex] = Mathf.Max(0, shownPartHp[e.TargetIndex][e.PartIndex] - e.Value);
                        // 투사체 → 터짐, 파편, 붉게 물들며 밀려남. 치명타는 잠깐 멈춘다.
                        var elementColor = Ui.ElementColor(e.Element);
                        if (currentSlot != null)
                            yield return Projectile(currentSlot, piece.Shape, elementColor, e.IsCritical);
                        StartCoroutine(Punch(piece.Shape, 0.07f, 0.18f));
                        StartCoroutine(HitReaction(piece, 18));
                        Burst(piece.Shape, "Effects/fx_hit", Color.Lerp(Color.white, elementColor, 0.5f), e.IsCritical ? 300 : 230);
                        Particles(piece.Shape, Color.Lerp(elementColor, Color.white, 0.3f), e.IsCritical ? 22 : 12, e.IsCritical ? 420 : 300);
                        bool magic = e.CardId > 0 && CardCatalog.Get(e.CardId).Category == SkillCategory.Magic;
                        Sound.Play(magic ? "sfx_magic" : "sfx_hit");
                        if (e.IsCritical)
                        {
                            Burst(piece.Shape, "Effects/fx_critical", Color.white, 360);
                            Sound.Play("sfx_critical");
                            StartCoroutine(Shake(10, 0.25f));
                            yield return HitStop(0.09f);
                            Float(piece.Shape, $"치명 {e.Value:N0}!", Ui.GoldHi, 46);
                        }
                        else
                        {
                            Float(piece.Shape, e.Value.ToString("N0"), Color.white, 38, outline: Color.Lerp(elementColor, Color.black, 0.45f));
                        }
                        Log(e.Message);
                        yield return new WaitForSeconds(e.IsCritical ? 0.32f : 0.2f);
                    }
                    else
                    {
                        shownArthurHp[e.TargetIndex] = Mathf.Max(0, shownArthurHp[e.TargetIndex] - e.Value);
                        StartCoroutine(Punch(party[e.TargetIndex].Root, 0.05f, 0.2f));
                        StartCoroutine(Nudge(party[e.TargetIndex].Root, new Vector2(-14, 0), 0.22f));
                        Burst(party[e.TargetIndex].Root, "Effects/fx_hit", Ui.Danger, 150);
                        Sound.Play("sfx_hit", volume: 0.7f);
                        Float(partyFloatAt[e.TargetIndex], $"-{e.Value:N0}", Ui.Danger, 34);
                        Log($"<color=#9B2F25>{e.Message}</color>");
                        yield return new WaitForSeconds(0.16f);
                    }
                    break;

                case BattleEventType.Heal:
                case BattleEventType.HealTick:
                    shownArthurHp[e.TargetIndex] = Mathf.Min(session.Arthurs[e.TargetIndex].MaxHp, shownArthurHp[e.TargetIndex] + e.Value);
                    Burst(party[e.TargetIndex].Root, "Effects/fx_heal", Color.white, 150);
                    Particles(party[e.TargetIndex].Root, Ui.Good, 8, 140, rise: true);
                    Sound.Play("sfx_heal", volume: 0.7f);
                    Float(partyFloatAt[e.TargetIndex], $"+{e.Value:N0}", Ui.Good, 32);
                    Log($"<color=#3E7A50>{e.Message}</color>");
                    yield return new WaitForSeconds(0.14f);
                    break;

                case BattleEventType.PoisonTick:
                    if (e.TargetSide == BattleSide.Enemy)
                    {
                        shownEnemyHp[e.TargetIndex] = Mathf.Max(0, shownEnemyHp[e.TargetIndex] - e.Value);
                        damageDealt += e.Value;
                        Float(PieceFor(e.TargetIndex, -1).Shape, $"독 {e.Value:N0}", Hex("#B48CE0"), 30);
                    }
                    else
                    {
                        shownArthurHp[e.TargetIndex] = Mathf.Max(0, shownArthurHp[e.TargetIndex] - e.Value);
                        Float(partyFloatAt[e.TargetIndex], $"독 {e.Value:N0}", Hex("#B48CE0"), 28);
                    }
                    Log(e.Message);
                    yield return new WaitForSeconds(0.2f);
                    break;

                case BattleEventType.StatusApplied:
                {
                    var at = e.TargetSide == BattleSide.Enemy ? PieceFor(e.TargetIndex, -1).Shape : partyFloatAt[e.TargetIndex];
                    string name = PartyWidget.StatusName(e.EffectKind);
                    string text = e.EffectKind == EffectKind.HealBoost ? name : $"{name} {e.Value}";
                    Float(at, text, e.TargetSide == BattleSide.Ally ? Ui.GoldHi : Hex("#B48CE0"), 24, 40);
                    if (e.TargetSide == BattleSide.Ally)
                        Sound.Play("sfx_buff", volume: 0.5f);
                    var target = e.TargetSide == BattleSide.Enemy ? (BattleUnit)session.Enemies[e.TargetIndex] : session.Arthurs[e.TargetIndex];
                    Log($"{target.Name}: {text}");
                    yield return new WaitForSeconds(0.1f);
                    break;
                }

                case BattleEventType.DrawQueued:
                    Float(partyFloatAt[e.TargetIndex], $"드로우 +{e.Value}", Ui.Text, 22, 40);
                    Log(e.Message);
                    yield return new WaitForSeconds(0.1f);
                    break;

                case BattleEventType.Stunned:
                    Float(PieceFor(e.TargetIndex, -1).Shape, "행동 정지!", Ui.GoldHi, 34, 50);
                    Log($"<b>{e.Message}</b>");
                    yield return new WaitForSeconds(0.3f);
                    break;

                case BattleEventType.PartDestroyed:
                {
                    // "격파" 도장 + BREAK + 번쩍 + 흔들림
                    shownPartHp[e.TargetIndex][e.PartIndex] = 0;
                    var piece = PieceFor(e.TargetIndex, e.PartIndex);
                    RefreshView();
                    Burst(piece.Shape, "Effects/fx_critical", Color.white, 420);
                    Particles(piece.Shape, Ui.Hex("#BFE3FF"), 26, 460);
                    StartCoroutine(HitReaction(piece, 30));
                    StartCoroutine(Shake(16, 0.4f));
                    yield return HitStop(0.12f);
                    Sound.Play("sfx_part_break");
                    StartCoroutine(SlamStamp(StampAnchor(piece.Shape), "격파", Ui.Wax, 150, 40, -12));
                    Float(piece.Shape, "BREAK!", Ui.GoldHi, 60, 110);
                    Log($"<color=#8A5A1A><b>{e.Message}</b></color>");
                    yield return new WaitForSeconds(0.8f);
                    break;
                }

                case BattleEventType.EnemyDefeated:
                {
                    shownEnemyHp[e.TargetIndex] = 0;
                    foreach (var pair in pieces.Where(p => p.Key.enemy == e.TargetIndex))
                    {
                        pair.Value.ClearTokens();
                        telegraphed.Remove(pair.Key);
                    }
                    RefreshView();
                    var piece = PieceFor(e.TargetIndex, -1);
                    StartCoroutine(SlamStamp(StampAnchor(piece.Shape), "토벌", Ui.Wax, 140, 38, 10));
                    Log($"<b>{e.Message}</b>");
                    yield return new WaitForSeconds(0.5f);
                    break;
                }

                case BattleEventType.ArthurDown:
                    shownArthurHp[e.TargetIndex] = 0;
                    Float(partyFloatAt[e.TargetIndex], "쓰러짐", Ui.Danger, 30, 30);
                    Log($"<color=#9B2F25><b>{e.Message}</b></color>");
                    yield return new WaitForSeconds(0.35f);
                    break;

                case BattleEventType.EnemyAction:
                {
                    // 적이 파티 쪽으로 덤벼든다. 전체 공격은 화면이 붉게 물든다.
                    var piece = PieceFor(e.ActorIndex, e.PartIndex);
                    StartCoroutine(Punch(piece.Shape, 0.12f, 0.3f));
                    StartCoroutine(Nudge(piece.Shape, new Vector2(-60, -10), 0.32f));
                    if (session.Enemies[e.ActorIndex].Intents.Any(i => i.Action.TargetsAll && e.Message.Contains(i.Action.Name)))
                        StartCoroutine(Vignette(Ui.Wax, 0.6f));
                    Log($"<color=#9B2F25>{e.Message}</color>");
                    string name = e.Message.Contains("「") ? e.Message.Substring(e.Message.IndexOf('「')) : e.Message;
                    yield return Banner(name, Ui.Wax, 0.45f, session.Enemies[e.ActorIndex].Name, small: true);
                    break;
                }

                case BattleEventType.EnemyActionCanceled:
                    Log($"<color=#8A5A1A>{e.Message}</color>");
                    yield return Banner("행동 취소!", Ui.Gold, 0.5f, e.Message, small: true);
                    break;

                case BattleEventType.BattleEnded:
                    Log($"<b>{e.Message}</b>");
                    break;
            }
        }

        private PieceWidget PieceFor(int enemyIndex, int partIndex)
        {
            return pieces.TryGetValue((enemyIndex, partIndex), out var piece) ? piece : pieces[(enemyIndex, -1)];
        }

        /// <summary>도장을 찍을 자리: 대상 위치에 둔 연출층의 빈 칸 (대상이 흐려져도 도장은 선명하게).</summary>
        private RectTransform StampAnchor(RectTransform target)
        {
            var anchor = Ui.Rect(fxLayer, "StampAt");
            anchor.sizeDelta = new Vector2(10, 10);
            anchor.position = target.position;
            return anchor;
        }

        // ───────────────────────── 결과: 보수 정산서 ─────────────────────────

        private void ShowResult()
        {
            bool victory = session.Outcome == BattleOutcome.Victory;
            resolving = false;
            Sound.Play(victory ? "sfx_victory" : "sfx_defeat");
            var dim = Ui.Box(overlayLayer, "Dim", Ui.Ink.WithAlpha(0.7f), 1);
            dim.rectTransform.Fill();

            var sheet = Ui.Rect(overlayLayer, "Settlement");
            sheet.At(0.5f, 0.5f, 0, 40, 620, 470).Rotate(-1f);
            var paper = Ui.GuildPaper(sheet, "Paper", "ui_quest_paper", Ui.Paper);
            paper.rectTransform.Fill();
            paper.raycastTarget = false;
            Ui.Pin(sheet, 0, -4, 26);

            var title = Ui.Label(paper.transform, "보수 정산서", 36, Ui.PaperInk, TextAnchor.UpperCenter, bold: true, serifFont: true);
            title.rectTransform.offsetMax = new Vector2(0, -40);
            var rule = Ui.Box(paper.transform, "Rule", Ui.Hex("#C9B48A"), 1);
            rule.rectTransform.Fill(0, 1, 1, 1);
            rule.rectTransform.offsetMin = new Vector2(48, -98);
            rule.rectTransform.offsetMax = new Vector2(-48, -96);

            int alive = session.Arthurs.Count(a => a.IsAlive);
            var lines = new[]
            {
                ("의뢰", quest == Quest.Glacies ? $"빙결룡 글라키에스 ({RaidLevelName})" : "시작의 대지 · 구골 무리"),
                ("결과", victory ? "토벌 완료" : "토벌 실패"),
                ("걸린 턴", $"{session.Turn}턴"),
                ("생존", $"{alive} / {session.Arthurs.Count}"),
                ("준 피해", damageDealt.ToString("N0")),
            };
            for (int i = 0; i < lines.Length; i++)
            {
                var key = Ui.Label(paper.transform, lines[i].Item1, 19, Ui.PaperFaint, TextAnchor.MiddleLeft, serifFont: true);
                key.rectTransform.At(0, 1, 150, -136 - i * 44, 200, 34);
                var value = Ui.Label(paper.transform, lines[i].Item2, 21, Ui.PaperInk, TextAnchor.MiddleLeft, bold: true, serifFont: true);
                value.rectTransform.At(0, 1, 360, -136 - i * 44, 320, 34);
            }

            var stampAt = Ui.Rect(paper.transform, "StampAt");
            stampAt.At(1, 0, -120, 110, 10, 10);
            StartCoroutine(SlamStamp(stampAt, victory ? "완료" : "재도전", victory ? Ui.Wax : Bronze, 170, victory ? 46 : 34, -14, 0.45f));

            var again = Ui.FrameButton(overlayLayer, "다시 하기", 22, StartBattle, out _);
            ((RectTransform)again.transform).At(0.5f, 0.5f, -140, -250, 240, 60);
            var back = Ui.FrameButton(overlayLayer, "출격 준비", 22, ShowSetup, out _);
            ((RectTransform)back.transform).At(0.5f, 0.5f, 140, -250, 240, 60);

            StartCoroutine(Pop(sheet));
        }

        // ───────────────────────── 연출 도구 ─────────────────────────

        private static Color Hex(string hex, float alpha = 1) => Ui.Hex(hex).WithAlpha(alpha);

        private static float Ease(float t) => 1 - (1 - t) * (1 - t) * (1 - t);

        private static IEnumerator Tween(float duration, Action<float> step)
        {
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                step(Ease(t / duration));
                yield return null;
            }
            step(1);
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float to, float duration)
        {
            float from = group.alpha;
            yield return Tween(duration, t => group.alpha = Mathf.Lerp(from, to, t));
        }

        private static IEnumerator Pop(RectTransform rect)
        {
            var angles = rect.localEulerAngles;
            yield return Tween(0.22f, t => rect.localScale = Vector3.one * (0.6f + 0.4f * t + 0.12f * Mathf.Sin(t * Mathf.PI)));
            rect.localEulerAngles = angles;
        }

        private static IEnumerator Punch(Transform target, float amount, float duration)
        {
            yield return Tween(duration, t => target.localScale = Vector3.one * (1 + amount * Mathf.Sin(t * Mathf.PI)));
        }

        private static IEnumerator DropIn(RectTransform slot)
        {
            var group = slot.GetComponent<CanvasGroup>();
            var end = slot.anchoredPosition;
            yield return Tween(0.22f, t =>
            {
                group.alpha = t;
                slot.anchoredPosition = end + new Vector2(0, 60 * (1 - t));
            });
        }

        /// <summary>가로로 접었다가 앞면으로 편다.</summary>
        private static IEnumerator Flip(RectTransform slot, GameObject back, GameObject face)
        {
            yield return Tween(0.1f, t => slot.localScale = new Vector3(1 - t, 1 + 0.08f * t, 1));
            back.SetActive(false);
            face.SetActive(true);
            yield return Tween(0.14f, t => slot.localScale = new Vector3(t, 1.08f - 0.08f * t, 1));
        }

        /// <summary>고무 도장이 위에서 쾅 찍힌다: 크게 → 제자리, 살짝 기울어진 채로 남는다.</summary>
        private IEnumerator SlamStamp(Transform parent, string text, Color color, float size, int fontSize, float angle, float delay = 0)
        {
            if (delay > 0)
                yield return new WaitForSeconds(delay);
            var stamp = Ui.Stamp(parent, text, color, size, fontSize);
            stamp.anchorMin = stamp.anchorMax = new Vector2(0.5f, 0.5f);
            stamp.anchoredPosition = Vector2.zero;
            stamp.Rotate(angle);
            var group = stamp.gameObject.AddComponent<CanvasGroup>();
            Sound.Play("sfx_stamp", "sfx_card_play", 0.8f);
            yield return Tween(0.14f, t =>
            {
                stamp.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, t);
                group.alpha = t;
            });
            yield return Punch(stamp, 0.06f, 0.12f);
        }

        /// <summary>이펙트 그림(fx_*)을 대상 위에서 잠깐 터뜨린다. 그림이 없으면 아무것도 안 한다.</summary>
        private void Burst(RectTransform at, string path, Color tint, float size)
        {
            if (Art.Get(path) == null)
                return;
            var image = Ui.Rect(fxLayer, "Fx").gameObject.AddComponent<Image>();
            Art.TryApply(image, path, tint);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.sizeDelta = new Vector2(size, size);
            rect.position = at.position;
            StartCoroutine(BurstRoutine(image, tint));
        }

        private static IEnumerator BurstRoutine(Image image, Color tint)
        {
            var rect = image.rectTransform;
            float spin = UnityEngine.Random.Range(-30f, 30f);
            yield return Tween(0.38f, t =>
            {
                rect.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.2f, t);
                rect.localEulerAngles = new Vector3(0, 0, spin * t);
                image.color = tint.WithAlpha(1 - t * t);
            });
            Destroy(image.gameObject);
        }

        private IEnumerator FadeOutAndDestroy(RectTransform rect)
        {
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null)
                group = rect.gameObject.AddComponent<CanvasGroup>();
            yield return Tween(0.2f, t => group.alpha = 1 - t);
            Destroy(rect.gameObject);
        }

        /// <summary>속성 색 빛구슬이 카드에서 대상까지 포물선으로 날아간다. 꼬리를 남긴다.</summary>
        private IEnumerator Projectile(RectTransform from, RectTransform to, Color color, bool big)
        {
            var orb = Ui.Circle(fxLayer, "Orb", Color.Lerp(color, Color.white, 0.25f));
            float size = big ? 46 : 34;
            orb.rectTransform.sizeDelta = new Vector2(size, size);
            orb.raycastTarget = false;
            var core = Ui.Circle(orb.transform, "Core", Color.white);
            core.rectTransform.Fill(inset: size * 0.28f);
            core.raycastTarget = false;
            Vector3 start = from.position, end = to.position;
            // 화면 배율이 달라도 같은 모양으로 휘도록, 높이는 두 점 거리에 비례
            float lift = Vector3.Distance(start, end) * 0.25f;
            float trailTimer = 0;
            yield return Tween(big ? 0.26f : 0.2f, t =>
            {
                var p = Vector3.Lerp(start, end, t) + Vector3.up * lift * Mathf.Sin(t * Mathf.PI);
                orb.rectTransform.position = p;
                trailTimer += Time.deltaTime;
                if (trailTimer > 0.012f)
                {
                    trailTimer = 0;
                    StartCoroutine(TrailDot(p, color, size * 0.7f));
                }
            });
            Destroy(orb.gameObject);
        }

        private IEnumerator TrailDot(Vector3 position, Color color, float size)
        {
            var dot = Ui.Circle(fxLayer, "Trail", color.WithAlpha(0.7f));
            dot.raycastTarget = false;
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.rectTransform.position = position;
            yield return Tween(0.22f, t =>
            {
                dot.rectTransform.localScale = Vector3.one * (1 - t);
                dot.color = color.WithAlpha(0.7f * (1 - t));
            });
            Destroy(dot.gameObject);
        }

        /// <summary>작은 파편이 사방으로 튀었다가 떨어진다. rise면 위로 천천히 피어오른다 (회복).</summary>
        private void Particles(RectTransform at, Color color, int count, float speed, bool rise = false)
        {
            for (int i = 0; i < count; i++)
                StartCoroutine(Particle(at.position, color, speed, rise));
        }

        private IEnumerator Particle(Vector3 origin, Color color, float speed, bool rise)
        {
            var bit = Ui.Rect(fxLayer, "Bit").gameObject.AddComponent<Image>();
            bit.sprite = rise ? UiSprites.Circle : UiSprites.Rounded(2);
            bit.color = color;
            bit.raycastTarget = false;
            float size = UnityEngine.Random.Range(6f, 13f);
            var rect = bit.rectTransform;
            rect.sizeDelta = new Vector2(size, rise ? size : size * 0.6f);
            rect.position = origin;
            var start = rect.anchoredPosition;
            Vector2 velocity = rise
                ? new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(60f, 140f))
                : UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.4f, 1f) * speed;
            float gravity = rise ? 0 : -600;
            float life = rise ? 0.8f : UnityEngine.Random.Range(0.35f, 0.6f);
            float spin = UnityEngine.Random.Range(-720f, 720f);
            for (float t = 0; t < life; t += Time.deltaTime)
            {
                rect.anchoredPosition = start + velocity * t + new Vector2(0, 0.5f * gravity * t * t);
                rect.localEulerAngles = new Vector3(0, 0, spin * t);
                bit.color = color.WithAlpha(1 - t / life);
                yield return null;
            }
            Destroy(bit.gameObject);
        }

        /// <summary>맞은 적: 그림이 붉게 물들었다가 돌아오고, 맞은 방향으로 살짝 밀려난다.</summary>
        private IEnumerator HitReaction(PieceWidget piece, float push)
        {
            var image = piece.ArtImage;
            var tint = piece.ArtTint;
            var hurt = new Color(1f, 0.45f, 0.4f, tint.a);
            StartCoroutine(Nudge(piece.Shape, new Vector2(push, 4), 0.24f));
            yield return Tween(0.24f, t => image.color = Color.Lerp(hurt, tint, t));
        }

        /// <summary>잠깐 그쪽으로 움직였다가 제자리로 돌아온다 (밀려남, 덤벼듦).</summary>
        private static IEnumerator Nudge(RectTransform target, Vector2 offset, float duration)
        {
            var home = target.anchoredPosition;
            yield return Tween(duration, t => target.anchoredPosition = home + offset * Mathf.Sin(t * Mathf.PI));
            target.anchoredPosition = home;
        }

        /// <summary>히트스톱: 강한 타격 순간에 시간을 거의 멈춘다.</summary>
        private static IEnumerator HitStop(float seconds)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
        }

        /// <summary>화면 전체가 한 번 물든다 (전체 공격).</summary>
        private IEnumerator Vignette(Color color, float strength)
        {
            var veil = Ui.Box(fxLayer, "Vignette", color.WithAlpha(0), 1);
            veil.rectTransform.Fill();
            veil.raycastTarget = false;
            yield return Tween(0.12f, t => veil.color = color.WithAlpha(strength * 0.5f * t));
            yield return Tween(0.4f, t => veil.color = color.WithAlpha(strength * 0.5f * (1 - t)));
            Destroy(veil.gameObject);
        }

        /// <summary>체인으로 묶인 두 카드 사이에 사슬 고리가 하나씩 걸린다.</summary>
        private IEnumerator ChainLinks(RectTransform parent, RectTransform a, RectTransform b, Color color)
        {
            var sprite = Art.Get("Icons/icon_signal_chain");
            Vector2 from = a.anchoredPosition, to = b.anchoredPosition;
            int count = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(from, to) / 34f));
            for (int i = 1; i < count; i++)
            {
                var link = Ui.Rect(parent, "Link").gameObject.AddComponent<Image>();
                link.raycastTarget = false;
                if (sprite != null)
                {
                    link.sprite = sprite;
                    link.preserveAspect = true;
                    link.color = Color.Lerp(Color.white, color, 0.35f);
                }
                else
                {
                    link.sprite = UiSprites.Ring(16);
                    link.color = Ui.GoldHi;
                }
                var rect = link.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(34, 34);
                rect.anchoredPosition = Vector2.Lerp(from, to, i / (float)count) + new Vector2(0, -RevealHeight * 0.5f + 8);
                StartCoroutine(Pop(rect));
                yield return new WaitForSeconds(0.03f);
            }
        }

        private IEnumerator Flash(RectTransform shape)
        {
            var flash = Ui.Box(shape, "Flash", Color.white, 30);
            flash.rectTransform.Fill(inset: -10);
            flash.raycastTarget = false;
            yield return Tween(0.35f, t => flash.color = Color.white.WithAlpha(0.9f * (1 - t)));
            Destroy(flash.gameObject);
        }

        private IEnumerator Shake(float strength, float duration)
        {
            if (shaking)
                yield break;
            shaking = true;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                battleLayer.anchoredPosition = UnityEngine.Random.insideUnitCircle * strength * (1 - t / duration);
                yield return null;
            }
            battleLayer.anchoredPosition = Vector2.zero;
            shaking = false;
        }

        /// <summary>피해·회복 숫자가 대상 위에서 떠오르며 사라진다.</summary>
        private void Float(RectTransform at, string text, Color color, int size, float rise = 0, Color? outline = null)
        {
            var label = Ui.Label(fxLayer, text, size, color, TextAnchor.MiddleCenter, bold: true);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420, size + 24);
            rect.position = at.position;
            rect.anchoredPosition += new Vector2(UnityEngine.Random.Range(-36f, 36f), rise + UnityEngine.Random.Range(-10f, 10f));
            var edge = label.AddOutline();
            if (outline != null)
                edge.effectColor = outline.Value;
            StartCoroutine(FloatUp(label));
        }

        private static IEnumerator FloatUp(Text label)
        {
            var rect = label.rectTransform;
            var start = rect.anchoredPosition;
            var color = label.color;
            yield return Tween(0.18f, t => rect.localScale = Vector3.one * (1.5f - 0.5f * t));
            yield return Tween(0.8f, t =>
            {
                rect.anchoredPosition = start + new Vector2(0, 70 * t);
                label.color = color.WithAlpha(t < 0.5f ? 1 : 1 - (t - 0.5f) * 2);
            });
            Destroy(label.gameObject);
        }

        /// <summary>화면을 가로지르는 띠 배너 (턴 시작, 체인, 적의 큰 기술). fx는 글자 뒤에 깔 이펙트 그림.</summary>
        private IEnumerator Banner(string text, Color stripe, float hold, string sub = null, bool small = false, string fx = null)
        {
            float height = small ? 96 : 136;
            var band = Ui.Box(fxLayer, "Banner", Ui.Ink.WithAlpha(0.88f), 1);
            band.rectTransform.At(0.5f, 0.5f, 0, 70, 2400, height);
            band.raycastTarget = false;
            var top = Ui.Box(band.transform, "Top", stripe, 1);
            top.rectTransform.Fill(0, 1, 1, 1);
            top.rectTransform.offsetMin = new Vector2(0, -4);
            var bottom = Ui.Box(band.transform, "Bottom", stripe, 1);
            bottom.rectTransform.Fill(0, 0, 1, 0);
            bottom.rectTransform.offsetMax = new Vector2(0, 4);
            top.raycastTarget = bottom.raycastTarget = false;

            if (fx != null)
            {
                var glow = Ui.Icon(band.transform, fx, height * 2.2f);
                if (glow != null)
                {
                    glow.rectTransform.At(0.5f, 0.5f, 0, 0, height * 2.2f, height * 2.2f);
                    glow.color = Color.white.WithAlpha(0.8f);
                }
            }

            var label = Ui.Label(band.transform, text, small ? 40 : 58, Ui.GoldHi, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            label.rectTransform.Fill(0, sub != null ? 0.32f : 0, 1, 1);
            label.AddOutline(0.7f);
            if (sub != null)
            {
                var subLabel = Ui.Label(band.transform, sub, small ? 17 : 22, Ui.Text, TextAnchor.MiddleCenter);
                subLabel.rectTransform.Fill(0, 0.04f, 1, 0.36f);
            }

            var group = band.gameObject.AddComponent<CanvasGroup>();
            var rect = band.rectTransform;
            yield return Tween(0.14f, t =>
            {
                rect.localScale = new Vector3(1, t, 1);
                label.rectTransform.anchoredPosition = new Vector2(-120 * (1 - t), 0);
            });
            yield return new WaitForSeconds(hold);
            yield return Tween(0.18f, t => group.alpha = 1 - t);
            Destroy(band.gameObject);
        }
    }
}
