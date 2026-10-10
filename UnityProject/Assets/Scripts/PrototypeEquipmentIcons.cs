using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Small transparent silhouettes shared by UI Toolkit and the battle HUD.
    public static class PrototypeEquipmentIcons
    {
        private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();
        private static readonly Color Ink = new Color(.81f, .89f, .87f, 1f);

        public static Texture2D Weapon(PrototypeWeaponId id) => Get((int)id, texture => DrawWeapon(texture, id));
        public static Texture2D Gadget(PrototypeInfantryClass role) => Get(20 + (int)role, texture => DrawGadget(texture, role));
        public static Texture2D Throwable(PrototypeThrowableId id) => Get(30 + (int)id, texture => DrawThrowable(texture, id));
        public static Texture2D Rocket() => Get(40, texture =>
        {
            Bar(texture, 21, 15, 51, 5); Bar(texture, 32, 20, 22, 3);
            Bar(texture, 33, 9, 5, 6); Bar(texture, 70, 13, 8, 9);
        });

        private static Texture2D Get(int key, System.Action<Texture2D> draw)
        {
            if (Cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            var texture = new Texture2D(96, 32, TextureFormat.RGBA32, false)
            {
                name = "IRONFRONT Equipment Icon " + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var clear = new Color[96 * 32];
            texture.SetPixels(clear);
            draw(texture);
            texture.Apply();
            Cache[key] = texture;
            return texture;
        }

        private static void Bar(Texture2D image, int x, int y, int w, int h)
        {
            for (int py = Mathf.Max(y, 0); py < Mathf.Min(y + h, image.height); py++)
                for (int px = Mathf.Max(x, 0); px < Mathf.Min(x + w, image.width); px++)
                    image.SetPixel(px, py, Ink);
        }

        private static void DrawWeapon(Texture2D image, PrototypeWeaponId id)
        {
            if (id == PrototypeWeaponId.Pistol)
            {
                Bar(image, 24, 18, 43, 5); Bar(image, 28, 16, 31, 3);
                Bar(image, 48, 6, 7, 12); Bar(image, 21, 18, 4, 3);
                return;
            }
            int barrel = id == PrototypeWeaponId.Sniper ? 30 : id == PrototypeWeaponId.Smg ? 12 : 20;
            int body = id == PrototypeWeaponId.Lmg ? 36 : 30;
            Bar(image, 24, 15, body, id == PrototypeWeaponId.Lmg ? 8 : 6);
            Bar(image, 24 + body, 18, barrel, 3);
            Bar(image, 13, 16, 12, 3); Bar(image, 9, 12, 5, 8);
            Bar(image, 29, 8, 5, 7); Bar(image, 43, 8, 6, 7);
            if (id == PrototypeWeaponId.Marksman || id == PrototypeWeaponId.Sniper)
                Bar(image, 36, 24, 17, 3);
            else Bar(image, 35, 23, 10, 3);
            Bar(image, 42, 7, 5, 8);
            if (id == PrototypeWeaponId.Lmg) Bar(image, 49, 7, 9, 9);
            if (id == PrototypeWeaponId.Sniper) Bar(image, 66, 11, 2, 7);
        }

        private static void DrawGadget(Texture2D image, PrototypeInfantryClass role)
        {
            Bar(image, 33, 5, 30, 21); Bar(image, 38, 26, 20, 3);
            Color dark = new Color(.08f, .19f, .22f, 1f);
            for (int y = 11; y < 20; y++) for (int x = 45; x < 51; x++) image.SetPixel(x, y, dark);
            for (int x = 41; x < 55; x++) for (int y = 14; y < 17; y++) image.SetPixel(x, y, dark);
            if (role == PrototypeInfantryClass.Engineer) Bar(image, 61, 12, 10, 3);
            if (role == PrototypeInfantryClass.Recon) Bar(image, 46, 29, 3, 3);
        }

        private static void DrawThrowable(Texture2D image, PrototypeThrowableId id)
        {
            Bar(image, 43, 8, 15, 18); Bar(image, 46, 26, 9, 4);
            Bar(image, 52, 29, 12, 2); Bar(image, 62, 25, 2, 6);
            if (id == PrototypeThrowableId.Smoke) Bar(image, 38, 11, 5, 13);
        }
    }
}
