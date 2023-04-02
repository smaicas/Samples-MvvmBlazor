namespace Nj.Samples.MvvmWebsocketServer.Shared;
public class GameMatch
{
    public Guid Id { get; set; }
    public Team Local { get; set; }
    public Team Visitor { get; set; }
    public int LocalGoals { get; set; }
    public int VisitorGoals { get; set; }
    public bool IsFinished { get; set; }
}

public class Team
{
    public string Name { get; set; }
    public string LogoUrl { get; set; }
}
