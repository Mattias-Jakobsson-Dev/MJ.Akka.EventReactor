using Amazon.SimpleNotificationService.Model;

namespace MJ.Akka.EventReactor.SNS;

public interface ISnsMessageSerializer
{
    Task<PublishRequest> Serialize(object message);
}