using UnityEngine;

public class SlashVfx : MonoBehaviour
{
    float _duration = 0.4f;
    float _timer;
    Color _color;
    MeshFilter _mf;
    MeshRenderer _mr;
    Color[] _baseColors;
    bool _glitchMode;

    public void Init(float angle, float range, Vector2 dir, Color color, float duration = 0.4f, bool glitch = false)
    {
        _color = color;
        _duration = duration;
        _glitchMode = glitch;

        _mf = gameObject.AddComponent<MeshFilter>();
        _mr = gameObject.AddComponent<MeshRenderer>();
        _mr.material = new Material(Shader.Find("Sprites/Default"));
        _mr.sortingOrder = 10;

        BuildArcMesh(angle, range, dir);
    }

    void BuildArcMesh(float angle, float range, Vector2 dir)
    {
        int segments = 20;
        float halfAngle = angle / 2f;

        var mesh = new Mesh();
        var vertices = new Vector3[segments + 2];
        var triangles = new int[segments * 3];
        _baseColors = new Color[segments + 2];

        Color initColor = _glitchMode ? Color.white : _color;

        vertices[0] = Vector3.zero;
        _baseColors[0] = new Color(initColor.r, initColor.g, initColor.b, 0.6f);

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float a = Mathf.Lerp(halfAngle, -halfAngle, t);
            Vector3 arcDir = Quaternion.Euler(0, 0, a) * (Vector3)dir;
            vertices[i + 1] = arcDir * range;
            _baseColors[i + 1] = new Color(initColor.r, initColor.g, initColor.b, 0.2f);
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = _baseColors;
        mesh.RecalculateNormals();
        _mf.mesh = mesh;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float alpha = Mathf.Lerp(1f, 0f, _timer / _duration);

        var colors = new Color[_baseColors.Length];

        if (_glitchMode)
        {
            bool visible = Mathf.Sin(Time.time * 35f) > 0f;

            if (visible)
            {
                colors[0] = new Color(1f, 1f, 1f, 0.9f * alpha);
                for (int i = 1; i < colors.Length; i++)
                    colors[i] = new Color(1f, 1f, 1f, 0.4f * alpha);
            }
            else
            {
                colors[0] = new Color(0f, 0f, 0f, 0.95f * alpha);
                for (int i = 1; i < colors.Length; i++)
                    colors[i] = new Color(0f, 0f, 0f, 0.7f * alpha);
            }
        }
        else
        {
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color(_baseColors[i].r, _baseColors[i].g, _baseColors[i].b, _baseColors[i].a * alpha);
        }

        _mf.mesh.colors = colors;
        if (_timer >= _duration) Destroy(gameObject);
    }
}