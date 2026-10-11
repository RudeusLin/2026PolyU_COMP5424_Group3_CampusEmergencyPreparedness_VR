using UnityEngine;

namespace CampusEmergency.Prototype
{
    public sealed class PrototypeDoor : MonoBehaviour, IPrototypeInteractable
    {
        [SerializeField] private float openAngle = -100f;
        [SerializeField] private bool startsOpen;
        private bool isOpen;

        public string Prompt => isOpen ? "[E] Close door" : "[E] Open door";
        public float OpenAngle => openAngle;

        public void Configure(float angle, bool initiallyOpen)
        {
            openAngle = angle;
            startsOpen = initiallyOpen;
            isOpen = initiallyOpen;
            transform.localRotation = Quaternion.Euler(0f, initiallyOpen ? angle : 0f, 0f);
        }

        private void Awake()
        {
            isOpen = startsOpen;
        }

        private void Update()
        {
            Quaternion target = Quaternion.Euler(0f, isOpen ? openAngle : 0f, 0f);
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, target, 140f * Time.deltaTime);
        }

        public string Interact(ICampusInteractor explorer = null)
        {
            isOpen = !isOpen;
            return string.Empty;
        }
    }
}
