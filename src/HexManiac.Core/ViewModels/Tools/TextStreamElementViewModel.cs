﻿using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TextStreamElementViewModel : StreamElementViewModel {
      string content;
      public string Content {
         get => content;
         set {
            if (TryUpdate(ref content, value)) {
               using (ModelCacheScope.CreateScope(Model)) {
                  var destination = Model.ReadPointer(Start);
                  var run = (IStreamRun)Model.GetNextRun(destination);
                  var newRun = run.DeserializeRun(content, ViewPort.CurrentChange, out var changedOffsets);
                  HandleNewDataStream(run, newRun, changedOffsets);
               }
            }
         }
      }

      protected void HandleNewDataStream(IStreamRun oldRun, IStreamRun newRun, IReadOnlyList<int> changedOffsets) {
         Model.ObserveRunWritten(ViewPort.CurrentChange, newRun);
         if (oldRun.Start != newRun.Start) {
            RaiseDataMoved(oldRun.Start, newRun.Start);
         }

         if (Model.GetNextRun(newRun.Start) is ITableRun table) {
            foreach(var change in changedOffsets) {
               var offsets = table.ConvertByteOffsetToArrayOffset(change);
               var info = table.NotifyChildren(Model, ViewPort.CurrentChange, offsets.ElementIndex, offsets.SegmentIndex);
               ViewPort.HandleErrorInfo(info);
            }
         }

         using (PreventSelfCopy()) {
            RaiseDataChanged();
         }

         NotifyPropertyChanged(nameof(Visualizations));
      }

      public IReadOnlyList<IPixelViewModel> Visualizations {
         get {
            var destination = Model.ReadPointer(Start);
            if (Model.GetNextRun(destination) is IStreamRun run) {
               return run.Visualizations;
            }
            return new List<IPixelViewModel>();
         }
      }

      public TextStreamElementViewModel(ViewPort viewPort, int start, string format) : base(viewPort, format ?? PCSRun.SharedFormatString, start) {
         var destination = viewPort.Model.ReadPointer(Start);
         if (viewPort.Model.GetNextRun(destination) is IStreamRun run) {
            content = run.SerializeRun() ?? string.Empty;
         } else {
            content = string.Empty;
         }
      }

      protected override bool TryCopy(StreamElementViewModel other) {
         if (GetType() != other.GetType()) return false;
         if (!(other is TextStreamElementViewModel stream)) return false;
         Start = other.Start;
         TryUpdate(ref content, stream.content, nameof(Content));
         NotifyPropertyChanged(nameof(Visualizations));
         return true;
      }
   }
}
