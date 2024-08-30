namespace DC.Akka.EventReactor;

public class NoEventReactorException(string reactorName)
    : Exception($"Didn't find any event reactor with name {reactorName}");