using System.Windows.Media;

namespace NullSave.CharPad
{

    internal sealed record FontEntry(string Name, FontFamily Family)
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
