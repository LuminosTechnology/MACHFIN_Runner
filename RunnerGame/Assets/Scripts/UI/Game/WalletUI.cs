using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class WalletUI : MonoBehaviour
{
    public GameObject coinPrefab;

    private Dictionary<CoinType, Transform> _coinsUI = new();

    private CharacterInputController _controller;
    
    #region Singleton

    public static WalletUI instance;
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #endregion

    void Start()
    {
        PopulateUI();
    }
    
    public void StartRun(CharacterInputController controller)
    {
        _controller = controller;
    }

    void PopulateUI()
    {
        foreach (CoinRef coin in System.Enum.GetValues(typeof(CoinRef)))
        {
            var itm = Instantiate(coinPrefab, transform);
            itm.GetComponentInChildren<Image>().sprite = coin.icon;
            
            _coinsUI.Add(coin.coin, itm.transform);

        }
    }

    void RefreshUI()
    {
        
    }

    void UpdateCoin(CoinType coinType)
    {
        if (!_controller)
        {
            Debug.LogError("Controller not found on player");
            return;
        }
        switch (coinType)
        {
            
            case CoinType.Picanha:
                
                    
                break;
                
        }
    }
    
    
    
    
}
