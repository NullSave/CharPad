namespace NullSave.CharPad
{

    internal sealed record CodePage(int Id, string Name)
    {

        #region Public Methods

        /// <inheritdoc />
        public override string ToString()
        {
            return Name;
        }

        #endregion

    }

}