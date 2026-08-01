using System.IO;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public static class RuntimeCardTextureLoader
    {
        public static Texture2D Load(string path, string textureName)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var source = NewTexture(textureName);
            byte[] bytes = File.ReadAllBytes(path);
            int maximumWidth = ArcaneGraphicsPreferences.CardTextureWidth;
            bool resize = maximumWidth > 0;
            if (!source.LoadImage(bytes, !resize))
            {
                Object.Destroy(source);
                return null;
            }

            if (!resize)
                return source;

            if (source.width <= maximumWidth)
            {
                source.Apply(false, true);
                return source;
            }

            float scale = maximumWidth / (float)source.width;
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            Texture2D resized = Resize(source, maximumWidth, height, textureName);
            Object.Destroy(source);
            return resized;
        }

        private static Texture2D Resize(
            Texture2D source,
            int width,
            int height,
            string textureName)
        {
            RenderTexture target = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                Texture2D result = NewTexture(textureName);
                result.Reinitialize(width, height, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                result.Apply(false, true);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static Texture2D NewTexture(string textureName)
        {
            return new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = textureName ?? "CardArt",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }
    }
}
