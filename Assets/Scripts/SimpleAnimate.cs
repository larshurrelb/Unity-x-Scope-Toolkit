using UnityEngine;

public class SimpleAnimate : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 1f; // Duration of the animation
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("3D Fold Effect")]
    [SerializeField] private float startRotationX = 90f; // Starting X rotation (90 = facing down)
    
    private RectTransform rectTransform;
    private float elapsedTime = 0f;
    private bool isAnimating = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("SimpleAnimate requires a RectTransform component (UI element)");
            enabled = false;
            return;
        }
    }

    void OnEnable()
    {
        // Start with X rotation at 90 degrees
        rectTransform.localEulerAngles = new Vector3(startRotationX, 0, 0);
        elapsedTime = 0f;
        isAnimating = true;
    }

    void Update()
    {
        if (!isAnimating) return;

        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / animationDuration);
        float curvedProgress = animationCurve.Evaluate(progress);

        // Interpolate X rotation from startRotationX to 0 (fold-in effect)
        float currentRotationX = Mathf.Lerp(startRotationX, 0f, curvedProgress);
        rectTransform.localEulerAngles = new Vector3(currentRotationX, 0, 0);

        if (progress >= 1f)
        {
            isAnimating = false;
            // Ensure exact final value
            rectTransform.localEulerAngles = Vector3.zero;
        }
    }
}
