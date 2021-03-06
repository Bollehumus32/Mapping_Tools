﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Mapping_Tools.Annotations;
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.BeatmapHelper.Events;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.MathUtil;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Classes.Tools;
using Mapping_Tools.Viewmodels;

namespace Mapping_Tools.Views.HitsoundCopier {
    /// <summary>
    /// Interactielogica voor HitsoundCopierView.xaml
    /// </summary>
    public partial class HitsoundCopierView : ISavable<HitsoundCopierVm> {
        public static readonly string ToolName = "Hitsound Copier";

        /// <summary>
        /// 
        /// </summary>
        [UsedImplicitly] 
        public static readonly string ToolDescription =
            $@"Copies hitsounds from A to B.{Environment.NewLine}There are 2 modes. " +
            $@"First mode is overwrite everything. " +
            $@"This will basically first remove the hitsounds from the map you’re copying to and then copy the hitsounds." +
            $@"{Environment.NewLine}Second mode is copying only the defined hitsounds." +
            $@" A defined hitsound is when there is something there in the map you’re copying from." +
            $@" This mode will copy over all the hitsounds from the map you’re copying from. " +
            $@"Anything in the map you’re copying to that has not been defined in the map you’re copying from will not change. " +
            $@"For instance muted sliderends will remain there.";

        /// <inheritdoc />
        public HitsoundCopierView() {
            InitializeComponent();
            Width = MainWindow.AppWindow.content_views.Width;
            Height = MainWindow.AppWindow.content_views.Height;
            DataContext = new HitsoundCopierVm();
            ProjectManager.LoadProject(this, message: false);
        }

        public HitsoundCopierVm ViewModel => (HitsoundCopierVm) DataContext;

        protected override void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            var bgw = sender as BackgroundWorker;
            e.Result = Copy_Hitsounds((HitsoundCopierVm) e.Argument, bgw);
        }

        private void Start_Click(object sender, RoutedEventArgs e) {
            // Remove logical focus to trigger LostFocus on any fields that didn't yet update the ViewModel
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);

            foreach (string fileToCopy in BeatmapToBox.Text.Split('|')) {
                BackupManager.SaveMapBackup(fileToCopy);
            }

