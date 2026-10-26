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
    /// 혼자 해 보는 전투 화면. 내 아서 1명 + AI 동료 3명. 화면 구성은 Docs/UI_목업_전투.html.
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

        private enum Quest
        {
            Golems,
            Glacies,
        }

        // 출격 준비에서 고른 것
        private Job myJob = Job.Warrior;
        private Quest quest = Quest.Golems;

        // 층: 배경 → 전투 → 연출 → 덮개(준비, 결과)
        private RectTransform root, battleLayer, fxLayer, overlayLayer;
        private Image background;

        // 전투 화면 조각
        private Text turnLabel, timerLabel, costLabel, deckLabel, logLabel, signalHeader, confirmLabel, toastLabel;
        private Image timerFill;
        private RectTransform handArea, enemyArea;
        private CanvasGroup handGroup, controlsGroup;
        private Button confirmButton, passButton;
        private readonly List<Button> signalButtons = new List<Button>();
        private readonly List<PartyWidget> party = new List<PartyWidget>();
        private readonly List<RectTransform> partyFloatAt = new List<RectTransform>();
        private readonly List<RectTransform> bubbles = new List<RectTransform>();
        private readonly List<Text> bubbleLabels = new List<Text>();
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

        // ───────────────────────── 출격 준비 ─────────────────────────

        private void ShowSetup()
        {
            StopAllCoroutines();
            session = null;
            Ui.Clear(battleLayer);
            Ui.Clear(fxLayer);
            Ui.Clear(overlayLayer);
            ApplyBackground();

            var dim = Ui.Box(overlayLayer, "Dim", Ui.Ink.WithAlpha(0.6f), 1);
            dim.rectTransform.Fill();

            var panel = Ui.Frame(overlayLayer, "Setup", radius: 18);
            ((RectTransform)panel.transform.parent).At(0.5f, 0.5f, 0, 0, 1000, 640);

            var title = Ui.Label(panel.transform, "출격 준비", 40, Ui.GoldHi, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            title.rectTransform.At(0.5f, 1, 0, -56, 600, 60);

            SectionLabel(panel.transform, "내 직업", -128);
            for (int i = 0; i < Jobs.Length; i++)
            {
                var job = Jobs[i];
                var choice = ChoiceButton(panel.transform, CardText.JobName(job), job == myJob ? "내가 맡음" : "AI 동료", job == myJob, Ui.JobColor(job), () =>
                {
                    myJob = job;
                    ShowSetup();
                });
                choice.At(0.5f, 1, (i - 1.5f) * 220, -210, 200, 110);
            }

            SectionLabel(panel.transform, "의뢰", -300);
            var golems = ChoiceButton(panel.transform, "노멀 퀘스트", "시작의 대지 · 구골 무리", quest == Quest.Golems, Ui.ElementColor(Element.Fire), () =>
            {
                quest = Quest.Golems;
                ShowSetup();
            });
            golems.At(0.5f, 1, -225, -380, 430, 100);
            var raid = ChoiceButton(panel.transform, "레이드 · 중급", "빙결룡 글라키에스", quest == Quest.Glacies, Ui.ElementColor(Element.Ice), () =>
            {
                quest = Quest.Glacies;
                ShowSetup();
            });
            raid.At(0.5f, 1, 225, -380, 430, 100);

            var note = Ui.Label(panel.transform, "남은 세 직업은 AI 동료 · 동료가 낸 카드는 공개 전까지 보이지 않음", 18, Ui.Dim, TextAnchor.MiddleCenter);
            note.rectTransform.At(0.5f, 0, 0, 150, 900, 30);

            var go = Ui.GoldButton(panel.transform, "출  격", 30, StartBattle, out _);
            ((RectTransform)go.transform).At(0.5f, 0, 0, 72, 280, 72);
        }

        private static void SectionLabel(Transform parent, string text, float y)
        {
            var label = Ui.Label(parent, text, 20, Ui.Gold, TextAnchor.MiddleCenter, bold: true);
            label.rectTransform.At(0.5f, 1, 0, y, 400, 30);
        }

        /// <summary>출격 준비의 선택 칸. 고른 칸은 밝은 금 테두리와 색 띠가 붙는다.</summary>
        private static RectTransform ChoiceButton(Transform parent, string title, string sub, bool selected, Color accent, Action onClick)
        {
            var outer = Ui.Box(parent, title, selected ? Ui.GoldHi : Ui.GoldDark.WithAlpha(0.6f), 14);
            var inner = Ui.Box(outer.transform, "Inner", selected ? Ui.Panel2 : Ui.PanelColor, 12);
            inner.rectTransform.Fill(inset: selected ? 3 : 2);
            inner.raycastTarget = false;

            var stripe = Ui.Box(inner.transform, "Accent", selected ? accent : accent.WithAlpha(0.35f), 4);
            stripe.rectTransform.Fill(0, 0, 0, 1);
            stripe.rectTransform.offsetMin = new Vector2(10, 14);
            stripe.rectTransform.offsetMax = new Vector2(16, -14);
            stripe.raycastTarget = false;

            var titleLabel = Ui.Label(inner.transform, title, 24, selected ? Ui.GoldHi : Ui.Text, TextAnchor.MiddleCenter, bold: true);
            titleLabel.rectTransform.Fill(0, 0.45f, 1, 1);
            var subLabel = Ui.Label(inner.transform, sub, 16, selected ? Ui.Text : Ui.Dim, TextAnchor.MiddleCenter);
            subLabel.rectTransform.Fill(0, 0.08f, 1, 0.5f);

            Ui.MakeButton(outer, onClick);
            return outer.rectTransform;
        }

        private void ApplyBackground()
        {
            string path = quest == Quest.Glacies ? "Backgrounds/bg_raid_ice_cave" : "Backgrounds/bg_field_start";
            if (!Art.TryApply(background, path, Color.white))
            {
                background.sprite = null;
                background.color = Ui.Ink;
            }
        }

        // ───────────────────────── 전투 시작 ─────────────────────────

        private void StartBattle()
        {
            StopAllCoroutines();
            Ui.Clear(battleLayer);
            Ui.Clear(fxLayer);
            Ui.Clear(overlayLayer);
            ApplyBackground();
            battleLayer.anchoredPosition = Vector2.zero;
            shaking = false;
            resolving = false;
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
                ? new List<EnemySetup> { Encounters.Glacies(RaidDifficulty.Medium) }
                : Encounters.GolemPack().ToList();

            session = new BattleSession(new BattleSetup
            {
                Arthurs = arthurs,
                Enemies = enemies,
                Random = new SeededBattleRandom(Environment.TickCount),
            });

            BuildBattleView();
            Log(quest == Quest.Glacies ? "<b>레이드: 빙결룡 글라키에스</b>" : "<b>노멀 퀘스트: 구골 무리</b>");
            BeginTurn();
        }

        private void BuildBattleView()
        {
            party.Clear();
            partyFloatAt.Clear();
            bubbles.Clear();
            bubbleLabels.Clear();
            pieces.Clear();
            signalButtons.Clear();

            // 무대 조명: 적 뒤쪽은 밝게, 바닥은 어둡게
            if (background.sprite == null)
            {
                var light = Ui.Circle(battleLayer, "StageLight", Ui.Panel2.WithAlpha(0.55f));
                light.rectTransform.At(0.5f, 1, 0, -380, 1500, 820);
                light.raycastTarget = false;
                var floor = Ui.Box(battleLayer, "Floor", Hex("#101116", 0.75f), 1);
                floor.rectTransform.Fill(0, 0, 1, 0.34f);
                floor.raycastTarget = false;
                var floorLine = Ui.Box(battleLayer, "FloorLine", Ui.Gold.WithAlpha(0.18f), 1);
                floorLine.rectTransform.Fill(0, 0.34f, 1, 0.34f);
                floorLine.rectTransform.offsetMax = new Vector2(0, 2);
                floorLine.raycastTarget = false;
            }

            BuildTopBar();
            BuildEnemies();
            BuildParty();
            BuildLog();
            BuildControls();

            toastLabel = Ui.Label(battleLayer, "", 22, Ui.Danger, TextAnchor.MiddleCenter, bold: true);
            toastLabel.rectTransform.At(0.5f, 0, 0, 420, 1000, 40);
            toastLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        }

        private void BuildTopBar()
        {
            var plate = Ui.Frame(battleLayer, "Quest", radius: 12);
            ((RectTransform)plate.transform.parent).At(0, 1, 200, -52, 360, 76);
            var kind = Ui.Label(plate.transform, quest == Quest.Glacies ? "레이드 · 중급" : "노멀 퀘스트", 15, Ui.Gold, TextAnchor.UpperLeft, bold: true);
            kind.rectTransform.Fill();
            kind.rectTransform.offsetMin = new Vector2(18, 0);
            kind.rectTransform.offsetMax = new Vector2(-12, -10);
            var name = Ui.Label(plate.transform, quest == Quest.Glacies ? "빙결룡 글라키에스" : "시작의 대지 · 구골 무리", 22, Ui.Text, TextAnchor.LowerLeft, bold: true, serifFont: true);
            name.rectTransform.Fill();
            name.rectTransform.offsetMin = new Vector2(18, 10);

            turnLabel = Ui.Pill(battleLayer, "TURN 1", 26, out var turnPill);
            turnPill.rectTransform.At(0.5f, 1, -50, -50, 210, 52);

            var ring = Ui.Circle(battleLayer, "Timer", Ui.Panel2);
            ring.rectTransform.At(0.5f, 1, 100, -50, 76, 76);
            ring.raycastTarget = false;
            timerFill = Ui.Circle(ring.transform, "Fill", Ui.Gold);
            timerFill.rectTransform.Fill();
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Radial360;
            timerFill.fillOrigin = (int)Image.Origin360.Top;
            timerFill.fillClockwise = false;
            timerFill.raycastTarget = false;
            var hole = Ui.Circle(ring.transform, "Hole", Ui.PanelColor);
            hole.rectTransform.Fill(inset: 7);
            hole.raycastTarget = false;
            timerLabel = Ui.Label(hole.transform, "20", 26, Ui.Text, TextAnchor.MiddleCenter, bold: true);
        }

        private void BuildEnemies()
        {
            enemyArea = Ui.Rect(battleLayer, "Enemies");
            enemyArea.At(0.5f, 1, 0, -370, 1100, 520);

            var enemies = session.Enemies;
            if (enemies.Count == 1 && enemies[0].Parts.Count > 0)
            {
                // 부위가 있는 보스: 본체 가운데, 발톱은 오른쪽 아래, 날개는 왼쪽 위
                var boss = enemies[0];
                AddPiece(0, -1, boss.Name, Hex("#3E6F98"), "Enemies/boss_glacies_body", Color.white, 0, 0, 400, 300);
                var spots = new[] { new Vector2(340, -120), new Vector2(-340, 50) };
                var sizes = new[] { new Vector2(200, 150), new Vector2(220, 160) };
                var arts = new[] { "Enemies/boss_glacies_claw", "Enemies/boss_glacies_wing" };
                foreach (var part in boss.Parts)
                {
                    int i = Mathf.Min(part.Index, spots.Length - 1);
                    AddPiece(0, part.Index, part.Name, Hex("#6FA8D6"), arts[i], Color.white, spots[i].x, spots[i].y, sizes[i].x, sizes[i].y);
                }
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
                AddPiece(i, -1, name, color, "Enemies/enemy_golem", Ui.ElementColor(enemy.Element), (i - (enemies.Count - 1) / 2f) * 320, -20, 230, 210);
            }
        }

        private void AddPiece(int enemyIndex, int partIndex, string name, Color color, string art, Color artTint, float x, float y, float w, float h)
        {
            var piece = PieceWidget.Build(enemyArea, name, color, art, artTint, w, h, () => OnPieceClicked(enemyIndex, partIndex));
            if (partIndex < 0)
                piece.DestroyedText = "쓰러짐";
            piece.Root.At(0.5f, 0.5f, x, y, w, h + 70);
            pieces[(enemyIndex, partIndex)] = piece;
        }

        private void BuildParty()
        {
            for (int i = 0; i < session.Arthurs.Count; i++)
            {
                int index = i;
                var arthur = session.Arthurs[i];
                var widget = PartyWidget.Build(battleLayer, arthur, i == Me, () => OnPartyClicked(index));
                float y = -170 - i * 124;
                widget.Root.At(0, 1, 185, y, 330, 110);
                party.Add(widget);
                // 아군 위의 숫자는 카드 글자를 가리지 않게 카드 오른쪽 바깥에서 떠오른다.
                var floatAt = Ui.Rect(widget.Root, "FloatAt");
                floatAt.At(1, 0.5f, 70, -10, 10, 10);
                partyFloatAt.Add(floatAt);

                // 신호 말풍선 (양피지). 카드 오른쪽에 붙는다.
                var bubble = Ui.PaperBox(battleLayer, "Bubble", 14);
                bubble.rectTransform.At(0, 1, 448, y + 10, 172, 44);
                bubble.raycastTarget = false;
                var tail = Ui.Label(bubble.transform, "◀", 16, Ui.Paper, TextAnchor.MiddleLeft);
                tail.rectTransform.Fill();
                tail.rectTransform.offsetMin = new Vector2(-13, 0);
                var text = Ui.Label(bubble.transform, "", 17, Ui.PaperInk, TextAnchor.MiddleCenter, bold: true, serifFont: true);
                text.rectTransform.Fill(inset: 4);
                bubble.gameObject.SetActive(false);
                bubbles.Add(bubble.rectTransform);
                bubbleLabels.Add(text);
            }
        }

        private void BuildLog()
        {
            var frame = Ui.Frame(battleLayer, "Log", radius: 14);
            ((RectTransform)frame.transform.parent).At(1, 1, -180, -330, 320, 420);
            var title = Ui.Label(frame.transform, "전투 기록", 17, Ui.Gold, TextAnchor.MiddleCenter, bold: true);
            title.rectTransform.Fill(0, 1, 1, 1);
            title.rectTransform.offsetMin = new Vector2(0, -36);

            var paper = Ui.PaperBox(frame.transform, "Paper", 10);
            paper.rectTransform.Fill();
            paper.rectTransform.offsetMin = new Vector2(10, 10);
            paper.rectTransform.offsetMax = new Vector2(-10, -38);
            paper.raycastTarget = false;
            paper.gameObject.AddComponent<RectMask2D>();
            logLabel = Ui.Label(paper.transform, "", 15, Ui.PaperInk, TextAnchor.LowerLeft, serifFont: true);
            logLabel.rectTransform.Fill(inset: 10);
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

            // 코스트 보석 (왼쪽 아래)
            var gemRing = Ui.Circle(controls, "CostRing", Ui.Gold);
            gemRing.rectTransform.At(0, 0, 130, 160, 156, 156);
            gemRing.raycastTarget = false;
            var gem = Ui.Circle(gemRing.transform, "Cost", Hex("#243A55"));
            gem.rectTransform.Fill(inset: 4);
            gem.raycastTarget = false;
            costLabel = Ui.Label(gem.transform, "", 44, Ui.Text, TextAnchor.MiddleCenter, bold: true);
            costLabel.rectTransform.offsetMin = new Vector2(0, 10);
            var costCaption = Ui.Label(gem.transform, "COST", 15, Ui.GoldHi, TextAnchor.LowerCenter, bold: true);
            costCaption.rectTransform.offsetMin = new Vector2(0, 22);
            deckLabel = Ui.Label(controls, "", 15, Ui.Dim, TextAnchor.MiddleCenter);
            deckLabel.rectTransform.At(0, 0, 130, 56, 260, 26);

            // 신호 (오른쪽 아래)
            signalHeader = Ui.Label(controls, "", 16, Ui.Gold, TextAnchor.MiddleCenter, bold: true);
            signalHeader.rectTransform.At(1, 0, -250, 340, 460, 26);
            var signals = new[]
            {
                (SignalKind.Attack, "공격 갈게"), (SignalKind.NeedDefense, "방어 부탁"), (SignalKind.NeedHeal, "회복 부탁"),
                (SignalKind.Buff, "버프 걸게"), (SignalKind.FocusPart, "부위 노리자"), (SignalKind.Chain, "체인 가자"),
            };
            for (int i = 0; i < signals.Length; i++)
            {
                var kind = signals[i].Item1;
                var button = Ui.FrameButton(controls, signals[i].Item2, 17, () => SendMySignal(kind), out _);
                ((RectTransform)button.transform).At(1, 0, -400 + (i % 3) * 150, 296 - (i / 3) * 58, 140, 50);
                signalButtons.Add(button);
            }

            confirmButton = Ui.GoldButton(controls, "확  정", 30, Confirm, out confirmLabel);
            ((RectTransform)confirmButton.transform).At(1, 0, -250, 156, 300, 72);
            passButton = Ui.FrameButton(controls, "패스", 16, Pass, out _);
            ((RectTransform)passButton.transform).At(1, 0, -250, 84, 150, 40);
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
            timerFill.color = timeLeft < 5 ? Ui.Danger : Ui.Gold;
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
                Log("<color=#9B3A2A>시간 초과</color>");
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
            RebuildHand();
            RefreshView();
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
            bubble.gameObject.SetActive(true);
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
            costLabel.text = $"{remaining}<size=24>/{session.Cost}</size>";
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
                : session.HasSubmitted(Me) && planning ? "대기 중…"
                : "확  정";
        }

        /// <summary>적이 이번 턴에 할 행동을 부위 위에 토큰으로 띄운다. 전체 공격과 강화는 강조하고 부위가 빛난다.</summary>
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
            float wave = 0.22f + 0.18f * Mathf.Sin(Time.time * 4.5f);
            foreach (var pair in pieces)
            {
                bool on = telegraphed.Contains(pair.Key);
                pair.Value.Glow.color = on ? Ui.Danger.WithAlpha(wave) : Color.clear;
            }
        }

        /// <summary>손패를 부채꼴로 다시 깐다. 고른 카드는 위로 올라가고 고른 순서 번호가 붙는다.</summary>
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
                    var badge = Ui.Circle(rect, "Order", Ui.GoldHi);
                    badge.rectTransform.At(1, 1, -6, -6, 40, 40);
                    badge.raycastTarget = false;
                    Ui.Label(badge.transform, (order + 1).ToString(), 22, Hex("#2B2114"), TextAnchor.MiddleCenter, bold: true);
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
        /// 1) 모두의 카드를 뒷면으로 깔고 한 장씩 뒤집는다 (아군이 뭘 냈는지 여기서 처음 안다).
        /// 2) 체인이 터지면 띠 배너. 3) 카드 순서대로 효과. 4) 적 행동. 5) 독·재생.
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
                    var face = BuildRevealCard(slot, CardCatalog.Get(e.CardId), session.Arthurs[e.ActorIndex], e.IsAwakened);
                    face.gameObject.SetActive(false);
                    slot.gameObject.AddComponent<CanvasGroup>().alpha = 0;
                    slots.Add(slot);
                    faces.Add((back.gameObject, face.gameObject));
                }

                // 뒷면이 차례로 내려앉는다
                foreach (var slot in slots)
                {
                    StartCoroutine(DropIn(slot));
                    yield return new WaitForSeconds(0.06f);
                }
                yield return new WaitForSeconds(0.35f);

                // 한 장씩 뒤집기
                for (int i = 0; i < slots.Count; i++)
                    yield return Flip(slots[i], faces[i].back, faces[i].face);
                yield return new WaitForSeconds(0.25f);

                // 체인
                foreach (var chain in events.Where(e => e.Type == BattleEventType.Chain))
                {
                    for (int i = 0; i < played.Count; i++)
                    {
                        if (played[i].Element == chain.Element)
                            StartCoroutine(Punch(slots[i], 0.15f, 0.3f));
                    }
                    Log($"<color=#8A5A1A><b>{CardText.ElementName(chain.Element)} {chain.Chain}체인 ×{BattleRules.ChainMultiplier(chain.Chain):0.0}</b></color>");
                    yield return Banner($"{CardText.ElementName(chain.Element)}속성  {chain.Chain} CHAIN", Ui.ElementColor(chain.Element), 0.75f,
                        $"피해·회복 ×{BattleRules.ChainMultiplier(chain.Chain):0.0}");
                }
            }

            int current = -1;
            foreach (var e in events)
            {
                if (row != null && IsAfterPlayerPhase(e.Type))
                {
                    yield return FadeOutAndDestroy(row);
                    row = null;
                }

                if (e.Type == BattleEventType.CardPlayed)
                {
                    if (current >= 0)
                        slots[current].GetComponent<CanvasGroup>().alpha = 0.4f;
                    current++;
                    var slot = slots[current];
                    slot.SetAsLastSibling();
                    StartCoroutine(Tween(0.15f, t => slot.localScale = Vector3.one * (1 + 0.14f * t)));
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
                        var piece = PieceFor(e.TargetIndex, e.PartIndex);
                        if (e.PartIndex >= 0)
                            shownPartHp[e.TargetIndex][e.PartIndex] = Mathf.Max(0, shownPartHp[e.TargetIndex][e.PartIndex] - e.Value);
                        StartCoroutine(Punch(piece.Shape, 0.07f, 0.18f));
                        if (e.IsCritical)
                            Float(piece.Shape, $"치명 {e.Value:N0}!", Ui.GoldHi, 46);
                        else
                            Float(piece.Shape, e.Value.ToString("N0"), Color.white, 38);
                        Log(e.Message);
                        yield return new WaitForSeconds(e.IsCritical ? 0.32f : 0.2f);
                    }
                    else
                    {
                        shownArthurHp[e.TargetIndex] = Mathf.Max(0, shownArthurHp[e.TargetIndex] - e.Value);
                        StartCoroutine(Punch(party[e.TargetIndex].Root, 0.05f, 0.2f));
                        Float(partyFloatAt[e.TargetIndex], $"-{e.Value:N0}", Ui.Danger, 34);
                        Log($"<color=#9B3A2A>{e.Message}</color>");
                        yield return new WaitForSeconds(0.16f);
                    }
                    break;

                case BattleEventType.Heal:
                case BattleEventType.HealTick:
                    shownArthurHp[e.TargetIndex] = Mathf.Min(session.Arthurs[e.TargetIndex].MaxHp, shownArthurHp[e.TargetIndex] + e.Value);
                    Float(partyFloatAt[e.TargetIndex], $"+{e.Value:N0}", Ui.Good, 32);
                    Log($"<color=#3E7A50>{e.Message}</color>");
                    yield return new WaitForSeconds(0.14f);
                    break;

                case BattleEventType.PoisonTick:
                    if (e.TargetSide == BattleSide.Enemy)
                    {
                        shownEnemyHp[e.TargetIndex] = Mathf.Max(0, shownEnemyHp[e.TargetIndex] - e.Value);
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
                    string name = StatusName(e.EffectKind);
                    string text = e.EffectKind == EffectKind.HealBoost ? name : $"{name} {e.Value}";
                    Float(at, text, e.TargetSide == BattleSide.Ally ? Ui.GoldHi : Hex("#B48CE0"), 24, 40);
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
                    // 옥토패스의 브레이크처럼: 번쩍 + 흔들림 + 큰 글자
                    shownPartHp[e.TargetIndex][e.PartIndex] = 0;
                    var piece = PieceFor(e.TargetIndex, e.PartIndex);
                    RefreshView();
                    StartCoroutine(Flash(piece.Shape));
                    StartCoroutine(Shake(14, 0.35f));
                    Float(piece.Shape, "BREAK!", Ui.GoldHi, 60, 30);
                    Log($"<color=#8A5A1A><b>{e.Message}</b></color>");
                    yield return new WaitForSeconds(0.7f);
                    break;
                }

                case BattleEventType.EnemyDefeated:
                    shownEnemyHp[e.TargetIndex] = 0;
                    foreach (var pair in pieces.Where(p => p.Key.enemy == e.TargetIndex))
                    {
                        pair.Value.ClearTokens();
                        telegraphed.Remove(pair.Key);
                    }
                    RefreshView();
                    Float(PieceFor(e.TargetIndex, -1).Shape, "격파", Ui.GoldHi, 52, 110);
                    Log($"<b>{e.Message}</b>");
                    yield return new WaitForSeconds(0.45f);
                    break;

                case BattleEventType.ArthurDown:
                    shownArthurHp[e.TargetIndex] = 0;
                    Float(partyFloatAt[e.TargetIndex], "쓰러짐", Ui.Danger, 30, 30);
                    Log($"<color=#9B3A2A><b>{e.Message}</b></color>");
                    yield return new WaitForSeconds(0.35f);
                    break;

                case BattleEventType.EnemyAction:
                {
                    var piece = PieceFor(e.ActorIndex, e.PartIndex);
                    StartCoroutine(Punch(piece.Shape, 0.12f, 0.3f));
                    Log($"<color=#9B3A2A>{e.Message}</color>");
                    string name = e.Message.Contains("「") ? e.Message.Substring(e.Message.IndexOf('「')) : e.Message;
                    yield return Banner(name, Ui.Danger, 0.45f, session.Enemies[e.ActorIndex].Name, small: true);
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

        private static string StatusName(EffectKind kind)
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

        /// <summary>공개 줄에 놓이는 작은 카드 앞면. 누가 냈는지 양피지 이름표로 붙는다.</summary>
        private static RectTransform BuildRevealCard(Transform parent, CardDefinition def, Arthur owner, bool awakened)
        {
            const float w = RevealWidth, h = RevealHeight;
            var outer = Ui.Box(parent, def.NameKo, awakened ? Ui.GoldHi : Ui.Gold, 12);
            outer.raycastTarget = false;
            var rect = outer.rectTransform;
            rect.At(0.5f, 0.5f, 0, 0, w, h);
            var body = Ui.Box(outer.transform, "Body", Ui.PanelColor, 10);
            body.rectTransform.Fill(inset: awakened ? 3 : 2);
            body.raycastTarget = false;

            var band = Ui.Box(body.transform, "Band", Ui.ElementColor(def.Element), 10);
            band.rectTransform.Fill(0, 1, 1, 1);
            band.rectTransform.offsetMin = new Vector2(0, -40);
            band.raycastTarget = false;
            var cost = Ui.Label(band.transform, def.Cost.ToString(), 22, Ui.Text, TextAnchor.MiddleLeft, bold: true);
            cost.rectTransform.offsetMin = new Vector2(12, 0);
            var stars = Ui.Label(band.transform, CardText.Stars(def.Rarity), 12, Hex("#FFF8E6"), TextAnchor.MiddleRight);
            stars.rectTransform.offsetMax = new Vector2(-10, 0);

            Ui.Pill(body.transform, def.NameKo, def.NameKo.Length > 7 ? 12 : 14, out var pill);
            pill.rectTransform.At(0.5f, 1, 0, -60, w - 18, 26);
            pill.raycastTarget = false;

            var art = Ui.Box(body.transform, "Art", Ui.Panel2, 8);
            art.rectTransform.At(0.5f, 1, 0, -108, w - 18, 58);
            art.raycastTarget = false;
            if (!Art.TryApply(art, $"Cards/card_{def.Id}", Color.white))
                Ui.Label(art.transform, CardText.JobName(def.Job), 16, Ui.JobColor(def.Job).WithAlpha(0.8f), TextAnchor.MiddleCenter, bold: true);

            if (awakened)
            {
                var awake = Ui.Label(body.transform, "◆ 각성", 13, Ui.GoldHi, TextAnchor.MiddleCenter, bold: true);
                awake.rectTransform.At(0.5f, 0, 0, 58, w, 20);
            }
            var plate = Ui.PaperBox(body.transform, "Owner", 8);
            plate.rectTransform.At(0.5f, 0, 0, 26, w - 18, 34);
            plate.raycastTarget = false;
            Ui.Label(plate.transform, owner.Name, 15, Ui.PaperInk, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            return rect;
        }

        // ───────────────────────── 결과 ─────────────────────────

        private void ShowResult()
        {
            bool victory = session.Outcome == BattleOutcome.Victory;
            resolving = false;
            var dim = Ui.Box(overlayLayer, "Dim", Ui.Ink.WithAlpha(0.7f), 1);
            dim.rectTransform.Fill();

            var panel = Ui.Frame(overlayLayer, "Result", border: victory ? Ui.GoldHi : Ui.GoldDark, radius: 18, borderWidth: 3);
            var outer = (RectTransform)panel.transform.parent;
            outer.At(0.5f, 0.5f, 0, 0, 680, 400);

            var title = Ui.Label(panel.transform, victory ? "승  리" : "패  배", 72, victory ? Ui.GoldHi : Ui.Danger, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            title.rectTransform.At(0.5f, 1, 0, -100, 600, 100);
            int alive = session.Arthurs.Count(a => a.IsAlive);
            var sub = Ui.Label(panel.transform, $"{session.Turn}턴 · 생존 {alive}/{session.Arthurs.Count}", 22, Ui.Text, TextAnchor.MiddleCenter);
            sub.rectTransform.At(0.5f, 1, 0, -180, 600, 40);

            var again = Ui.GoldButton(panel.transform, "다시 하기", 24, StartBattle, out _);
            ((RectTransform)again.transform).At(0.5f, 0, -140, 70, 240, 64);
            var back = Ui.FrameButton(panel.transform, "출격 준비", 22, ShowSetup, out _);
            ((RectTransform)back.transform).At(0.5f, 0, 140, 70, 240, 64);

            StartCoroutine(Pop(outer));
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
            yield return Tween(0.22f, t => rect.localScale = Vector3.one * (0.6f + 0.4f * t + 0.12f * Mathf.Sin(t * Mathf.PI)));
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

        /// <summary>마블 스냅처럼 가로로 접었다가 앞면으로 편다.</summary>
        private static IEnumerator Flip(RectTransform slot, GameObject back, GameObject face)
        {
            yield return Tween(0.1f, t => slot.localScale = new Vector3(1 - t, 1 + 0.08f * t, 1));
            back.SetActive(false);
            face.SetActive(true);
            yield return Tween(0.14f, t => slot.localScale = new Vector3(t, 1.08f - 0.08f * t, 1));
        }

        private IEnumerator FadeOutAndDestroy(RectTransform rect)
        {
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null)
                group = rect.gameObject.AddComponent<CanvasGroup>();
            yield return Tween(0.2f, t => group.alpha = 1 - t);
            Destroy(rect.gameObject);
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
        private void Float(RectTransform at, string text, Color color, int size, float rise = 0)
        {
            var label = Ui.Label(fxLayer, text, size, color, TextAnchor.MiddleCenter, bold: true);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420, size + 24);
            rect.position = at.position;
            rect.anchoredPosition += new Vector2(UnityEngine.Random.Range(-36f, 36f), rise + UnityEngine.Random.Range(-10f, 10f));
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(2, -2);
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

        /// <summary>화면을 가로지르는 띠 배너 (턴 시작, 체인, 적의 큰 기술).</summary>
        private IEnumerator Banner(string text, Color stripe, float hold, string sub = null, bool small = false)
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

            var label = Ui.Label(band.transform, text, small ? 40 : 58, Ui.GoldHi, TextAnchor.MiddleCenter, bold: true, serifFont: true);
            label.rectTransform.Fill(0, sub != null ? 0.32f : 0, 1, 1);
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);
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
