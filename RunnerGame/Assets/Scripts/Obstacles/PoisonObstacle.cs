using UnityEngine;

public class PoisonObstacle : SimpleBarricade
{
    
    public override void Impacted()
    {
        CharacterInputController inputController = GameObject.FindGameObjectWithTag("Player").gameObject
            .GetComponent<CharacterInputController>();

        // inputController.directionMultiplier = -1;
        
        inputController.HitPoison();
        
        base.Impacted();
        
    }
    
    
}
