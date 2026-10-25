namespace NullSave.CharPad
{
    internal sealed record CharGroup(string Key, int Count)
    {

        #region Public Methods

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Key}  ({Count:N0})";
        }

        #endregion
    }

}
