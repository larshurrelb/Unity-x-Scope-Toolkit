using UnityEngine;
using System.Collections;

/// <summary>
/// Flexible UI animator that can be attached to any UI element.
/// Provides multiple animation types: bounce, hover, pulse, rotate, fade, and more.
/// </summary>
public class UIAnimator : MonoBehaviour
{
    #region Animation Types

    public enum AnimationType
    {
        None,
        Bounce,
        HoverUpDown,
        HoverLeftRight,
        Pulse,
        PulseColor,
        Rotate,
        Shake,
        Float,
        FadeInOut,
        ScaleIn,
        SlideIn,
        Wiggle
    }

    public enum LoopType
    {
        Once,
        Loop,
        PingPong
    }

    public enum SlideDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    #endregion

    #region Inspector Fields

    [Header("Animation Settings")]
    [Tooltip("Type of animation to play")]
    public AnimationType animationType = AnimationType.Pulse;

    [Tooltip("Should the animation play automatically on enable?")]
    public bool playOnEnable = true;

    [Tooltip("Delay before starting the animation")]
    public float startDelay = 0f;

    [Tooltip("How the animation should loop")]
    public LoopType loopType = LoopType.Loop;

    [Header("Timing")]
    [Tooltip("Duration of one animation cycle")]
    public float duration = 1f;

    [Tooltip("Delay between animation loops (only for Loop type)")]
    public float loopDelay = 0f;

    [Header("Bounce Settings")]
    [Tooltip("Height of the bounce")]
    public float bounceHeight = 30f;

    [Tooltip("Number of bounces")]
    public int bounceCount = 3;

    [Header("Hover Settings")]
    [Tooltip("Distance to hover")]
    public float hoverDistance = 20f;

    [Tooltip("Speed of hover animation")]
    public float hoverSpeed = 1f;

    [Header("Pulse Settings")]
    [Tooltip("Scale multiplier for pulse")]
    public float pulseScale = 1.2f;

    [Tooltip("Color to pulse to (for PulseColor)")]
    public Color pulseColor = Color.white;

    [Header("Rotate Settings")]
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 90f;

    [Tooltip("Clockwise or counter-clockwise")]
    public bool clockwise = true;

    [Header("Shake Settings")]
    [Tooltip("Intensity of shake")]
    public float shakeIntensity = 10f;

    [Tooltip("Number of shakes")]
    public int shakeCount = 5;

    [Header("Fade Settings")]
    [Tooltip("Minimum alpha for fade")]
    [Range(0f, 1f)]
    public float minAlpha = 0f;

    [Tooltip("Maximum alpha for fade")]
    [Range(0f, 1f)]
    public float maxAlpha = 1f;

    [Header("Slide Settings")]
    [Tooltip("Direction to slide in from")]
    public SlideDirection slideDirection = SlideDirection.Left;

    [Tooltip("Distance to slide from (in pixels)")]
    public float slideDistance = 500f;

    [Header("Animation Curve")]
    [Tooltip("Custom animation curve for easing")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    #endregion

    #region Private Fields

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private UnityEngine.UI.Graphic graphic;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private Color originalColor;
    private Coroutine currentAnimation;
    private bool isAnimating = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        graphic = GetComponent<UnityEngine.UI.Graphic>();

        // Store original values
        if (rectTransform != null)
        {
            originalPosition = rectTransform.anchoredPosition;
            originalScale = rectTransform.localScale;
            originalRotation = rectTransform.localRotation;
        }

        if (graphic != null)
        {
            originalColor = graphic.color;
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            PlayAnimation();
        }
    }

    private void OnDisable()
    {
        StopAnimation();
        ResetToOriginal();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Start playing the configured animation
    /// </summary>
    public void PlayAnimation()
    {
        if (isAnimating) return;

        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
        }

        currentAnimation = StartCoroutine(AnimationRoutine());
    }

