using System.Collections.Generic;
using System.Linq;
using Tanks.Complete;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class CarBowlingManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody carRb;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private BowlingPin[] pins;

        [Header("UI")]
        [SerializeField] private Text playerText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text turnText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text rankingText;

        [SerializeField] private Button nextThrowButton;
        [SerializeField] private Button nextPlayerButton;
        [SerializeField] private GameObject rankingPanel;

        [Header("Players")]
        [SerializeField]
        private string[] playerNames =
        {
            "Player 1",
            "Player 2",
            "Player 3"
        };

        [Header("Game Settings")]
        [SerializeField] private int attemptsPerPlayer = 3;
        [SerializeField] private int scorePerPin = 10;

        [Header("Stop Detection")]
        [SerializeField] private float startSpeedKmh = 3f;
        [SerializeField] private float stopSpeedKmh = 1.5f;
        [SerializeField] private float stopDuration = 2f;

        private Vector3 startPosition;
        private Quaternion startRotation;

        private int[] totalScores;

        private int currentPlayerIndex = 0;
        private int currentThrow = 1;

        private int currentThrowScore = 0;
        private int fallenPinCount = 0;

        private float stopTimer = 0f;

        private bool isThrowing = false;
        private bool isStarted = false;
        private bool isGameEnded = false;

        private HashSet<BowlingPin> scoredPins =
            new HashSet<BowlingPin>();

        private CarMove carMove;

        private void Awake()
        {
            // 車の初期位置を保存
            startPosition = carRb.position;
            startRotation = carRb.rotation;

            // CarControllerを取得
            carMove = carRb.GetComponent<CarMove>();

            // プレイヤーごとの得点を初期化
            totalScores = new int[playerNames.Length];
        }

        private void Start()
        {
            nextThrowButton.gameObject.SetActive(false);
            nextPlayerButton.gameObject.SetActive(false);

            rankingPanel.SetActive(false);

            resultText.text = "";
            rankingText.text = "";

            ResetPins();

            UpdatePlayerUI();
            UpdateScoreUI();

            StartThrow();
        }

        private void Update()
        {
            if (!isThrowing || isGameEnded) return;

            // 車の速度を取得
            float speed =
                carRb.linearVelocity.magnitude * 3.6f;

            if (!isStarted)
            {
                // 車が動き始めるまで待つ
                if (speed >= startSpeedKmh)
                {
                    isStarted = true;
                }

                return;
            }

            // 車が停止したか確認
            if (speed <= stopSpeedKmh)
            {
                stopTimer += Time.deltaTime;

                if (stopTimer >= stopDuration)
                {
                    FinishThrow();
                }
            }
            else
            {
                stopTimer = 0f;
            }
        }

        // ピンが倒れたときに呼ばれる
        public void AddScore(BowlingPin pin)
        {
            if (!isThrowing) return;

            // 同じピンの二重加点を防止
            if (!scoredPins.Add(pin)) return;

            // 得点を加算
            fallenPinCount++;
            currentThrowScore += scorePerPin;

            totalScores[currentPlayerIndex] += scorePerPin;

            UpdateScoreUI();
        }

        // 投球開始
        private void StartThrow()
        {
            if (isGameEnded) return;

            isThrowing = true;
            isStarted = false;

            stopTimer = 0f;

            currentThrowScore = 0;
            fallenPinCount = 0;

            scoredPins.Clear();

            ResetPins();
            ResetCar();

            playerInput.enabled = true;

            nextThrowButton.gameObject.SetActive(false);
            nextPlayerButton.gameObject.SetActive(false);

            resultText.text = "";

            UpdatePlayerUI();
            UpdateScoreUI();
        }

        // 投球終了
        private void FinishThrow()
        {
            if (!isThrowing) return;

            isThrowing = false;

            playerInput.enabled = false;

            resultText.text =
                $"{fallenPinCount}本倒した！\n" +
                $"{currentThrowScore}点獲得！";

            if (currentThrow < attemptsPerPlayer)
            {
                nextThrowButton.gameObject.SetActive(true);
            }
            else
            {
                if (currentPlayerIndex < playerNames.Length - 1)
                {
                    nextPlayerButton.gameObject.SetActive(true);
                }
                else
                {
                    ShowRanking();
                }
            }

            UpdateScoreUI();
        }

        // 次の投球
        public void NextThrow()
        {
            if (isThrowing || isGameEnded) return;

            currentThrow++;

            StartThrow();
        }

        // 次のプレイヤー
        public void NextPlayer()
        {
            if (isThrowing || isGameEnded) return;

            currentPlayerIndex++;

            currentThrow = 1;

            StartThrow();
        }

        // 車を初期位置に戻す
        private void ResetCar()
        {
            carRb.linearVelocity = Vector3.zero;
            carRb.angularVelocity = Vector3.zero;

            carRb.position = startPosition;
            carRb.rotation = startRotation;

            carMove.ResetMovementState();

            Physics.SyncTransforms();
        }

        // 全ピンを初期状態に戻す
        private void ResetPins()
        {
            foreach (BowlingPin pin in pins)
            {
                pin.ResetPin();
            }

            Physics.SyncTransforms();
        }

        // プレイヤー表示
        private void UpdatePlayerUI()
        {
            playerText.text =
                $"PLAYER : {playerNames[currentPlayerIndex]}";

            turnText.text =
                $"THROW : {currentThrow} / {attemptsPerPlayer}";
        }

        // 得点表示
        private void UpdateScoreUI()
        {
            scoreText.text =
                $"SCORE : {totalScores[currentPlayerIndex]}点\n" +
                $"今回 : {currentThrowScore}点";
        }

        // ランキング表示
        private void ShowRanking()
        {
            isGameEnded = true;

            rankingPanel.SetActive(true);

            var ranking = totalScores
                .Select((score, index) => new
                {
                    Name = playerNames[index],
                    Score = score,
                    Index = index
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Index)
                .ToList();

            rankingText.text = "RESULT RANKING\n\n";

            int rank = 1;

            foreach (var player in ranking)
            {
                rankingText.text +=
                    $"{rank}位  {player.Name}  {player.Score}点\n";

                rank++;
            }

            resultText.text = "GAME SET!";
            playerText.text = "ALL PLAYERS FINISHED";
        }
    }
}
