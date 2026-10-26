using System;
using System.Collections;
using System.IO;
using System.Linq;
using ChainKnights.Core.Battle;
using UnityEngine;

namespace ChainKnights.Client.Battle
{
    /// <summary>
    /// 화면 확인용. 명령줄에 -autoshot &lt;폴더&gt; 를 주면 준비 화면 → 몇 턴의 선택과 공개 연출을 찍고 끝낸다.
    /// -raid를 같이 주면 글라키에스전을 찍는다. 발표 자료용 스크린샷을 뽑을 때도 쓴다.
    /// </summary>
    public sealed partial class BattleScreen
    {
        private void StartAutoShotIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-autoshot");
            if (index < 0 || index + 1 >= args.Length)
                return;
            // 창이 뒤로 가도 멈추지 않게 한다 (기본값이면 포커스를 잃는 순간 멈춘다).
            Application.runInBackground = true;
            var runner = new GameObject("AutoShot").AddComponent<AutoShotRunner>();
            runner.Begin(this, args[index + 1], args.Contains("-raid"));
        }

        // BattleScreen이 화면을 바꿀 때 자기 코루틴을 모두 멈추므로, 찍는 쪽은 따로 돈다.
        private sealed class AutoShotRunner : MonoBehaviour
        {
            private BattleScreen screen;
            private string folder;

            public void Begin(BattleScreen target, string outputFolder, bool raid)
            {
                screen = target;
                folder = outputFolder;
                Directory.CreateDirectory(folder);
                StartCoroutine(Run(raid));
            }

            private IEnumerator Run(bool raid)
            {
                yield return new WaitForSeconds(1f);
                if (raid)
                {
                    screen.quest = Quest.Glacies;
                    screen.ShowSetup();
                    yield return new WaitForSeconds(0.5f);
                }
                Shot("01_setup");
                yield return new WaitForSeconds(0.3f);
                screen.StartBattle();

                for (int turn = 1; turn <= 3 && screen.session.Phase == BattlePhase.Planning; turn++)
                {
                    yield return new WaitForSeconds(1.2f);
                    foreach (var card in screen.session.Arthurs[Me].Hand.ToList())
                    {
                        if (screen.PickedCost + card.Card.Cost <= screen.session.Cost)
                            screen.ToggleCard(card.InstanceId);
                    }
                    yield return new WaitForSeconds(0.5f);
                    Shot($"t{turn}_a_plan");
                    screen.Confirm();

                    while (!screen.resolving)
                        yield return null;
                    for (int i = 0; screen.resolving && i < 14; i++)
                    {
                        yield return new WaitForSeconds(0.45f);
                        Shot($"t{turn}_b_resolve_{i:00}");
                    }
                    while (screen.resolving)
                        yield return null;
                }
                yield return new WaitForSeconds(0.8f);
                Shot("99_end");
                yield return new WaitForSeconds(0.5f);
                Application.Quit();
            }

            private void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        }
    }
}
