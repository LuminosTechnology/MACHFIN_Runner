using UnityEngine;
using UnityEngine.UI;

public class ShopItemListItem : MonoBehaviour
{
    public Image icon;
    public Text nameText;
    public Text pricetext;
	public Text premiumText;
    public Button buyButton;

	public Text countText;

	public Sprite buyButtonSprite;
	public Sprite disabledButtonSprite;

	[Space] public GameObject PriceCoinPrefab;
	public Transform PriceZone;

	public void PopulateUI(CoinPrice[] coins, ConsumableDatabase db)
	{
		foreach (var c in coins)
		{
			var priceCoin = Instantiate(PriceCoinPrefab, PriceZone);

			var _image = priceCoin.transform.GetChild(0).GetComponent<Image>();
			var _amount = priceCoin.transform.GetChild(1).GetComponent<Text>();

			System.Console.WriteLine($"[ShopItemListItem.PopulateUI Line 29] {c.amount}");
			
			_image.sprite = db.GetCoinRef(c.coinType).icon;
			_amount.text = c.amount.ToString();
		}
	}
}
