namespace LongShadow.Core
{
    /// <summary>
    /// One objective line (GDD §7.12 master list). The HUD prints
    /// <c>OBJ {Index}/{Total} — {FNV-1a(Id) mod 100000:D5} — {Text}</c> (see <see cref="MathX.Fnv1a32"/>).
    /// </summary>
    public struct ObjectiveDef
    {
        /// <summary>Objective id from GDD §7.12 (stable; used by save and tests).</summary>
        public string Id;
        /// <summary>Upper-case objective text.</summary>
        public string Text;
        /// <summary>Beat counter shown before the flavour group.</summary>
        public int Index;
        /// <summary>Total objectives in the counter.</summary>
        public int Total;
        /// <summary>5-digit flavour group text (empty = derived from the id).</summary>
        public string FlavourGroup;
        /// <summary>Optional objective (does not block progression).</summary>
        public bool Optional;
    }
}
