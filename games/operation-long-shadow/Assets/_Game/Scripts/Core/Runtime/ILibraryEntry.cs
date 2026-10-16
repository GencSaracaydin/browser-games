namespace LongShadow.Core
{
    /// <summary>An entry of an <see cref="AssetLibrary{TEntry}"/>, looked up by id (TECH_PLAN D12).</summary>
    public interface ILibraryEntry
    {
        /// <summary>Lookup id (weapon id, cue id, line id, manifest id ...).</summary>
        string Id { get; }
    }
}
