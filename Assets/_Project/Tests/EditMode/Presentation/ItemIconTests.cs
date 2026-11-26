using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Space.Core.Equipment;
using Space.Presentation.UI;
using UnityEngine;

namespace Space.Presentation.Tests
{
    public class ItemIconTests
    {
        /// <summary>Where the preview images go: the CI uploads the artifacts folder.</summary>
        static string PreviewFolder => Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "icons");

        [Test]
        public void EveryItemHasADistinctVisibleIcon()
        {
            var signatures = new Dictionary<string, string>();
            foreach (var item in ItemCatalog.All)
            {
                var pixels = ItemIcons.Paint(item).ToColor32();
                Assert.AreEqual(ItemIcons.Size * ItemIcons.Size, pixels.Length);

                // The glyph covers a fair part of the tile, in more than the background colours.
                var bright = pixels.Count(p => p.a > 200 && (p.r + p.g + p.b) > 220);
                Assert.Greater(bright, pixels.Length / 40, $"{item.Id} : icône presque vide");

                var signature = string.Join(",", pixels.Where((_, i) => i % 37 == 0).Select(p => p.r + p.g * 3 + p.b * 7));
                Assert.IsFalse(signatures.ContainsKey(signature), $"{item.Id} a la même icône que {signatures.GetValueOrDefault(signature)}");
                signatures[signature] = item.Id;
            }
        }

        [Test]
        public void WritePreviewSheet()
        {
            // One row per category, eight icons per row, with a gap: easy to review in the CI artifacts.
            const int gap = 8;
            var size = ItemIcons.Size;
            var columns = 8;
            var rows = ItemCategories.Count;
            var width = columns * (size + gap) + gap;
            var height = rows * (size + gap) + gap;
            var sheet = new Color32[width * height];
            for (var i = 0; i < sheet.Length; i++)
                sheet[i] = new Color32(18, 20, 28, 255);

            Directory.CreateDirectory(PreviewFolder);
            for (var c = 0; c < rows; c++)
            {
                var items = ItemCatalog.InCategory((ItemCategory)c).ToList();
                for (var k = 0; k < items.Count && k < columns; k++)
                {
                    var canvas = ItemIcons.Paint(items[k]);
                    var pixels = canvas.ToColor32();
                    // Row 0 at the top of the image (textures are bottom-up).
                    var ox = gap + k * (size + gap);
                    var oy = height - (gap + c * (size + gap)) - size;
                    for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var p = pixels[y * size + x];
                        ref var dst = ref sheet[(oy + y) * width + ox + x];
                        var a = p.a / 255f;
                        dst = new Color32((byte)(p.r * a + dst.r * (1 - a)), (byte)(p.g * a + dst.g * (1 - a)), (byte)(p.b * a + dst.b * (1 - a)), 255);
                    }
                    WritePng(Path.Combine(PreviewFolder, items[k].Id + ".png"), pixels, size, size);
                }
            }
            WritePng(Path.Combine(PreviewFolder, "_planche.png"), sheet, width, height);
            Assert.IsTrue(File.Exists(Path.Combine(PreviewFolder, "_planche.png")));
        }

        static void WritePng(string path, Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void IconsAreCachedAsTextures()
        {
            var item = ItemCatalog.Get(ItemCatalog.StarterShield);
            var a = ItemIcons.Get(item);
            var b = ItemIcons.Get(item);
            Assert.IsNotNull(a);
            Assert.AreSame(a, b);
            Assert.AreEqual(ItemIcons.Size, a.width);
        }
    }
}
