// Small graphics helpers: colors from design tokens, runtime-generated sprites (so no extra
// art files are needed for UI shapes), shared font and material, design-space -> world units.
using UnityEngine;

namespace CommunityWorld
{
    public static class Gfx
    {
        /// <summary>World units per design pixel is 1/100 (a 128 px tile is 1.28 units wide).</summary>
        public const float Unit = 100f;

        public static Vector3 ToWorld(Vec2 p, float z = 0f)
        {
            return new Vector3((float)p.X / Unit, -(float)p.Y / Unit, z);
        }

        public static Vec2 ToDesign(Vector3 w)
        {
            return new Vec2(w.x * Unit, -w.y * Unit);
        }

        // ------------------------------------------------------------ colors

        public static Color Hex(string s, Color def)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(s, out c) ? c : def;
        }

        public static Color Hex(string s) { return Hex(s, Color.magenta); }

        public static Color WithAlpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

        /// <summary>Same math as Godot's Color.darkened.</summary>
        public static Color Darkened(Color c, float a) { return new Color(c.r * (1f - a), c.g * (1f - a), c.b * (1f - a), c.a); }

        /// <summary>Same math as Godot's Color.lightened.</summary>
        public static Color Lightened(Color c, float a) { return new Color(c.r + (1f - c.r) * a, c.g + (1f - c.g) * a, c.b + (1f - c.b) * a, c.a); }

        public static Color Multiply(Color a, Color b) { return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a); }

        // ------------------------------------------------------------ shared font and material

        private static Font font;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return font;
            }
        }

        private static Material spriteMat;

        /// <summary>The unlit sprite material (supports vertex colors). Taken from a SpriteRenderer so it is always in the build.</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMat == null)
                {
                    GameObject tmp = new GameObject("tmp");
                    SpriteRenderer sr = tmp.AddComponent<SpriteRenderer>();
                    spriteMat = sr.sharedMaterial;
                    Object.Destroy(tmp);
                    if (spriteMat == null) spriteMat = new Material(Shader.Find("Sprites/Default"));
                }
                return spriteMat;
            }
        }

        // ------------------------------------------------------------ generated sprites

        private static Texture2D MakeTexture(int w, int h, System.Func<float, float, float> alphaAt)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(alphaAt(x + 0.5f, y + 0.5f));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D circleTex;

        /// <summary>A soft white disc (64 px). Its centre is solid white, so it also serves as a "solid color" texture.</summary>
        public static Texture2D CircleTexture
        {
            get
            {
                if (circleTex == null)
                {
                    const int n = 64;
                    circleTex = MakeTexture(n, n, (x, y) =>
                    {
                        float d = Mathf.Sqrt((x - n * 0.5f) * (x - n * 0.5f) + (y - n * 0.5f) * (y - n * 0.5f));
                        return n * 0.5f - d;
                    });
                }
                return circleTex;
            }
        }

        private static Sprite circleSprite;

        public static Sprite CircleSprite
        {
            get
            {
                if (circleSprite == null)
                    circleSprite = Sprite.Create(CircleTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
                return circleSprite;
            }
        }

        /// <summary>A ring: outer radius = half the size, thickness as a fraction of the size.</summary>
        public static Sprite Ring(int size, float thickness)
        {
            Texture2D tex = MakeTexture(size, size, (x, y) =>
            {
                float d = Mathf.Sqrt((x - size * 0.5f) * (x - size * 0.5f) + (y - size * 0.5f) * (y - size * 0.5f));
                float outer = size * 0.5f - d;
                float inner = d - (size * 0.5f - thickness);
                return Mathf.Min(outer, inner);
            });
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite rounded;

        /// <summary>A 9-slice rounded rectangle (use with Image.type = Sliced). Corner radius in pixels.</summary>
        public static Sprite RoundedRect(int radius)
        {
            if (rounded != null) return rounded;
            int n = radius * 2 + 4;
            Texture2D tex = MakeTexture(n, n, (x, y) =>
            {
                float cx = Mathf.Clamp(x, radius, n - radius);
                float cy = Mathf.Clamp(y, radius, n - radius);
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                return radius - d + 0.5f;
            });
            rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
            return rounded;
        }

        // ------------------------------------------------------------ art lookup

        public static Sprite Art(string path)
        {
            Sprite s = Resources.Load<Sprite>("Art/" + path);
            if (s == null) Debug.LogError("Missing art: " + path);
            return s;
        }
    }
}
