using UnityEngine;
using UnityEngine.UI;

public class MissionEntry : MonoBehaviour
{
    public Text descText;
    public Text rewardText;
    public LayoutElement spaceElement;
    public Button claimButton;
    public Text progressText;
	public Image background;

	public Color notCompletedColor;
	public Color completedColor;

    public void FillWithMission(MissionBase m, MissionUI owner)
    {
        descText.text = m.GetMissionDesc();
        rewardText.text = m.reward.ToString();

        if (m.isComplete)
        {
            claimButton.gameObject.SetActive(true);
            // progressText.gameObject.SetActive(false);
            
            progressText.text = ((int)m.max) + " / " + ((int)m.max);
            progressText.fontSize = 28;
            progressText.resizeTextMaxSize = 28;
            spaceElement.flexibleWidth = 1;

			background.color = completedColor;

			progressText.color = Color.white;
			// descText.color = Color.white;
			rewardText.color = Color.white;

			claimButton.onClick.AddListener(delegate { owner.Claim(m); } );
        }
        else
        {
            claimButton.gameObject.SetActive(false);
            progressText.gameObject.SetActive(true);

			background.color = notCompletedColor;

			progressText.color = Color.white;
			rewardText.color = Color.white;
			descText.color = completedColor;

			progressText.text = ((int)m.progress) + " / " + ((int)m.max);
        }
    }
}
