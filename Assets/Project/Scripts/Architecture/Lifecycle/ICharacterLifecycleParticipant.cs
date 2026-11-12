/// <summary>
/// Optional hook for future components (for example new mob/boss AI) that must
/// clean up on death and return to their starting runtime state on a reset.
/// This does NOT implement any AI and has no dependency on prototype controllers.
/// </summary>
public interface ICharacterLifecycleParticipant
{
    void OnCharacterDied();
    void OnCharacterReset();
}
