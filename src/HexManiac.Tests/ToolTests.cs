﻿using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ToolTests {
      [Fact]
      public void ViewPortHasTools() {
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[100]));
         Assert.True(viewPort.HasTools);
      }

      [Fact]
      public void StringToolCanOpenOnChosenData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         var toolProperties = new List<string>();
         viewPort.Tools.PropertyChanged += (sender, e) => toolProperties.Add(e.PropertyName);
         viewPort.FollowLink(0, 0);

         Assert.Contains("SelectedIndex", toolProperties);
         Assert.IsType<PCSTool>(viewPort.Tools[viewPort.Tools.SelectedIndex]);
      }

      [Fact]
      public void StringToolEditsAreReflectedInViewPort() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.StringTool.Address = 0;

         viewPort.Tools.StringTool.Content = "Some Test"; // Text -> Test
         var pcs = (PCS)viewPort[7, 0].Format;
         Assert.Equal("s", pcs.ThisCharacter);
      }

      [Fact]
      public void StringToolCanMoveData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         var toolProperties = new List<string>();
         viewPort.Tools.StringTool.PropertyChanged += (sender, e) => toolProperties.Add(e.PropertyName);
         viewPort.Tools.StringTool.Address = 0;

         toolProperties.Clear();
         viewPort.Tools.StringTool.Content = "Some More Text";
         Assert.Contains("Address", toolProperties);
      }

      [Fact]
      public void ViewPortMovesWhenStringToolMovesData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.StringTool.Address = 0;

         viewPort.Tools.StringTool.Content = "Some More Text";
         Assert.NotEqual(0, int.Parse(viewPort.Headers[0], NumberStyles.HexNumber));
      }

      [Fact]
      public void StringToolMultiCharacterDeleteCleansUpUnusedBytes() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.StringTool.Address = 0;

         viewPort.Tools.StringTool.Content = "Some "; // removed 'Text' from the end

         Assert.Equal(0xFF, model[7]);
      }

      [Fact]
      public void HideCommandClosesAnyOpenTools() {
         var model = new PokemonModel(new byte[0x200]);
         var history = new ChangeHistory<ModelDelta>(null);
         var tools = new ToolTray(model, new Selection(new ScrollRegion(), model), history);

         tools.SelectedIndex = 1;
         tools.HideCommand.Execute();

         Assert.Equal(-1, tools.SelectedIndex);
      }

      [Fact]
      public void StringToolContentUpdatesWhenViewPortChange() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\"");

         viewPort.SelectionStart = new Point(3, 0);   // select the 'e' in 'Some'
         viewPort.FollowLink(3, 0);                   // open the string tool
         viewPort.Edit("i");                          // change the 'e' to 'i'

         Assert.Equal("Somi Text", viewPort.Tools.StringTool.Content);
      }

      [Fact]
      public void ToolSelectionChangeUpdatesViewPortSelection() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\"");
         viewPort.SelectionStart = new Point(3, 0);
         viewPort.FollowLink(3, 0);

         viewPort.Tools.StringTool.ContentIndex = 4;

         Assert.Equal(new Point(4, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void SelectingAPointerAddressInStringToolDisablesTheTool() {
         var token = new ModelDelta();
         var model = new PokemonModel(new byte[0x200]);
         model.WritePointer(token, 16, 100);
         model.ObserveRunWritten(token, new PointerRun(16));
         var tool = new PCSTool(
            model,
            new Selection(new ScrollRegion { Width = 0x10, Height = 0x10 }, model),
            new ChangeHistory<ModelDelta>(dm => dm),
            null);

         tool.Address = 18;

         Assert.Equal(18, tool.Address); // address updated correctly
         Assert.False(tool.Enabled);     // run is not one that this tool knows how to edit
      }

      [Fact]
      public void TableToolUpdatesWhenTextToolDataChanges() {
         // Arrange
         var data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");
         viewPort.SelectionStart = new Point(8, 1);

         // Act: Update via the Text Tool
         viewPort.Tools.SelectedIndex = Enumerable.Range(0, 10).First(i => viewPort.Tools[i] == viewPort.Tools.StringTool);
         viewPort.Tools.StringTool.Content = Environment.NewLine + "Larry";

         // Assert: Table Tool is updated
         viewPort.Tools.SelectedIndex = Enumerable.Range(0, 10).First(i => viewPort.Tools[i] == viewPort.Tools.TableTool);
         var field = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[0];
         Assert.Equal("Larry", field.Content);
      }

      [Fact]
      public void TextToolToolUpdatesWhenTableToolDataChanges() {
         // Arrange
         var data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");
         viewPort.SelectionStart = new Point(8, 1);

         // Act: Update via the Table Tool
         viewPort.Tools.SelectedIndex = Enumerable.Range(0, 10).First(i => viewPort.Tools[i] == viewPort.Tools.TableTool);
         var field = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[0];
         field.Content = "Larry";

         // Assert: Text Tool is updated
         viewPort.Tools.SelectedIndex = Enumerable.Range(0, 10).First(i => viewPort.Tools[i] == viewPort.Tools.StringTool);
         var textToolContent = viewPort.Tools.StringTool.Content.Split(Environment.NewLine)[1];
         Assert.Equal("Larry", textToolContent);
      }

      [Fact]
      public void TableToolUpdatesIndexOnCursorMove() {
         // Arrange
         var data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");

         // Act: move the cursor to change the selected table item
         viewPort.SelectionStart = new Point(8, 1);

         // Assert: table item index 1 is selected
         Assert.Contains("1", viewPort.Tools.TableTool.CurrentElementName);
      }

      [Fact]
      public void ContentUpdateFromAnotherToolDoesNotResetCaretInStringTool() {
         // Arrange
         var data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");

         // mock the view: whenever the stringtool content changes,
         // reset the cursor to the start position.
         viewPort.Tools.StringTool.PropertyChanged += (sender, e) => {
            if (e.PropertyName == "Content") viewPort.Tools.StringTool.ContentIndex = 0;
         };

         viewPort.SelectionStart = new Point(8, 1);                                                                         // move the cursor
         viewPort.Tools.SelectedIndex = Enumerable.Range(0, 10).First(i => viewPort.Tools[i] == viewPort.Tools.StringTool); // open the string tool
         viewPort.Tools.StringTool.ContentIndex = 12;                                                                       // place the cursor somewhere, like the UI would
         viewPort.Tools.SelectedIndex = Enumerable.Range(0, 10).First(i => viewPort.Tools[i] == viewPort.Tools.TableTool);  // open the table tool
         var field = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[0];
         field.Content = "Larry";                                                                                           // make a change with the table tool

         Assert.NotEqual(new Point(), viewPort.SelectionStart);
      }

      [Fact]
      public void TableToolCanExtendTable() {
         // Arrange
         var data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");
         viewPort.Tools.SelectedIndex = Enumerable.Range(0, viewPort.Tools.Count).Single(i => viewPort.Tools[i] == viewPort.Tools.TableTool);
         Assert.True(viewPort.Tools.TableTool.Next.CanExecute(null)); // table has 3 entries

         // Act: move to end of table
         while (viewPort.Tools.TableTool.Next.CanExecute(null)) viewPort.Tools.TableTool.Next.Execute();

         Assert.True(viewPort.Tools.TableTool.Append.CanExecute(null));
         viewPort.Tools.TableTool.Append.Execute();
         Assert.Contains("3", viewPort.Tools.TableTool.CurrentElementName);
         Assert.Equal(16 * 4, model.GetNextRun(0).Length);
      }

      [Fact]
      public void TableToolNotOfferedOnNormalText() {
         // Arrange
         var data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^array[name\"\"16]3 ");
         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^text\"\" Some Text\"");

         // Act
         viewPort.SelectionStart = new Point(2, 4);
         var items = viewPort.GetContextMenuItems(viewPort.SelectionStart);

         // Assert
         var matches = items.Where(item => item.Text.Contains("Table"));
         Assert.Empty(matches);
      }

      [Theory]
      [InlineData(0x0000, "lsl   r0, r0, #0")]
      [InlineData(0b0001100_010_001_000, "add   r0, r1, r2")]
      [InlineData(0b00000_00100_010_001, "lsl   r1, r2, #4")]
      [InlineData(0b1101_0000_00001010, "beq   <00001C>")] // 1C = 28 (current address is zero)
      public void ThumbDecompilerTests(int input, string output) {
         var bytes = new[] { (byte)input, (byte)(input >> 8) };
         var result = parser.Parse(bytes, 0, 2).Split(Environment.NewLine)[1].Trim();
         Assert.Equal(output, result);
      }

      [Fact]
      public void DecompileThumbRoutineTest() {
         // sample routine: If r0 is true, return double r1. Else, return 0
         var code = new ushort[] {
         // 000000:
            0b10110101_00001100,    // push  lr, {r4, r5}
            0b00101_000_00000001,   // cmp   r0, 1
            0b1101_0001_11111110,   // bne   pc(4)+(-2)*2+8 = 8
            0b0001100_001_001_000,  // add   r0, r1, r1
         // 000008:
            0b10111101_00001100,    // pop   pc, {r4, r5}
         };

         var bytes = code.SelectMany(pair => new[] { (byte)pair, (byte)(pair >> 8) }).ToArray();
         var lines = parser.Parse(bytes, 0, bytes.Length).Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         Assert.Equal(7, lines.Length);
         Assert.Equal("000000:",               lines[0]);
         Assert.Equal("    push  lr, {r4-r5}", lines[1]);
         Assert.Equal("    cmp   r0, #1",      lines[2]);
         Assert.Equal("    bne   <000008>",    lines[3]);
         Assert.Equal("    add   r0, r1, r1",  lines[4]);
         Assert.Equal("000008:",               lines[5]);
         Assert.Equal("    pop   pc, {r4-r5}", lines[6]);
      }

      private static readonly ThumbParser parser;
      static ToolTests(){
         parser = new ThumbParser(File.ReadAllLines("Models/Code/armReference.txt"));
      }
   }
}
