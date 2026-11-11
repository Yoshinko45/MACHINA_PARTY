using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

namespace Tanks.Complete
{
    public class ChickenRaceManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody carRb;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private Transform carFront;
        [SerializeField] private Transform cliffEdge;

        [Header("UI")]
        [SerializeField] private Text speedText;
        [SerializeField] private Text distanceText;
        [SerializeField] private Text playerText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text rankingText;
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

        [Header("Settings")]
        [SerializeField] private float startSpeed = 5f;
        [SerializeField] private float stopSpeed = 0.5f;
        [SerializeField] private float stopDuration = 1f;
        [SerializeField] private float fallHeight = -5f;

        private CarMove carMove;

        private int currentPlayerIndex = 0;

        private bool isStarted = false;
        private bool isFinished = false;
        private bool isGameEnded = false;

        private float stopTimer = 0f;
        private float currentDistance;

        private Vector3 startPosition;
        private Quaternion startRotation;

        // プレイヤーごとの記録
        private List<float> records = new List<float>();

        private void Awake()
        {
            carMove = carRb.GetComponent<CarMove>();

            startPosition = carRb.position;
            startRotation = carRb.rotation;

            for (int i = 0; i < playerNames.Length; i++)
            {
                records.Add(-1f);
            }
        }

        private void Start()
        {
            nextPlayerButton.gameObject.SetActive(false);
            rankingPanel.SetActive(false);

            rankingText.text = "";
            resultText.text = "";

            UpdatePlayerUI();
        }

        private void Update()
        {
            if (isFinished || isGameEnded)
            {
                return;
            }

            // 速度表示
            float speed = carRb.linearVelocity.magnitude * 3.6f;

            speedText.text = $"{speed:F0} km/h";

            // 崖までの距離
            Vector3 direction = cliffEdge.forward;
            direction.y = 0f;
            direction.Normalize();

            currentDistance = Vector3.Dot(
                cliffEdge.position - carFront.position,
                direction
            );

            distanceText.text =
                $"Cliff : {currentDistance:F2} m";

            // ゲーム開始判定
            if (!isStarted)
            {
                if (speed >= startSpeed)
                {
                    isStarted = true;
                }

                return;
            }

            // 落下判定
            if (carRb.position.y < fallHeight ||
                currentDistance <= 0f)
            {
                FinishTurn(-1f, "FAILED");
                //FinishGame("FAILED");
                return;
            }

            // 停止判定
            if (speed <= stopSpeed)
            {
                stopTimer += Time.deltaTime;

                if (stopTimer >= stopDuration)
                {
                    JudgeResult();
                }
            }
            else
            {
                stopTimer = 0f;
            }
        }

        private void JudgeResult()
        {
            string result;

            if (currentDistance <= 0.5f)
                result = "PERFECT!";
            else if (currentDistance <= 2f)
                result = "GREAT!";
            else if (currentDistance <= 5f)
                result = "GOOD!";
            else if (currentDistance <= 10f)
                result = "NORMAL";
            else
                result = "MISS";

            FinishTurn(currentDistance, result);
        }

        private void FinishTurn(float distance, string result)
        {
            isFinished = true;

            records[currentPlayerIndex] = distance;

            // 車の操作を無効化
            playerInput.enabled = false;

            resultText.text =
                $"{playerNames[currentPlayerIndex]}\n" +
                $"{result}\n" +
                (distance >= 0f
                    ? $"Distance : {distance:F2} m"
                    : "落下しました！");

            if (currentPlayerIndex < playerNames.Length - 1)
            {
                nextPlayerButton.gameObject.SetActive(true);
            }
            else
            {
                ShowRanking();
            }
        }

        public void NextPlayer()
        {
            if (!isFinished || isGameEnded)
                return;

            currentPlayerIndex++;

            ResetCar();
            UpdatePlayerUI();

            resultText.text = "";

            nextPlayerButton.gameObject.SetActive(false);

            isStarted = false;
            isFinished = false;
            stopTimer = 0f;

            playerInput.enabled = true;
        }

        private void ResetCar()
        {
            carRb.linearVelocity = Vector3.zero;
            carRb.angularVelocity = Vector3.zero;

            carRb.position = startPosition;
            carRb.rotation = startRotation;

            carMove.ResetMovementState();
        }

        private void UpdatePlayerUI()
        {
            playerText.text =
                $"TURN : {playerNames[currentPlayerIndex]}";
        }

        private void ShowRanking()
        {
            isGameEnded = true;

            var ranking = records
                .Select((distance, index) => new
                {
                    Name = playerNames[index],
                    Distance = distance,
                    Index = index
                })
                .OrderBy(x =>
                    x.Distance < 0f ? float.MaxValue : x.Distance)
                .ThenBy(x => x.Index)
                .ToList();

            rankingText.text = "RESULT RANKING\n\n";

            int rank = 1;

            foreach (var player in ranking)
            {
                string score = player.Distance < 0f
                    ? "FAILED"
                    : $"{player.Distance:F2} m";

                rankingText.text +=
                    $"{rank}位  {player.Name}  {score}\n";

                rank++;
            }

            resultText.text = "GAME SET!";
            playerText.text = "ALL PLAYERS FINISHED";
            rankingPanel.SetActive(true);
        }

    }
}