using UnityEngine;

public class Coin : MonoBehaviour
{
    public Pooler poolOrigin;
	static public Pooler coinPool;
    static public Pooler[] coinsPool;
    public bool isPremium = false;
    public CoinType coinType;
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
