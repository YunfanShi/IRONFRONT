using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace Ironfront.UnityPrototype.Editor
{
    // Explicit font faces avoid platform-dependent CJK fallback and synthetic bold.
    public static class PrototypeLobbyFontBuilder
    {
        private const string Source = "Assets/Fonts/Lobby/";
        private const string Output = "Assets/Resources/Menu/Fonts/";
        private const string Copy = "联合特遣队准备行动工业前线开始游戏选择作战联机房间大厅配装设置单人对抗可离线游玩征服五据点攻防逐点进攻铁锋测试玩家→←·";

        [MenuItem("IRONFRONT/Generate Lobby Fonts")]
        public static void Generate()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            string glyphs = new string(Enumerable.Range(32, 95).Select(i => (char)i).ToArray()) + Copy;
            GenerateFace("SourceHanSansCN-Regular.otf", "LobbyRegular", glyphs);
            GenerateFace("SourceHanSansCN-Medium.otf", "LobbyMedium", glyphs);
            GenerateFace("SourceHanSansCN-Heavy.otf", "LobbyHeavy", glyphs);
            GenerateFace("BlackOpsOne-Regular.ttf", "LobbyStencil", "IRONFRONT ");
            // Include licenses in the standalone application as well as the source project.
            const string licenses = "Assets/StreamingAssets/ThirdPartyFonts/";
            Directory.CreateDirectory(licenses);
            foreach (string file in new[] { "SourceHanSans-LICENSE.txt", "BlackOpsOne-OFL.txt" })
                File.Copy(Source + file, licenses + file, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("IRONFRONT lobby fonts PASS: four embedded faces, required glyphs and redistributable licenses.");
        }

        private static void GenerateFace(string source, string name, string glyphs)
        {
            string path = Output + name + ".asset";
            FontAsset asset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (asset == null)
            {
                Font font = AssetDatabase.LoadAssetAtPath<Font>(Source + source);
                if (font == null) throw new InvalidOperationException("Missing font: " + source);
                asset = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (asset == null) throw new InvalidOperationException("Could not create " + name);
                asset.name = name;
                AssetDatabase.CreateAsset(asset, path);
                asset.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            if (!asset.TryAddCharacters(glyphs, out string missing))
                throw new InvalidOperationException(name + " is missing glyphs: " + missing);
            foreach (Texture2D atlas in asset.atlasTextures)
            {
                if (atlas == null) continue;
                if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
                EditorUtility.SetDirty(atlas);
            }
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.material);
            Debug.Log(name + ": " + asset.faceInfo.familyName + " / " + asset.faceInfo.styleName +
                ", " + glyphs.Distinct().Count() + " required glyphs, " + asset.atlasTextures.Length + " atlas(es)");
        }
    }
}
