using UnityEngine;

public class OnDoorOpenedScript : StateMachineBehaviour 
{
     override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
     {
         animator.SetBool("Transitioning", false);
     }
}
