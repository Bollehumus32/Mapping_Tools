﻿using System;
using System.Collections.Generic;
using System.Linq;
using Mapping_Tools.Classes.SystemTools;
using Newtonsoft.Json;

namespace Mapping_Tools.Viewmodels {
    public class SliderMergerVm : BindableBase
    {
        #region Properties

        [JsonIgnore]
        public string[] Paths { get; set; }

        [JsonIgnore]
        public bool Quick { get; set; }

        private ImportMode _importModeSetting;
        public ImportMode ImportModeSetting {
            get => _importModeSetting;
            set => Set(ref _importModeSetting, value);
        }

        public IEnumerable<ImportMode> ImportModes => Enum.GetValues(typeof(ImportMode)).Cast<ImportMode>();

        private ConnectionMode _connectionModeSetting;
        public ConnectionMode ConnectionModeSetting {
            get => _connectionModeSetting;
            set => Set(ref _connectionModeSetting, value);
        }

        public IEnumerable<ConnectionMode> ConnectionModes => Enum.GetValues(typeof(ConnectionMode)).Cast<ConnectionMode>();

        private double _leniency;
        public double Leniency {
            get => _leniency;
            set => Set(ref _leniency, value);
        }

        private bool _linearOnLinear;
        public bool LinearOnLinear {
            get => _linearOnLinear;
            set => Set(ref _linearOnLinear, value);
        }

        private bool _mergeOnSliderEnd;
        public bool MergeOnSliderEnd {
            get => _mergeOnSliderEnd;
            set => Set(ref _mergeOnSliderEnd, value);
        }

        #endregion

        public SliderMergerVm() {
            ImportModeSetting = ImportMode.Selected;
            ConnectionModeSetting = ConnectionMode.Move;
            Leniency = 2;
            LinearOnLinear = false;
            MergeOnSliderEnd = true;
        }

        public enum ImportMode
        {
            Selected,
            Bookmarked,
            Time,
            Everything
        }

        public enum ConnectionMode
        {
            Move,
            Linear
        }
    }
}
