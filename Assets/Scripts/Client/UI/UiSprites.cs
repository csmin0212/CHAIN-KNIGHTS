using System.Collections.Generic;
using UnityEngine;

namespace ChainKnights.Client.UI
{
    /// <summary>
    /// 그림 에셋이 오기 전까지 쓰는 기본 도형 스프라이트. 실행할 때 텍스처를 직접 만든다.
    /// 둥근 사각형은 9-slice라서 어떤 크기로 늘려도 모서리가 유지된다.
    /// </summary>
    public static class UiSprites
    {
        private static readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();
        private static Sprite circle;

        public static Sprite Rounded(int radius)
        {
            radius = Mathf.Max(1, radius);
            if (rounded.TryGetValue(radius, out var sprite))
                return sprite;

            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float half = size / 2f;
            float inner = half - radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 둥근 사각형까지의 거리 (안쪽이면 음수). 경계 1픽셀은 부드럽게 처리한다.
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    byte alpha = (byte)(Mathf.Clamp01(0.5f - distance) * 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            float border = radius + 1;
            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            rounded[radius] = sprite;
            return sprite;
        }

        /// <summary>원. 코스트 보석, 원형 타이머(Filled)에 쓴다.</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle != null)
                    return circle;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                float half = size / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) - (half - 1);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - distance) * 255));
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
                return circle;
            }
        }
    }
}
