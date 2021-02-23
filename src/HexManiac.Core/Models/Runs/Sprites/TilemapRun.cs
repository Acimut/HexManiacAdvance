﻿using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class TilemapRun : BaseRun, ITilemapRun {
      public IDataModel Model { get; }

      public TilemapFormat Format { get; }

      public int BytesPerTile => 2;

      public bool SupportsImport => false;

      public bool SupportsEdit => false;

      public SpriteFormat SpriteFormat { get; }

      public int Pages => 1;

      public override int Length => Format.TileWidth * Format.TileHeight * BytesPerTile;

      public override string FormatString {
         get {
            var start = $"`ucm{Format.BitsPerPixel}x{Format.TileWidth}x{Format.TileHeight}|{Format.MatchingTileset}";
            if (Format.TilesetTableMember != null) start += "|" + Format.TilesetTableMember;
            return start + '`';
         }
      }

      public TilemapRun(IDataModel model, int start, TilemapFormat format, SortedSpan<int> sources = null) : base(start, sources) {
         Model = model;
         Format = format;

         string hint = null;
         var address = Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, Format.MatchingTileset);
         if (address >= 0 && address < Model.Count) {
            var tileset = Model.GetNextRun(address) as ISpriteRun;
            // if (tileset == null) tileset = Model.GetNextRun(arrayTilesetAddress) as ISpriteRun;
            if (tileset != null && !(tileset is LzTilemapRun)) hint = tileset.SpriteFormat.PaletteHint;
         }

         SpriteFormat = new SpriteFormat(format.BitsPerPixel, format.TileWidth, format.TileHeight, hint);
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         throw new System.NotImplementedException();
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(model, start + i, 0xFF);
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var offset = index - Start;
         var segStart = offset - offset % 2;
         return new IntegerHex(segStart, offset % 2, Model.ReadMultiByteValue(segStart, 2), 2);
      }

      public ISpriteRun Duplicate(SpriteFormat newFormat) {
         throw new System.NotImplementedException();
      }

      public int FindMatchingTileset(IDataModel model) {
         throw new System.NotImplementedException();
      }

      public byte[] GetTilemapData() {
         var data = new byte[Format.ExpectedUncompressedLength];
         Array.Copy(Model.RawData, Start, data, 0, data.Length);
         return data;
      }

      public byte[] GetData() {
         var tilesetAddress = Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, Format.MatchingTileset);
         var tileset = Model.GetNextRun(tilesetAddress) as ISpriteRun;
         // if (tileset == null) tileset = Model.GetNextRun(arrayTilesetAddress) as ISpriteRun;

         if (tileset == null) return new byte[Format.TileWidth * 8 * Format.TileHeight * Format.BitsPerPixel];

         var tiles = tileset.GetData();

         return LzTilemapRun.GetData(GetTilemapData(), tiles, Format, BytesPerTile);
      }

      public int[,] GetPixels(IDataModel model, int page) {
         var mapData = GetTilemapData();

         var tilesetAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, Format.MatchingTileset);
         var tileset = model.GetNextRun(tilesetAddress) as ISpriteRun;
         // if (tileset == null) tileset = model.GetNextRun(arrayTilesetAddress) as ISpriteRun;

         if (tileset == null) return new int[Format.TileWidth * 8, Format.TileHeight * 8]; // relax the conditions slightly: if the run we found is an LZSpriteRun, that's close enough, we can use it as a tileset.

         var tiles = tileset.GetData();

         return LzTilemapRun.GetPixels(mapData, tiles, Format, BytesPerTile);
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         throw new System.NotImplementedException();
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new TilemapRun(Model, Start, Format, newPointerSources);

      ITilemapRun ITilemapRun.Duplicate(TilemapFormat format) => Duplicate(format);
      public TilemapRun Duplicate(TilemapFormat format) => new TilemapRun(Model, Start, format, PointerSources);
   }
}