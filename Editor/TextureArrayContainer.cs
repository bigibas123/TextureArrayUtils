using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace cc.dingemans.bigibas123.texturearrayutils
{
    [CreateAssetMenu(fileName = "Texture Array Container", menuName = "Bigi/Texture Array Container", order = 3)]
    public class TextureArrayContainer : ScriptableObject
    {
        [SerializeField] public FilterMode filterMode;
        [SerializeField] public TextureWrapMode wrapMode;
        [SerializeField] public List<Texture2D> textures;
        [SerializeField] public TextureCreationFlags flags;

        public int Width => textures?.Where(t => t != null).Select(t => t.width).Max() ?? 0;
        public int Height => textures?.Where(t => t != null).Select(t => t.height).Max() ?? 0;
        public int Depth => textures?.Count(p => { return p != null; }) ?? 0;

        public int MipCount => textures?.Where(t => t != null).Select(t => t.mipmapCount).Max() ?? 0;

        public GraphicsFormat GraphicsFormat => (textures[0] != null ? textures[0]?.graphicsFormat : null) ?? GraphicsFormat.None;

        public override string ToString()
        {
            return base.ToString() + AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
        }
        public Texture2DArray ToArray()
        {
            if (!VerifySettings())
            {
                return null;
            }

            Texture2DArray array = new Texture2DArray(Width, Height, Depth, GraphicsFormat, flags, MipCount)
            {
                filterMode = filterMode,
                wrapMode = wrapMode,
            };


            TextureImporterSettings[] oldSettings = MakeTexturesReadable();

            int curTexNumber = 0;
            for (int texIdx = 0; texIdx < textures.Count; texIdx++)
            {
                var tex = textures[texIdx];
                if (tex is null || tex == null) { continue; }
                Debug.Log("Processing #"+curTexNumber+": " + tex + " gformat:" + tex.graphicsFormat + " tformat:" + tex.format + " " + tex.width + "x" + tex.height + " mips:"+tex.mipmapCount);
                for (int mipMapLevel = 0; mipMapLevel < array.mipmapCount; mipMapLevel++)
                {
                    Graphics.CopyTexture(tex, 0, mipMapLevel, array, curTexNumber, mipMapLevel);
                }

                curTexNumber++;
            }

            array.Apply(true, true);

            RestoreTexturesSettings(oldSettings);
            return array;
        }
        private bool VerifySettings()
        {

            if (Depth <= 0)
            {
                Debug.LogError("Attempted generation of empty TextureArray: " + this);
                return false;
            }
            if (!SystemInfo.IsFormatSupported(GraphicsFormat, FormatUsage.Sample))
            {
                Debug.LogError("Attempted generation of TextureArray with wrong graphicsFormat, please use a different one: " + this);
                return false;
            }


            for (int texIdx = 0; texIdx < textures.Count; texIdx++)
            {
                var tex = textures[texIdx];
                if (tex is null || tex == null) { continue; }

                if (tex.width == Width && tex.height == Height) { continue; }
                Debug.LogError($"Texture: {tex.name} not the right size or format: ({tex.width}x{tex.height}@{tex.graphicsFormat}) array: {Width}x{Height}@{GraphicsFormat})");
                return false;
            }

            flags &= (~TextureCreationFlags.Crunch);
            return true;
        }
        private void RestoreTexturesSettings(IReadOnlyList<TextureImporterSettings> oldSettings)
        {
            try
            {
                AssetDatabase.DisallowAutoRefresh();

                for (int texIdx = 0; texIdx < textures.Count; texIdx++)
                {
                    if (oldSettings[texIdx] == null) { continue; }
                    var tex = textures[texIdx];
                    string path = AssetDatabase.GetAssetPath(tex);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.SetTextureSettings(oldSettings[texIdx]);
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
            }
        }
        private TextureImporterSettings[] MakeTexturesReadable()
        {
            TextureImporterSettings[] oldSettings = new TextureImporterSettings[textures.Count];
            try
            {
                AssetDatabase.DisallowAutoRefresh();

                for (int texIdx = 0; texIdx < textures.Count; texIdx++)
                {
                    var tex = textures[texIdx];
                    if (tex is null || tex == null) { continue; }

                    string path = AssetDatabase.GetAssetPath(tex);
                    var ip = AssetImporter.GetAtPath(path);
                    var importer = ip as TextureImporter;
                    if ((!(importer is null)) && importer != null)
                    {
                        oldSettings[texIdx] = new TextureImporterSettings();
                        importer.ReadTextureSettings(oldSettings[texIdx]);
                        
                        importer.isReadable = true;
                        importer.maxTextureSize = Math.Max(Width, Height);

                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();

                    }
                    else
                    {
                        Debug.LogWarning("Not recognized textureImporter: " + ip);
                    }
                }


            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
            }
            return oldSettings;
        }

        public void SaveToFile()
        {
            string path = AssetDatabase.GetAssetPath(this);
            string destPath = path.Replace(".asset", "TC.asset");
            //string tmpPath = path.Replace(".asset", "_TC.tmp.asset");
            var arr = ToArray();
            if (!(arr is null) && arr != null)
            {
                var deps = GetDependants(destPath);
                Debug.Log($"Created array: {arr}");
                AssetDatabase.CreateAsset(arr, destPath);
                //FileUtil.ReplaceFile(tmpPath, destPath);
                //FileUtil.DeleteFileOrDirectory(tmpPath);
                //AssetDatabase.DeleteAsset(tmpPath);
                EditorUtility.SetDirty(arr);
                AssetDatabase.ImportAsset(destPath);
                Debug.Log($"Replaced asset at {destPath}");
                foreach (var p in deps)
                {
                    Debug.Log($"Re importing asset at: {p}");
                    AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
                }
            }
        }

        private string[] GetDependants(string us)
        {
            List<string> result = new List<string>();
            try
            {
                string[] allAssets = AssetDatabase.GetAllAssetPaths();
                foreach (var asset in allAssets)
                {
                    try
                    {
                        if (AssetDatabase.GetDependencies(asset, false).Contains(us))
                        {
                            result.Add(asset);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e,this);
            }
            
            return result.ToArray();
        }
    }
}