            BackgroundWorker.RunWorkerAsync(ViewModel);
            CanRun = false;
        }

        private string Copy_Hitsounds(HitsoundCopierVm arg, BackgroundWorker worker) {
            var doMutedIndex = arg.MutedIndex >= 0;

            var paths = arg.PathTo.Split('|');
            var mapsDone = 0;

            var reader = EditorReaderStuff.GetFullEditorReaderOrNot();

            foreach (var pathTo in paths) {
                var editorTo = EditorReaderStuff.GetNewestVersionOrNot(pathTo, reader);
                var editorFrom = EditorReaderStuff.GetNewestVersionOrNot(arg.PathFrom, reader);

                var beatmapTo = editorTo.Beatmap;
                var beatmapFrom = editorFrom.Beatmap;

                Timeline processedTimeline;

                if (arg.CopyMode == 0) {
                    // Every defined hitsound and sampleset on hitsound gets copied to their copyTo destination
                    // Timelines
                    var tlTo = beatmapTo.GetTimeline();
                    var tlFrom = beatmapFrom.GetTimeline();

                    var volumeMuteTimes = arg.CopyVolumes && arg.AlwaysPreserve5Volume ? new List<double>() : null;

                    if (arg.CopyHitsounds) {
                        ResetHitObjectHitsounds(beatmapTo);
                        CopyHitsounds(arg, tlFrom, tlTo);
                    }

                    // Save tlo times where timingpoint volume is 5%
                    // Timingpointchange all the undefined tlo from copyFrom
                    volumeMuteTimes?.AddRange(from tloTo in tlTo.TimelineObjects
                        where tloTo.CanCopy && Math.Abs(tloTo.SampleVolume) < Precision.DOUBLE_EPSILON
                                            && Math.Abs(tloTo.FenoSampleVolume - 5) < Precision.DOUBLE_EPSILON
                        select tloTo.Time);

                    // Volumes and samplesets and customindices greenlines get copied with timingpointchanges and allafter enabled
                    var timingPointsChanges = beatmapFrom.BeatmapTiming.TimingPoints.Select(tp =>
                        new TimingPointsChange(tp, sampleset: arg.CopySampleSets, index: arg.CopySampleSets,
                            volume: arg.CopyVolumes)).ToList();

                    // Apply the timingpoint changes
                    TimingPointsChange.ApplyChanges(beatmapTo.BeatmapTiming, timingPointsChanges, true);

                    processedTimeline = tlTo;

                    // Return 5% volume to tlo that had it before
                    if (volumeMuteTimes != null) {
                        var timingPointsChangesMute = new List<TimingPointsChange>();
                        processedTimeline.GiveTimingPoints(beatmapTo.BeatmapTiming);

                        // Exclude objects which use their own sample volume property instead
                        foreach (var tloTo in processedTimeline.TimelineObjects.Where(o => Math.Abs(o.SampleVolume) < Precision.DOUBLE_EPSILON)) {
                            if (volumeMuteTimes.Contains(tloTo.Time)) {
                                // Add timingpointschange to copy timingpoint hitsounds
                                var tp = tloTo.HitsoundTimingPoint.Copy();
                                tp.Offset = tloTo.Time;
                                tp.Volume = 5;
                                timingPointsChangesMute.Add(new TimingPointsChange(tp, volume: true));
                            } else {
                                // Add timingpointschange to preserve index and volume
                                var tp = tloTo.HitsoundTimingPoint.Copy();
                                tp.Offset = tloTo.Time;
                                tp.Volume = tloTo.FenoSampleVolume;
                                timingPointsChangesMute.Add(new TimingPointsChange(tp, volume: true));
                            }
                        }

                        // Apply the timingpoint changes
                        TimingPointsChange.ApplyChanges(beatmapTo.BeatmapTiming, timingPointsChangesMute);
                    }
                } else {
                    // Smarty mode
                    // Copy the defined hitsounds literally (not feno, that will be reserved for cleaner). Only the tlo that have been defined by copyFrom get overwritten.
                    var tlTo = beatmapTo.GetTimeline();
                    var tlFrom = beatmapFrom.GetTimeline();

                    var timingPointsChanges = new List<TimingPointsChange>();
                    var mode = (GameMode) beatmapTo.General["Mode"].IntValue;
                    var mapDir = editorTo.GetParentFolder();
                    var firstSamples = HitsoundImporter.AnalyzeSamples(mapDir);

                    if (arg.CopyHitsounds) {
                        CopyHitsounds(arg, tlFrom, tlTo, timingPointsChanges, mode, mapDir, firstSamples);
                    }

                    if (arg.CopyBodyHitsounds) {
                        // Remove timingpoints in beatmapTo that are in a sliderbody/spinnerbody for both beatmapTo and BeatmapFrom
                        foreach (var tp in from ho in beatmapTo.HitObjects
                            from tp in ho.BodyHitsounds
                            where beatmapFrom.HitObjects.Any(o => o.Time < tp.Offset && o.EndTime > tp.Offset)
                            where !tp.Uninherited
                            select tp) {
                            beatmapTo.BeatmapTiming.TimingPoints.Remove(tp);
                        }

                        // Get timingpointschanges for every timingpoint from beatmapFrom that is in a sliderbody/spinnerbody for both beatmapTo and BeatmapFrom
                        timingPointsChanges.AddRange(from ho in beatmapFrom.HitObjects
                            from tp in ho.BodyHitsounds
                            where beatmapTo.HitObjects.Any(o => o.Time < tp.Offset && o.EndTime > tp.Offset)
                            select new TimingPointsChange(tp.Copy(), sampleset: arg.CopySampleSets, index: arg.CopySampleSets,
                                volume: arg.CopyVolumes));
                    }

                    // Apply the timingpoint changes
                    TimingPointsChange.ApplyChanges(beatmapTo.BeatmapTiming, timingPointsChanges);

                    processedTimeline = tlTo;
                }

                if (arg.CopyStoryboardedSamples) {
                    if (arg.CopyMode == 0) {
                        beatmapTo.StoryboardSoundSamples.Clear();
                    }

                    beatmapTo.GiveObjectsGreenlines();
                    processedTimeline.GiveTimingPoints(beatmapTo.BeatmapTiming);

                    var mapDir = editorTo.GetParentFolder();
                    var firstSamples = HitsoundImporter.AnalyzeSamples(mapDir, true);

                    var samplesTo = new HashSet<StoryboardSoundSample>(beatmapTo.StoryboardSoundSamples);
                    var mode = (GameMode) beatmapTo.General["Mode"].IntValue;

                    foreach (var sampleFrom in beatmapFrom.StoryboardSoundSamples) {
                        if (arg.IgnoreHitsoundSatisfiedSamples) {
                            var tloHere = processedTimeline.TimelineObjects.FindAll(o =>
                                Math.Abs(o.Time - sampleFrom.StartTime) <= arg.TemporalLeniency);
                            var samplesHere = new HashSet<string>();
                            foreach (var tlo in tloHere) {
                                foreach (var filename in tlo.GetPlayingFilenames(mode)) {
                                    var samplePath = Path.Combine(mapDir, filename);
                                    var fullPathExtLess = Path.Combine(Path.GetDirectoryName(samplePath),
                                        Path.GetFileNameWithoutExtension(samplePath));

                                    if (firstSamples.Keys.Contains(fullPathExtLess)) {
                                        samplePath = firstSamples[fullPathExtLess];
                                    }

                                    samplesHere.Add(samplePath);
                                }
                            }

                            var sbSamplePath = Path.Combine(mapDir, sampleFrom.FilePath);
                            var sbFullPathExtLess = Path.Combine(Path.GetDirectoryName(sbSamplePath),
                                Path.GetFileNameWithoutExtension(sbSamplePath));

                            if (firstSamples.Keys.Contains(sbFullPathExtLess)) {
                                sbSamplePath = firstSamples[sbFullPathExtLess];
                            }

                            if (samplesHere.Contains(sbSamplePath))
                                continue;
                        }

                        // Add the StoryboardSoundSamples from beatmapFrom to beatmapTo if it doesn't already have the sample
                        if (!samplesTo.Contains(sampleFrom)) {
                            beatmapTo.StoryboardSoundSamples.Add(sampleFrom);
                        }
                    }

                    // Sort the storyboarded samples
                    beatmapTo.StoryboardSoundSamples = beatmapTo.StoryboardSoundSamples.OrderBy(o => o.StartTime).ToList();
                }

                if (arg.MuteSliderends) {
                    var timingPointsChanges = new List<TimingPointsChange>();
                    beatmapTo.GiveObjectsGreenlines();
                    processedTimeline.GiveTimingPoints(beatmapTo.BeatmapTiming);

                    foreach (var tloTo in processedTimeline.TimelineObjects) {
                        if (FilterMuteTlo(tloTo, beatmapTo, arg)) {
                            // Set volume to 5%, remove all hitsounds, apply customindex and sampleset
                            tloTo.SampleSet = arg.MutedSampleSet;
                            tloTo.AdditionSet = 0;
                            tloTo.Normal = false;
                            tloTo.Whistle = false;
                            tloTo.Finish = false;
                            tloTo.Clap = false;

                            tloTo.HitsoundsToOrigin();

                            // Add timingpointschange to copy timingpoint hitsounds
                            var tp = tloTo.HitsoundTimingPoint.Copy();
                            tp.Offset = tloTo.Time;
                            tp.SampleSet = arg.MutedSampleSet;
                            tp.SampleIndex = arg.MutedIndex;
                            tp.Volume = 5;
                            timingPointsChanges.Add(new TimingPointsChange(tp, sampleset: true, index: doMutedIndex,
                                volume: true));
                        } else {
                            // Add timingpointschange to preserve index and volume and sampleset
                            var tp = tloTo.HitsoundTimingPoint.Copy();
                            tp.Offset = tloTo.Time;
                            timingPointsChanges.Add(new TimingPointsChange(tp, sampleset: true, index: doMutedIndex,
                                volume: true));
                        }
                    }

                    // Apply the timingpoint changes
                    TimingPointsChange.ApplyChanges(beatmapTo.BeatmapTiming, timingPointsChanges);
                }

                // Save the file
                editorTo.SaveFile();

                // Update progressbar
                if (worker != null && worker.WorkerReportsProgress) {
                    worker.ReportProgress(++mapsDone * 100 / paths.Length);
                }
            }

            return "Done!";
        }

        private static void CopyHitsounds(HitsoundCopierVm arg, Timeline tlFrom, Timeline tlTo) {
            foreach (var tloFrom in tlFrom.TimelineObjects) {
                var tloTo = tlTo.GetNearestTlo(tloFrom.Time, true);

                if (tloTo != null &&
                    Math.Abs(Math.Round(tloFrom.Time) - Math.Round(tloTo.Time)) <= arg.TemporalLeniency) {
                    // Copy to this tlo
                    CopyHitsounds(arg, tloFrom, tloTo);
                }

                tloFrom.CanCopy = false;
            }
        }

        private void CopyHitsounds(HitsoundCopierVm arg, Timeline tlFrom, Timeline tlTo,
            List<TimingPointsChange> timingPointsChanges, GameMode mode, string mapDir,
            Dictionary<string, string> firstSamples) {
            foreach (var tloFrom in tlFrom.TimelineObjects) {
                var tloTo = tlTo.GetNearestTlo(tloFrom.Time, true);

                if (tloTo != null &&
                    Math.Abs(Math.Round(tloFrom.Time) - Math.Round(tloTo.Time)) <= arg.TemporalLeniency) {
                    // Copy to this tlo
                    CopyHitsounds(arg, tloFrom, tloTo);

                    // Add timingpointschange to copy timingpoint hitsounds
                    var tp = tloFrom.HitsoundTimingPoint.Copy();
                    tp.Offset = tloTo.Time;
                    timingPointsChanges.Add(new TimingPointsChange(tp, sampleset: arg.CopySampleSets,
                        index: arg.CopySampleSets, volume: arg.CopyVolumes));
                }

                tloFrom.CanCopy = false;
            }

            // Timingpointchange all the undefined tlo from copyFrom
            foreach (var tloTo in tlTo.TimelineObjects) {
                if (!tloTo.CanCopy) continue;
                var tp = tloTo.HitsoundTimingPoint.Copy();
                var holdSampleset = arg.CopySampleSets && tloTo.SampleSet == SampleSet.Auto;
                var holdIndex = arg.CopySampleSets && !(tloTo.CanCustoms && tloTo.CustomIndex != 0);

                // Dont hold indexes or sampleset if the sample it plays currently is the same as the sample it would play without conserving
                if (holdSampleset || holdIndex) {
                    var nativeSamples = tloTo.GetFirstPlayingFilenames(mode, mapDir, firstSamples);

                    if (holdSampleset) {
                        var oldSampleSet = tloTo.FenoSampleSet;
                        var newSampleSet = tloTo.FenoSampleSet;
                        var latest = double.NegativeInfinity;
                        foreach (TimingPointsChange tpc in timingPointsChanges) {
                            if (!tpc.Sampleset || !(tpc.MyTP.Offset <= tloTo.Time) || !(tpc.MyTP.Offset >= latest))
                                continue;
                            newSampleSet = tpc.MyTP.SampleSet;
                            latest = tpc.MyTP.Offset;
                        }

                        tp.SampleSet = newSampleSet;
                        tloTo.GiveHitsoundTimingPoint(tp);
                        var newSamples = tloTo.GetFirstPlayingFilenames(mode, mapDir, firstSamples);
                        tp.SampleSet = nativeSamples.SequenceEqual(newSamples) ? newSampleSet : oldSampleSet;
                    }

                    if (holdIndex) {
                        var oldIndex = tloTo.FenoCustomIndex;
                        var newIndex = tloTo.FenoCustomIndex;
                        var latest = double.NegativeInfinity;
                        foreach (var tpc in timingPointsChanges) {
                            if (!tpc.Index || !(tpc.MyTP.Offset <= tloTo.Time) || !(tpc.MyTP.Offset >= latest))
                                continue;
                            newIndex = tpc.MyTP.SampleIndex;
                            latest = tpc.MyTP.Offset;
                        }

                        tp.SampleIndex = newIndex;
                        tloTo.GiveHitsoundTimingPoint(tp);
                        var newSamples = tloTo.GetFirstPlayingFilenames(mode, mapDir, firstSamples);
                        tp.SampleIndex = nativeSamples.SequenceEqual(newSamples) ? newIndex : oldIndex;
                    }

                    tloTo.GiveHitsoundTimingPoint(tp);
                }

                tp.Offset = tloTo.Time;
                timingPointsChanges.Add(new TimingPointsChange(tp, sampleset: holdSampleset, index: holdIndex,
                    volume: arg.CopyVolumes));
            }
        }

        private static void CopyHitsounds(HitsoundCopierVm arg, TimelineObject tloFrom, TimelineObject tloTo) {
            // Copy to this tlo
            tloTo.SampleSet = tloFrom.SampleSet;
            tloTo.AdditionSet = tloFrom.AdditionSet;
            tloTo.Normal = tloFrom.Normal;
            tloTo.Whistle = tloFrom.Whistle;
            tloTo.Finish = tloFrom.Finish;
            tloTo.Clap = tloFrom.Clap;

            if (tloTo.CanCustoms) {
                tloTo.CustomIndex = tloFrom.CustomIndex;
                tloTo.SampleVolume = tloFrom.SampleVolume;
                tloTo.Filename = tloFrom.Filename;
            }

            // Copy sliderbody hitsounds
            if (tloTo.IsSliderHead && tloFrom.IsSliderHead && arg.CopyBodyHitsounds) {
                tloTo.Origin.Hitsounds = tloFrom.Origin.Hitsounds;
                tloTo.Origin.SampleSet = tloFrom.Origin.SampleSet;
                tloTo.Origin.AdditionSet = tloFrom.Origin.AdditionSet;
            }

            tloTo.HitsoundsToOrigin();
            tloTo.CanCopy = false;
        }

        private static void ResetHitObjectHitsounds(Beatmap beatmap) {
            foreach (var ho in beatmap.HitObjects) {
                // Remove all hitsounds
                ho.Clap = false;
                ho.Whistle = false;
                ho.Finish = false;
                ho.Clap = false;
                ho.SampleSet = 0;
                ho.AdditionSet = 0;
                ho.CustomIndex = 0;
                ho.SampleVolume = 0;
                ho.Filename = "";

                if (!ho.IsSlider) continue;
                // Remove edge hitsounds
                ho.EdgeHitsounds = ho.EdgeHitsounds.Select(o => 0).ToList();
                ho.EdgeSampleSets = ho.EdgeSampleSets.Select(o => SampleSet.Auto).ToList();
                ho.EdgeAdditionSets = ho.EdgeAdditionSets.Select(o => SampleSet.Auto).ToList();
            }
        }

        private static bool FilterMuteTlo(TimelineObject tloTo, Beatmap beatmapTo, HitsoundCopierVm arg) {
            // Check whether it's defined
            if (!tloTo.CanCopy)
                return false;

            // Check type
            if (!(tloTo.IsSliderEnd || tloTo.IsSpinnerEnd))
                return false;

            // Check repeats
            if (tloTo.Repeat != 1) {
                return false;
            }

            // Check filter snap
            // It's at least snap x or worse if the time is not a multiple of snap x / 2
            var timingPoint = beatmapTo.BeatmapTiming.GetRedlineAtTime(tloTo.Time - 1);
            var resnappedTime = beatmapTo.BeatmapTiming.Resnap(tloTo.Time, arg.Snap1, arg.Snap2, false, timingPoint);
            var beatsFromRedline = (resnappedTime - timingPoint.Offset) / timingPoint.MpB;
            var dist1 = beatsFromRedline * arg.Snap1 / (arg.Snap1 == 1 ? 4 : 2);
            var dist2 = beatsFromRedline * arg.Snap2 / (arg.Snap2 == 1 ? 4 : arg.Snap2 == 3 ? 3 : 2);
            dist1 %= 1;
            dist2 %= 1;
            if (Precision.AlmostEquals(dist1, 0) || Precision.AlmostEquals(dist1, 1) ||
                Precision.AlmostEquals(dist2, 0) || Precision.AlmostEquals(dist2, 1))
                return false;

            // Check filter temporal length
            return Precision.AlmostBigger(tloTo.Origin.TemporalLength, arg.MinLength * timingPoint.MpB);
        }

        public HitsoundCopierVm GetSaveData() {
            return ViewModel;
        }

        public void SetSaveData(HitsoundCopierVm saveData) {
            DataContext = saveData;
        }

        public string AutoSavePath => Path.Combine(MainWindow.AppDataPath, "hitsoundcopierproject.json");

        public string DefaultSaveFolder => Path.Combine(MainWindow.AppDataPath, "Hitsound Copier Projects");
    }
}