    /// <summary>
    /// Stop the current animation
    /// </summary>
    public void StopAnimation()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        isAnimating = false;
    }

    /// <summary>
    /// Reset UI element to original state
    /// </summary>
    public void ResetToOriginal()
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = originalPosition;
            rectTransform.localScale = originalScale;
            rectTransform.localRotation = originalRotation;
        }

        if (graphic != null)
        {
            graphic.color = originalColor;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    #endregion

    #region Animation Coroutines

    private IEnumerator AnimationRoutine()
    {
        if (startDelay > 0)
        {
            yield return new WaitForSeconds(startDelay);
        }

        isAnimating = true;

        do
        {
            switch (animationType)
            {
                case AnimationType.Bounce:
                    yield return BounceAnimation();
                    break;
                case AnimationType.HoverUpDown:
                    yield return HoverAnimation(Vector2.up);
                    break;
                case AnimationType.HoverLeftRight:
                    yield return HoverAnimation(Vector2.right);
                    break;
                case AnimationType.Pulse:
                    yield return PulseAnimation();
                    break;
                case AnimationType.PulseColor:
                    yield return PulseColorAnimation();
                    break;
                case AnimationType.Rotate:
                    yield return RotateAnimation();
                    break;
                case AnimationType.Shake:
                    yield return ShakeAnimation();
                    break;
                case AnimationType.Float:
                    yield return FloatAnimation();
                    break;
                case AnimationType.FadeInOut:
                    yield return FadeAnimation();
                    break;
                case AnimationType.ScaleIn:
                    yield return ScaleInAnimation();
                    break;
                case AnimationType.SlideIn:
                    yield return SlideInAnimation();
                    break;
                case AnimationType.Wiggle:
                    yield return WiggleAnimation();
                    break;
            }

            if (loopType == LoopType.Loop && loopDelay > 0)
            {
                yield return new WaitForSeconds(loopDelay);
            }

        } while (loopType == LoopType.Loop);

        isAnimating = false;
    }

    private IEnumerator BounceAnimation()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // Create bounce effect using sine wave
            float bounce = Mathf.Abs(Mathf.Sin(progress * Mathf.PI * bounceCount)) * bounceHeight;
            bounce *= (1 - progress); // Decay over time

            rectTransform.anchoredPosition = originalPosition + Vector3.up * bounce;
            yield return null;
        }

        rectTransform.anchoredPosition = originalPosition;
    }

    private IEnumerator HoverAnimation(Vector2 direction)
    {
        float elapsed = 0f;

        while (elapsed < duration || loopType == LoopType.Loop || loopType == LoopType.PingPong)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * hoverSpeed) * hoverDistance;
            rectTransform.anchoredPosition = originalPosition + (Vector3)(direction * offset);
            yield return null;

            if (loopType == LoopType.Once && elapsed >= duration)
                break;
        }

        rectTransform.anchoredPosition = originalPosition;
    }

    private IEnumerator PulseAnimation()
    {
        float elapsed = 0f;

        while (elapsed < duration || loopType == LoopType.Loop || loopType == LoopType.PingPong)
        {
            elapsed += Time.deltaTime;
            float scale = 1 + (pulseScale - 1) * Mathf.PingPong(elapsed / duration, 1);
            rectTransform.localScale = originalScale * scale;
            yield return null;

            if (loopType == LoopType.Once && elapsed >= duration)
                break;
        }

        rectTransform.localScale = originalScale;
    }

    private IEnumerator PulseColorAnimation()
    {
        if (graphic == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration || loopType == LoopType.Loop || loopType == LoopType.PingPong)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed / duration, 1);
            graphic.color = Color.Lerp(originalColor, pulseColor, t);
            yield return null;

            if (loopType == LoopType.Once && elapsed >= duration)
                break;
        }

        graphic.color = originalColor;
    }

    private IEnumerator RotateAnimation()
    {
        float elapsed = 0f;
        float direction = clockwise ? -1f : 1f;

        while (elapsed < duration || loopType == LoopType.Loop)
        {
            elapsed += Time.deltaTime;
            float rotation = elapsed * rotationSpeed * direction;
            rectTransform.localRotation = originalRotation * Quaternion.Euler(0, 0, rotation);
            yield return null;

            if (loopType == LoopType.Once && elapsed >= duration)
                break;
        }

        rectTransform.localRotation = originalRotation;
    }

    private IEnumerator ShakeAnimation()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // Shake with decreasing intensity
            float intensity = shakeIntensity * (1 - progress);
            Vector3 randomOffset = new Vector3(
                Random.Range(-intensity, intensity),
                Random.Range(-intensity, intensity),
                0
            );

            rectTransform.anchoredPosition = originalPosition + randomOffset;
            yield return null;
        }

        rectTransform.anchoredPosition = originalPosition;
    }

    private IEnumerator FloatAnimation()
    {
        float elapsed = 0f;

        while (elapsed < duration || loopType == LoopType.Loop || loopType == LoopType.PingPong)
        {
            elapsed += Time.deltaTime;
            float y = Mathf.Sin(elapsed * Mathf.PI * 2 / duration) * hoverDistance;
            float x = Mathf.Cos(elapsed * Mathf.PI * 2 / duration) * (hoverDistance * 0.5f);
            rectTransform.anchoredPosition = originalPosition + new Vector3(x, y, 0);
            yield return null;

            if (loopType == LoopType.Once && elapsed >= duration)
                break;
        }

        rectTransform.anchoredPosition = originalPosition;
    }

    private IEnumerator FadeAnimation()
    {
        // Ensure we have a CanvasGroup
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        float elapsed = 0f;

        while (elapsed < duration || loopType == LoopType.Loop || loopType == LoopType.PingPong)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed / duration, 1);
            canvasGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
            yield return null;

            if (loopType == LoopType.Once && elapsed >= duration)
                break;
        }

        canvasGroup.alpha = maxAlpha;
    }

    private IEnumerator ScaleInAnimation()
    {
        rectTransform.localScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = easeCurve.Evaluate(elapsed / duration);
            rectTransform.localScale = Vector3.Lerp(Vector3.zero, originalScale, t);
            yield return null;
        }

        rectTransform.localScale = originalScale;
    }

    private IEnumerator SlideInAnimation()
    {
        Vector3 offset = Vector3.zero;
        switch (slideDirection)
        {
            case SlideDirection.Left:
                offset = Vector3.left * slideDistance;
                break;
            case SlideDirection.Right:
                offset = Vector3.right * slideDistance;
                break;
            case SlideDirection.Up:
                offset = Vector3.up * slideDistance;
                break;
            case SlideDirection.Down:
                offset = Vector3.down * slideDistance;
                break;
        }

        Vector3 startPos = originalPosition + offset;
        rectTransform.anchoredPosition = startPos;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = easeCurve.Evaluate(elapsed / duration);
            rectTransform.anchoredPosition = Vector3.Lerp(startPos, originalPosition, t);
            yield return null;
        }

        rectTransform.anchoredPosition = originalPosition;
    }

    private IEnumerator WiggleAnimation()
    {
        float elapsed = 0f;

        while (elapsed < duration || loopType == LoopType.Loop || loopType == LoopType.PingPong)
        {
            elapsed += Time.deltaTime;
            float angle = Mathf.Sin(elapsed * 10f) * 15f; // Fast wiggle
            rectTransform.localRotation = originalRotation * Quaternion.Euler(0, 0, angle);
            yield return null;

            if (loopType == LoopType.Once && elapsed >= duration)
                break;
        }

        rectTransform.localRotation = originalRotation;
    }

    #endregion

    #region Editor Helpers

    private void OnValidate()
    {
        // Clamp values
        duration = Mathf.Max(0.1f, duration);
        startDelay = Mathf.Max(0f, startDelay);
        loopDelay = Mathf.Max(0f, loopDelay);
    }

    #endregion
}
