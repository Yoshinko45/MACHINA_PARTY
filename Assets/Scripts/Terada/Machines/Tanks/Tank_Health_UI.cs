using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class Tank_Health_UI : MonoBehaviour
    {
        [SerializeField] Tank_Health health;

        [SerializeField] Slider slider;
        [SerializeField] Image fill;

        [SerializeField]
        Color fullColor = Color.green;

        [SerializeField]
        Color emptyColor = Color.red;

        [SerializeField]
        float smoothSpeed = 8f;

        float displayHealth;

        void Start()
        {
            displayHealth =
                health.MaxHealth;
        }

        void Update()
        {
            displayHealth =
                Mathf.Lerp(
                    displayHealth,
                    health.Health,
                    Time.deltaTime * smoothSpeed);

            slider.value = displayHealth;

            fill.color =
                Color.Lerp(
                    emptyColor,
                    fullColor,
                    displayHealth /
                    health.MaxHealth);
        }
    }
}