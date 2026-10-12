namespace ProjectShaman.AI.Ghost
{
    public interface IGhostActionAgent
    {
        GhostNightCommand Command { get; }
        GhostActionContext CreateContext();
        void ReportActionEnded(bool succeeded);
    }
}
