using UnityEngine;

public class OrbVfx : MonoBehaviour
{
    SpriteRenderer _sr;
    Color _color;

    public void Init(float radius, Color color, float duration = 0f)
    {
        _color = color;

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = CreateCircleSprite();
        _sr.color = color;
        _sr.sortingOrder = 10;

        SetSize(radius);
    }

    public void SetSize(float radius)
    {
        transform.localScale = Vector3.one * radius * 2f;
    }

    public void SetColor(Color color)
    {
        _color = color;
        if (_sr != null) _sr.color = color;
    }

    public static Sprite GetCircleSprite() => CreateCircleSprite();

    static Sprite CreateCircleSprite()
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new(size / 2f, size / 2f);
        float outerR = size / 2f;
        float innerR = outerR * 0.4f;

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), center);

            if (dist > outerR)
                tex.SetPixel(x, y, Color.clear);
            else if (dist < innerR)
            {
                float t = dist / innerR;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0.3f, t)));
            }
            else
            {
                float t = (dist - innerR) / (outerR - innerR);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Lerp(0.6f, 0f, t)));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}