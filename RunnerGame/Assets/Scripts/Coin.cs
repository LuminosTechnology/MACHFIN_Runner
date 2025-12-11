using System;
using UnityEngine;

public class Coin : MonoBehaviour
{
    public Pooler poolOrigin;
    static public Pooler coinPool;
    static public Pooler[] coinsPool;
    public bool isPremium = false;
    public CoinType coinType;
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.greenYellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
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