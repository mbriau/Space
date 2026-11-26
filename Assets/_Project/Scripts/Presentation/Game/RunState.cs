namespace Space.Presentation.Game
{
    /// <summary>
    /// What a run keeps from one level to the next. Plain data so it can be saved later
    /// (save of the run in progress, M2).
    /// </summary>
    public sealed class RunState
    {
        public int RunIndex;
        public int Credits;
        public int LevelsCompleted;

        public void StartNewRun()
        {
            RunIndex++;
            Credits = 0;
            LevelsCompleted = 0;
        }
    }
}
