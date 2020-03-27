﻿using HavenSoft.HexManiac.Core.Models.Runs.Compressed;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public interface ISpriteRun : IFormattedRun {
      int Pages { get; }
      int[,] GetPixels(IDataModel model, int page);
      ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels);
   }

   public interface IPaletteRun : IFormattedRun {
      int Pages { get; }
      IReadOnlyList<short> GetPalette(IDataModel model, int page);
      IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> colors);
   }

   public class SpriteRun : BaseRun, ISpriteRun {
      private readonly int bitsPerPixel, tileWidth, tileHeight;

      public int Pages => 1;
      public override int Length { get; }

      public override string FormatString { get; }

      public SpriteRun(int start, int bitsPerPixel, int tileWidth, int tileHeight, IReadOnlyList<int> sources = null) : base(start, sources) {
         this.bitsPerPixel = bitsPerPixel;
         this.tileWidth = tileWidth;
         this.tileHeight = tileHeight;
         Length = tileWidth * tileHeight * bitsPerPixel * 8;
         FormatString = $"`ucs{bitsPerPixel}x{tileWidth}x{tileHeight}`";
      }

      public static bool TryParseSpriteFormat(string pointerFormat, out SpriteFormat spriteFormat) {
         spriteFormat = default;
         if (!pointerFormat.StartsWith("`ucs") || !pointerFormat.EndsWith("`")) return false;
         return Compressed.SpriteRun.TryParseDimensions(pointerFormat, out spriteFormat);
      }

      // not actually LZ, but it is uncompressed and acts much the same way.
      public override IDataFormat CreateDataFormat(IDataModel data, int index) => LzUncompressed.Instance; 

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new SpriteRun(Start, bitsPerPixel, tileWidth, tileHeight, newPointerSources);

      // TODO support values other than 4bpp
      public int[,] GetPixels(IDataModel model, int page) {
         var pageSize = 8 * bitsPerPixel * tileWidth * tileHeight;
         return GetPixels(model, Start + page * pageSize, tileWidth, tileHeight);
      }

      /// <summary>
      /// convert from raw values to palette-index values
      /// </summary>
      public static int[,] GetPixels(IReadOnlyList<byte> data, int start, int tileWidth, int tileHeight) {
         var result = new int[8 * tileWidth, 8 * tileHeight];
         for (int y = 0; y < tileHeight; y++) {
            int yOffset = y * 8;
            for (int x = 0; x < tileWidth; x++) {
               var tileStart = ((y * tileWidth) + x) * 32 + start;
               int xOffset = x * 8;
               for (int i = 0; i < 32; i++) {
                  int xx = i % 4; // ranges from 0 to 3
                  int yy = i / 4; // ranges from 0 to 7
                  byte raw = data[tileStart + i];
                  result[xOffset + xx * 2 + 0, yOffset + yy] = (raw & 0xF);
                  result[xOffset + xx * 2 + 1, yOffset + yy] = raw >> 4;
               }
            }
         }
         return result;
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         for (int y = 0; y < tileHeight; y++) {
            int yOffset = y * 8;
            for (int x = 0; x < tileWidth; x++) {
               var tileStart = ((y * tileWidth) + x) * 32 + Start;
               int xOffset = x * 8;
               for (int i = 0; i < 32; i++) {
                  int xx = i % 4;
                  int yy = i / 4;
                  var high = pixels[xOffset + xx + 0, yOffset + yy];
                  var low = pixels[xOffset + xx + 1, yOffset + yy];
                  var raw = ((high << 4) | low);
                  token.ChangeData(model, tileStart + i, (byte)raw);
               }
            }
         }
         return this;
      }
   }

   // TODO inline edit of palette based on RGB. Custom DataFormat PaletteColor
   public class PaletteRun : BaseRun, IPaletteRun {
      private readonly int bits;

      public int Pages => 1;
      public override int Length { get; }

      public override string FormatString { get; }

      public PaletteRun(int start, int bits, IReadOnlyList<int> sources = null) : base(start, sources) {
         this.bits = bits;
         Length = 2 * (int)Math.Pow(2, bits);
         FormatString = $"`ucp{bits}`";
      }

      public static bool TryParsePaletteFormat(string pointerFormat, out PaletteFormat paletteFormat) {
         paletteFormat = default;
         if (!pointerFormat.StartsWith("`ucp") || !pointerFormat.EndsWith("`")) return false;
         return Compressed.PaletteRun.TryParseDimensions(pointerFormat, out paletteFormat);
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => LzUncompressed.Instance;

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new PaletteRun(Start, bits, newPointerSources);

      public IReadOnlyList<short> GetPalette(IDataModel model, int page) {
         var paletteColorCount = (int)Math.Pow(2, bits);
         var pageLength = paletteColorCount * 2;
         return GetPalette(model, Start + page * pageLength, paletteColorCount);
      }

      public IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> data) {
         for (int i = 0; i < Length; i += 2) {
            model.WriteMultiByteValue(Start + i, 2, token, data[i / 2]);
         }
         return this;
      }

      public static IReadOnlyList<short> GetPalette(IReadOnlyList<byte> data, int start, int count) {
         var results = new List<short>();
         for (int i = 0; i < count; i++) {
            var color = (short)data.ReadMultiByteValue(start + i * 2, 2);
            results.Add(FlipColorChannels(color));
         }
         return results;
      }

      /// <summary>
      /// the gba and WPF do color channels reversed
      /// </summary>
      public static short FlipColorChannels(short color) {
         var r = ((color >> 10) & 0x1F);
         var g = ((color >> 5) & 0x1F);
         var b = ((color >> 0) & 0x1F);
         return (short)((b << 10) | (g << 5) | (r << 0));
      }
   }
}