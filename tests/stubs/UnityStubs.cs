// Minimal stand-ins for the Unity APIs the view code uses. They exist only so the view code can be
// compiled with mono in CI-less environments to catch typos and type errors (not behaviour).
// The real build uses UnityEngine; keep these signatures aligned with the Unity manual.
#pragma warning disable 0649, 0169, 0414, 0067
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(); } } public static Vector2 one { get { return new Vector2(1, 1); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
        public static bool operator !=(Vector2 a, Vector2 b) { return a.x != b.x || a.y != b.y; }
        public static bool operator ==(Vector2 a, Vector2 b) { return a.x == b.x && a.y == b.y; }
        public override bool Equals(object o) { return true; } public override int GetHashCode() { return 0; }
        public static float Distance(Vector2 a, Vector2 b) { return 0; } }
    public struct Vector3 { public float x, y, z; public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0); } public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public struct Vector4 { public Vector4(float a, float b, float c, float d) { } }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1, 1, 1, 1); } } public static Color magenta { get { return new Color(1, 0, 1, 1); } } }
    public struct Color32 { public Color32(byte r, byte g, byte b, byte a) { } }
    public struct Rect { public Rect(float x, float y, float w, float h) { } public bool Contains(Vector2 p) { return true; } }
    public struct RectOffset_ { }
    public class RectOffset { public RectOffset(int l, int r, int t, int b) { } }
    public static class Mathf { public static float Max(float a, float b) { return Math.Max(a, b); } public static float Min(float a, float b) { return Math.Min(a, b); }
        public static float Clamp01(float a) { return a; } public static float Clamp(float a, float b, float c) { return a; } public static float Sqrt(float a) { return a; }
        public static int RoundToInt(float a) { return 0; } }
    public class Object { public static void Destroy(Object o) { } public static T FindObjectOfType<T>() where T : Object { return null; } public static implicit operator bool(Object o) { return o != null; }
        public string name; }
    public class Component : Object { public GameObject gameObject; public Transform transform; public T GetComponent<T>() { return default(T); } }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public class Transform : Component { public Vector3 position; public void SetParent(Transform p, bool worldPositionStays) { } public void SetParent(Transform p) { } }
    public class GameObject : Object { public GameObject(string n) { } public GameObject(string n, params Type[] t) { } public Transform transform; public string tag; public bool activeSelf;
        public T AddComponent<T>() where T : Component { return null; } public T GetComponent<T>() { return default(T); } public void SetActive(bool a) { } }
    public class Camera : Behaviour { public bool orthographic; public CameraClearFlags clearFlags; public Color backgroundColor; public float nearClipPlane, farClipPlane, orthographicSize; }
    public enum CameraClearFlags { SolidColor }
    public class Resources { public static T Load<T>(string p) where T : Object { return null; } public static T GetBuiltinResource<T>(string p) where T : Object { return null; } }
    public class TextAsset : Object { public string text; }
    public class Texture : Object { public FilterMode filterMode; public TextureWrapMode wrapMode; }
    public class Texture2D : Texture { public Texture2D(int w, int h, TextureFormat f, bool mip) { } public void SetPixels32(Color32[] p) { } public void Apply(bool a, bool b) { } }
    public enum TextureFormat { RGBA32 }
    public enum TextureWrapMode { Clamp }
    public enum FilterMode { Bilinear }
    public enum SpriteMeshType { FullRect }
    public class Sprite : Object { public static Sprite Create(Texture2D t, Rect r, Vector2 p, float ppu) { return null; }
        public static Sprite Create(Texture2D t, Rect r, Vector2 p, float ppu, uint e, SpriteMeshType m, Vector4 b) { return null; } }
    public class Shader : Object { public static Shader Find(string n) { return null; } }
    public class Material : Object { public Material(Material m) { } public Material(Shader s) { } public Texture mainTexture; }
    public class Renderer : Component { public Material sharedMaterial; public int sortingOrder; public bool enabled; }
    public class SpriteRenderer : Renderer { public Sprite sprite; public Color color; }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class MeshRenderer : Renderer { }
    public class Mesh : Object { public int vertexCount; public void SetVertices(List<Vector3> v) { } public void SetColors(List<Color> c) { } public void SetUVs(int ch, List<Vector2> u) { }
        public void SetTriangles(List<int> t, int s) { } public void RecalculateBounds() { } public void Clear() { } public void MarkDynamic() { } }
    public class LineRenderer : Renderer { public bool useWorldSpace, loop; public int positionCount, numCornerVertices; public float widthMultiplier; public Color startColor, endColor; public void SetPosition(int i, Vector3 p) { } }
    public static class Application { public static int targetFrameRate; public static bool isBatchMode; }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch { public Vector2 position; public TouchPhase phase; }
    public enum KeyCode { Return, KeypadEnter, Escape }
    public static class Input { public static bool simulateMouseWithTouches, multiTouchEnabled; public static int touchCount; public static Touch GetTouch(int i) { return new Touch(); }
        public static Vector2 mouseScrollDelta; public static Vector3 mousePosition; public static bool GetMouseButtonDown(int b) { return false; } public static bool GetMouseButtonUp(int b) { return false; }
        public static bool GetMouseButton(int b) { return false; } public static bool GetKeyDown(KeyCode k) { return false; } }
    public static class Screen { public static int width, height; }
    public static class Time { public static float deltaTime; public static double realtimeSinceStartupAsDouble; }
    public static class PlayerPrefs { public static void SetString(string k, string v) { } public static string GetString(string k, string d) { return d; } public static void Save() { } }
    public static class Debug { public static void LogError(object o) { } public static void Log(object o) { } }
    public class Font : Object { public static Font CreateDynamicFontFromOSFont(string n, int s) { return null; } }
    public static class ColorUtility { public static bool TryParseHtmlString(string s, out Color c) { c = new Color(); return true; } }
    public class CanvasGroup : Behaviour { public float alpha; public bool blocksRaycasts; }
    public enum RenderMode { ScreenSpaceOverlay }
    public class Canvas : Behaviour { public RenderMode renderMode; public int sortingOrder; }
    public class RectTransform : Transform { public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, offsetMin, offsetMax; }
    public enum TextAnchor { MiddleCenter, MiddleLeft }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum RuntimeInitializeLoadType { AfterSceneLoad }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
}

