using UnityEngine;
using Tanks.Complete;

public class BowlingPin : MonoBehaviour
{
    [Header("“|‚ê‚½”»’è")]
    [SerializeField] private float fallenAngle = 45f;

    private CarBowlingManager manager;

    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private bool isFallen = false;

    private void Awake()
    {
        // ‰ŠúˆÊ’u‚Æ‰Šú‰ñ“]‚ğ•Û‘¶
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    private void Start()
    {
        // ƒV[ƒ““à‚ÌCarBowlingManager‚ğæ“¾
        manager = FindFirstObjectByType<CarBowlingManager>();
    }

    private void Update()
    {
        if (isFallen) return;

        // ‰Šúp¨‚©‚ç‚ÌŒX‚«‚ğæ“¾
        float angle = Quaternion.Angle(
            initialRotation,
            transform.rotation
        );

        // ˆê’èˆÈãŒX‚¢‚½‚ç“|‚ê‚½‚Æ”»’è
        if (angle >= fallenAngle)
        {
            isFallen = true;

            if (manager != null)
            {
                manager.AddScore(this);
            }
        }
    }

    public void ResetPin()
    {
        // •¨—‹““®‚ğˆê“x’â~
        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // ‰ŠúˆÊ’u‚Æ‰ñ“]‚É–ß‚·
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        // •¨—‹““®‚ğÄŠJ
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        isFallen = false;
    }

    public bool IsFallen()
    {
        return isFallen;
    }
}