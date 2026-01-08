using UnityEngine;

public class CloseTransition : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Check if animation is finished
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            
            // If animation has finished playing (normalized time >= 1)
            if (stateInfo.normalizedTime >= 1.0f && !animator.IsInTransition(0))
            {
                gameObject.SetActive(false);
            }
        }
    }
}
