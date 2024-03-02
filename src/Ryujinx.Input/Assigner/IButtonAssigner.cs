namespace Ryujinx.Input.Assigner
{
    /// <summary>
    /// An interface that allows to gather the driver input info to assign to a button on the UI.
    /// </summary>
    public interface IButtonAssigner
    {
        /// <summary>
        /// Initialize the button assigner.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Read input.
        /// </summary>
        void ReadInput();

        /// <summary>
        /// Check if a button was pressed.
        /// </summary>
        /// <returns>True if a button was pressed</returns>
        bool HasAnyButtonPressed();

        /// <summary>
        /// Indicate if the user of this API should cancel operations. This is triggered for example when a gamepad get disconnected or when a user cancel assignation operations.
        /// </summary>
        /// <returns>True if the user of this API should cancel operations</returns>
        bool ShouldCancel();

        /// <summary>
        /// Get the pressed button that was read in <see cref="ReadInput"/> by the button assigner.
        /// </summary>
        /// <returns>The pressed button that was read</returns>
        ButtonValue? GetPressedButton();
    }
}