namespace UnityEngine.Events { public class UnityAction { } }

namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.Behaviour { public static EventSystem current; public void RaycastAll(PointerEventData d, List<RaycastResult> r) { } }
    public class StandaloneInputModule : UnityEngine.Behaviour { }
    public class PointerEventData { public PointerEventData(EventSystem e) { } public UnityEngine.Vector2 position; }
    public struct RaycastResult { }
}

namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Behaviour { public UnityEngine.Color color; public bool raycastTarget; public UnityEngine.RectTransform rectTransform; }
    public class Image : Graphic { public enum Type { Simple, Sliced } public UnityEngine.Sprite sprite; public Type type; }
    public class Text : Graphic { public UnityEngine.Font font; public int fontSize; public UnityEngine.TextAnchor alignment; public UnityEngine.HorizontalWrapMode horizontalOverflow;
        public UnityEngine.VerticalWrapMode verticalOverflow; public bool supportRichText, resizeTextForBestFit; public int resizeTextMinSize, resizeTextMaxSize; public string text; }
    public struct ColorBlock { public UnityEngine.Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor; }
    public struct Navigation { public enum Mode { None } public Mode mode; }
    public class Selectable : UnityEngine.Behaviour { public ColorBlock colors; public Navigation navigation; public bool interactable; public Graphic targetGraphic; }
    public class Button : Selectable { public class ButtonClickedEvent { public void AddListener(Action a) { } } public ButtonClickedEvent onClick; }
    public class LayoutElement : UnityEngine.Behaviour { public float minWidth, minHeight, preferredWidth, preferredHeight, flexibleWidth, flexibleHeight; }
    public class HorizontalOrVerticalLayoutGroup : UnityEngine.Behaviour { public UnityEngine.RectOffset padding; public float spacing; public bool childControlWidth, childControlHeight, childForceExpandWidth, childForceExpandHeight; }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class CanvasScaler : UnityEngine.Behaviour { public enum ScaleMode { ScaleWithScreenSize } public enum ScreenMatchMode { Expand } public ScaleMode uiScaleMode; public UnityEngine.Vector2 referenceResolution; public ScreenMatchMode screenMatchMode; }
    public class GraphicRaycaster : UnityEngine.Behaviour { }
    public class Outline : UnityEngine.Behaviour { public UnityEngine.Color effectColor; public UnityEngine.Vector2 effectDistance; }
}
