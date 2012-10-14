using System.ServiceModel;

namespace iImagineC.WCF
{
    public interface ISubscribedClient
    {
        [OperationContract(IsOneWay = true)]
        void Callback(string data);
    }

    [ServiceContract(CallbackContract = typeof(ISubscribedClient))]
    public interface ISubscriberService
    {
        [OperationContract(IsOneWay = true)]
        void Register(string data);
    }

}
