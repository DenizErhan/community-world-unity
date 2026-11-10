// Imports every PNG under Resources/Art as a crisp sprite with the right pivot, taken from the
// design tokens (grid.* in tokens.json). No hand-written .meta files are needed.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CommunityWorld.EditorTools
{
    public class ArtImporter : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Resources/Art/";
        private const string TokensPath = "Assets/Resources/design/tokens.json";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            JObj tokens = MiniJson.ParseObject(File.ReadAllText(TokensPath));
            JObj grid = J.O(tokens, "grid");
            float scale = (float)J.N(grid, "raster_scale", 3);
            float ppu = Gfx.Unit * scale;
            Vector2 pivot = new Vector2(0.5f, 0.5f);

            if (assetPath.StartsWith(ArtRoot + "buildings/"))
            {
                System.Collections.Generic.List<object> canvas = J.A(grid, "sprite_canvas");
                System.Collections.Generic.List<object> anchor = J.A(grid, "sprite_anchor");
                pivot = new Vector2((float)(J.Num(anchor[0]) / J.Num(canvas[0])), 1f - (float)(J.Num(anchor[1]) / J.Num(canvas[1])));
            }
            else if (assetPath.StartsWith(ArtRoot + "ground/"))
            {
                System.Collections.Generic.List<object> canvas = J.A(grid, "tile_canvas");
                System.Collections.Generic.List<object> anchor = J.A(grid, "tile_anchor");
                pivot = new Vector2((float)(J.Num(anchor[0]) / J.Num(canvas[0])), 1f - (float)(J.Num(anchor[1]) / J.Num(canvas[1])));
            }
            else
            {
                ppu = 100f;   // UI icons are sized in code
            }

            TextureImporter ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Trilinear;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaIsTransparency = true;
            ti.isReadable = false;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            TextureImporterSettings s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spritePixelsPerUnit = ppu;
            ti.SetTextureSettings(s);
        }
    }
}
