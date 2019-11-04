using Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObjectGenerators.GeneratorTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using Mapping_Tools.Annotations;
using Mapping_Tools.Classes.SnappingTools.DataStructure.Layers;
using Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObjectGenerators;

namespace Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObject {
    public interface IRelevantObject : IDisposable {
        /// <summary>
        /// Indicates the time of the object. Relevant objects always get sorted by time.
        /// </summary>
        double Time { get; set; }

        /// <summary>
        /// Ranging from 0 to 1. This indicates the relevancy of the relevant object. Less relevant objects should be more transparent.
        /// </summary>
        double Relevancy { get; set; }

        /// <summary>
        /// Indicates whether this object is disposed. Disposed objects should not be part of the object structure.
        /// </summary>
        bool Disposed { get; set; }

        /// <summary>
        /// Indicates whether this object is selected. Selected objects can trigger special interactions. This trait can be inherited.
        /// </summary>
        bool IsSelected { get; set; }

        /// <summary>
        /// Indicates whether this object is locked in time and space. Locked objects should not have parents. This trait cannot be inherited.
        /// </summary>
        bool IsLocked { get; set; }

        /// <summary>
        /// Indicates whether this object can get children. If false, generators will ignore this object. This trait cannot be inherited.
        /// </summary>
        bool IsInheritable { get; set; }

        /// <summary>
        /// The layer in which this object is located.
        /// </summary>
        [CanBeNull]
        RelevantObjectLayer Layer { get; set; }

        /// <summary>
        /// The generator that generated this object.
        /// </summary>
        [CanBeNull]
        RelevantObjectsGenerator Generator { get; set; }

        /// <summary>
        /// The objects that this object was generated from.
        /// </summary>
        HashSet<IRelevantObject> ParentObjects { get; set; }

        /// <summary>
        /// The objects that were generated from this object
        /// </summary>
        HashSet<IRelevantObject> ChildObjects { get; set; }

        HashSet<IRelevantObject> GetParentage();
        HashSet<IRelevantObject> GetDescendants();
        void UpdateRelevancy();
        void UpdateTime();
        void UpdateSelected();
        IRelevantObject GetLockedRelevantObject();
        void Consume(IRelevantObject other);
        double DistanceTo(IRelevantObject relevantObject);
    }
}