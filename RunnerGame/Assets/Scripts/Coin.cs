using System;
using UnityEngine;
using UnityEngine.UI;

public class Coin : MonoBehaviour
{
    public Pooler poolOrigin;
    static public Pooler coinPool;
    static public Pooler[] coinsPool;
    public bool isPremium = false;
    public CoinType coinType;
 
    // public AudioClip collectSound;

    public void Collect(CharacterInputController c)
    {
        switch (coinType)
        {
            case CoinType.Picanha:
                c.picanhas++;
                break;
            case CoinType.Cafe:
                c.cafe++;
                break;
            case CoinType.Chocolate:
                c.chocolates++;
                break;
            case CoinType.Cash:
                c.cash++;
                break;
            case CoinType.Gold:
                c.gold++;
                break;
            case CoinType.Premium:
                c.premium++;
                break;
            default:
                break;
            
        }
        
        // if (collectSound)
        // {
        //     c.powerupSource.clip = collectSound;
        //     c.powerupSource.Play();
        // }
        
    }
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.greenYellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
    #endif
}

public enum CoinType
{
    Picanha,
    Chocolate,
    Cash,
    Cafe,
    Coin,
    Gold,
    Premium
}