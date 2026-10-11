namespace CampusEmergency.Prototype
{
    public interface IPrototypeInteractable
    {
        string Prompt { get; }
        string Interact(ICampusInteractor explorer = null);
    }
}
