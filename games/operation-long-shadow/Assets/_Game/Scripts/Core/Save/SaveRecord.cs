using System;
using System.Collections.Generic;

namespace LongShadow.Core
{
    /// <summary>The state of one <see cref="ISaveable"/> inside a <see cref="CheckpointSnapshot"/> (name/value string pairs).</summary>
    [Serializable]
    public class SaveRecord
    {
        /// <summary>The saveable's key.</summary>
        public string Key;
        /// <summary>Field names.</summary>
        public List<string> Names = new List<string>();
        /// <summary>Field values (invariant-culture strings or JSON), parallel to <see cref="Names"/>.</summary>
        public List<string> Values = new List<string>();

        /// <summary>Index of a field, or -1.</summary>
        public int IndexOf(string name) => Names.IndexOf(name);

        /// <summary>Sets or adds a field.</summary>
        public void Set(string name, string value)
        {
            var i = Names.IndexOf(name);
            if (i >= 0) Values[i] = value;
            else
            {
                Names.Add(name);
                Values.Add(value);
            }
        }

        /// <summary>Gets a field value, or null.</summary>
        public string Get(string name)
        {
            var i = Names.IndexOf(name);
            return i >= 0 && i < Values.Count ? Values[i] : null;
        }
    }
}
