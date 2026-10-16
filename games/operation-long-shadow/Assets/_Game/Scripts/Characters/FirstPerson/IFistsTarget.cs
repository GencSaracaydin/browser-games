namespace LongShadow.Characters.FirstPerson
{
    /// <summary>Anything whose hands follow <c>IFirstPersonBody.SetFists</c> (FP arms, the seated booth Calder).</summary>
    public interface IFistsTarget
    {
        /// <summary>Clench (true) or lay the hands flat (false).</summary>
        void SetFists(bool clenched);
    }
}
