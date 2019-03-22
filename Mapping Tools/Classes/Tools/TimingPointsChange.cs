﻿using Mapping_Tools.Classes.BeatmapHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mapping_Tools.Classes.Tools {
    public struct TimingPointsChange {

        public TimingPoint MyTP;
        public bool MpB;
        public bool Meter;
        public bool Sampleset;
        public bool Index;
        public bool Volume;
        public bool Inherited;
        public bool Kiai;
        public bool OmitFirstBarLine;

        public TimingPointsChange(TimingPoint tpNew, bool mpb = false, bool meter = false, bool sampleset = false, bool index = false, bool volume = false, bool inherited = false, bool kiai = false, bool omitFirstBarLine = false) {
            MyTP = tpNew;
            MpB = mpb;
            Meter = meter;
            Sampleset = sampleset;
            Index = index;
            Volume = volume;
            Inherited = inherited;
            Kiai = kiai;
            OmitFirstBarLine = omitFirstBarLine;
        }

        public void AddChange2(List<TimingPoint> list, bool allAfter = false) {
            TimingPoint prevTimingPoint = null;
            List<TimingPoint> onTimingPoints = new List<TimingPoint>();

            foreach (TimingPoint tp in list) {
                if (tp == null) { continue; }  // Continue nulls to avoid exceptions
                if (tp.Offset < MyTP.Offset && (prevTimingPoint == null || tp.Offset >= prevTimingPoint.Offset)) {
                    prevTimingPoint = tp;
                }
                if (tp.Offset == MyTP.Offset) {
                    onTimingPoints.Add(tp);
                }
            }

            foreach(TimingPoint on in onTimingPoints) {
                if (MpB) { on.MpB = MyTP.MpB; }
                if (Meter) { on.Meter = MyTP.Meter; }
                if (Sampleset) { on.SampleSet = MyTP.SampleSet; }
                if (Index) { on.SampleIndex = MyTP.SampleIndex; }
                if (Volume) { on.Volume = MyTP.Volume; }
                if (Inherited) { on.Inherited = MyTP.Inherited; }
                if (Kiai) { on.Kiai = MyTP.Kiai; }
                if (OmitFirstBarLine) { on.OmitFirstBarLine = MyTP.OmitFirstBarLine; }
            }

        }

        public void AddChange(List<TimingPoint> list, Timing timing, bool allAfter=false) {
            TimingPoint prev = null;
            TimingPoint on = null;
            foreach (TimingPoint tp in list) {
                if (tp == null) {
                    continue;
                }
                if (prev == null) {
                    if (tp.Offset < MyTP.Offset) {
                        prev = tp;
                    }
                } else if (tp.Offset >= prev.Offset && tp.Offset < MyTP.Offset) {
                    prev = tp;
                }
                if (tp.Offset == MyTP.Offset) {
                    if (tp.Inherited && MpB) {
                        prev = tp;
                    } else {
                        on = tp;
                    }
                }
            }

            if (on != null) {
                if (MpB) { on.MpB = MyTP.MpB; }
                if (Meter) { on.Meter = MyTP.Meter; }
                if (Sampleset) { on.SampleSet = MyTP.SampleSet; }
                if (Index) { on.SampleIndex = MyTP.SampleIndex; }
                if (Volume) { on.Volume = MyTP.Volume; }
                if (Inherited) { on.Inherited = MyTP.Inherited; }
                if (Kiai) { on.Kiai = MyTP.Kiai; }
                if (OmitFirstBarLine) { on.OmitFirstBarLine = MyTP.OmitFirstBarLine; }
            } else {
                if (prev != null) {
                    // Make new timingpoint
                    if (prev.Inherited) {
                        on = new TimingPoint(MyTP.Offset, -100, prev.Meter, prev.SampleSet, prev.SampleIndex, prev.Volume, false, prev.Kiai, prev.OmitFirstBarLine);
                    } else {
                        on = new TimingPoint(MyTP.Offset, prev.MpB, prev.Meter, prev.SampleSet, prev.SampleIndex, prev.Volume, false, prev.Kiai, prev.OmitFirstBarLine);
                    }
                    if (MpB) { on.MpB = MyTP.MpB; }
                    if (Meter) { on.Meter = MyTP.Meter; }
                    if (Sampleset) { on.SampleSet = MyTP.SampleSet; }
                    if (Index) { on.SampleIndex = MyTP.SampleIndex; }
                    if (Volume) { on.Volume = MyTP.Volume; }
                    if (Inherited) { on.Inherited = MyTP.Inherited; }
                    if (Kiai) { on.Kiai = MyTP.Kiai; }
                    if (OmitFirstBarLine) { on.OmitFirstBarLine = MyTP.OmitFirstBarLine; }

                    if (!on.SameEffect(prev) || Inherited) {
                        list.Add(on);
                    }
                } else {
                    list.Add(MyTP);
                }
            }

            if (allAfter) // Change every timingpoint after
            {
                foreach (TimingPoint tp in list) {
                    if (tp.Offset > MyTP.Offset) {
                        if (Sampleset) { tp.SampleSet = MyTP.SampleSet; }
                        if (Index) { tp.SampleIndex = MyTP.SampleIndex; }
                        if (Volume) { tp.Volume = MyTP.Volume; }
                        if (Kiai) { tp.Kiai = MyTP.Kiai; }
                    }
                }
            }
        }
    }
}
