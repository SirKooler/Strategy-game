using UnityEngine;

/// <summary>
/// Placeholder health bar. Replace the sprites on this prefab when real art exists.
/// Fill sprites use a left-center pivot so scaling X shrinks from the right.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [SerializeField] SpriteRenderer track;
    [SerializeField] SpriteRenderer fill;
    [SerializeField] Sprite friendlyFill;
    [SerializeField] Sprite enemyFill;

    /// <summary>
    /// Shows <paramref name="health"/> out of <paramref name="maxHealth"/>.
    /// Friendly units use the blue fill. Enemies use the red fill.
    /// </summary>
    public void Show(bool friendly, int health, int maxHealth)
    {
        if (fill == null)
            return;

        fill.sprite = friendly ? friendlyFill : enemyFill;
        fill.color = Color.white;
        float ratio = maxHealth <= 0 ? 0f : Mathf.Clamp01(health / (float)maxHealth);
        Vector3 scale = fill.transform.localScale;
        scale.x = ratio;
        fill.transform.localScale = scale;
        AlignFillToTrack();
    }

    void AlignFillToTrack()
    {
        if (track == null || track.sprite == null)
            return;

        float width = track.sprite.rect.width / track.sprite.pixelsPerUnit;
        float left = track.transform.localPosition.x - width * track.transform.localScale.x * 0.5f;
        Vector3 position = fill.transform.localPosition;
        position.x = left;
        fill.transform.localPosition = position;
    }
}
