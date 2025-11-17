namespace MJ.Akka.EventReactor.RavenDb;

public record EventReactorState(string Id, string ReactorName, string Slug, object State)
{
    public static string BuildId(string reactorName, string slug)
    {
        return $"event-reactors/{reactorName}/states/{slug}";
    }
}