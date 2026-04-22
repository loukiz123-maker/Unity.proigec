using UnityEngine;
using TMPro;

public class GlitchOverlay : MonoBehaviour
{
    public GameObject glitchCanvas;
    public TextMeshProUGUI glitchText;

    // —четчик дронов, которые вид€т игрока
    private int dronesWatchingMe = 0;

    public void SetGlitch(bool active)
    {
        if (active) dronesWatchingMe++;
        else dronesWatchingMe--;

        // ќграничиваем, чтобы счетчик не ушел в минус
        if (dronesWatchingMe < 0) dronesWatchingMe = 0;

        // ¬ключаем эффект, если хот€ бы один дрон нас видит
        bool shouldBeVisible = dronesWatchingMe > 0;

        if (glitchCanvas.activeSelf != shouldBeVisible)
        {
            glitchCanvas.SetActive(shouldBeVisible);
            if (shouldBeVisible) InvokeRepeating("GenerateScrapCode", 0f, 0.1f);
            else CancelInvoke("GenerateScrapCode");
        }
    }

    void GenerateScrapCode()
    {
        glitchText.rectTransform.localPosition = new Vector3(Random.Range(-5, 5), Random.Range(-5, 5), 0);
    }
}