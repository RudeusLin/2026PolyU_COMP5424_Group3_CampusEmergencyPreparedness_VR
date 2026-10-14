using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CampusEmergency.Interaction.FireSafety;
using CampusEmergency.Prototype;

namespace CampusEmergency.Player.XR
{
    /// <summary>Head-locked direction arrow and world-space beacon for the current drill objective.</summary>
    [DisallowMultipleComponent]
    public sealed class CampusXRGuidancePointer : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform assemblyPoint;
        [SerializeField] private float arrivalDistance = 1.8f;
        [SerializeField] private float fireDiversionDistance = 4.5f;
        private CampusXRInventory inventory;
        private CampusXRExtinguisherController extinguisher;
        private CampusDrillAtmosphere atmosphere;
        private RectTransform arrow;
        private Texture2D arrowTexture;
        private Sprite arrowSprite;
        private Text distanceLabel;
        private Transform beacon;
        private Renderer beaconRenderer;
        private Transform routeAnchor;
        private Transform target;
        private int routeIndex;
        private int activeRoute = -1;

        // Door, flight ends and floor landings. Keeping the turns explicit prevents the
        // beacon from pointing through ceilings or across the stairwell void.
        private static readonly Vector3[] RouteA =
        {
            new Vector3(-12.05f,10.8f,3f), new Vector3(-14.42f,10.8f,1.8f),
            new Vector3(-14.42f,9f,-3f), new Vector3(-16.58f,9f,-1.65f), new Vector3(-16.58f,7.2f,3f),
            new Vector3(-14.42f,7.2f,1.8f), new Vector3(-14.42f,5.4f,-3f), new Vector3(-16.58f,5.4f,-1.65f), new Vector3(-16.58f,3.6f,3f),
            new Vector3(-14.42f,3.6f,1.8f), new Vector3(-14.42f,1.8f,-3f), new Vector3(-16.58f,1.8f,-1.65f), new Vector3(-16.58f,0f,3f)
        };
        private static readonly Vector3[] RouteB =
        {
            new Vector3(12.05f,10.8f,3f), new Vector3(16.58f,10.8f,1.8f),
            new Vector3(16.58f,9f,-3f), new Vector3(14.42f,9f,-1.65f), new Vector3(14.42f,7.2f,3f),
            new Vector3(16.58f,7.2f,1.8f), new Vector3(16.58f,5.4f,-3f), new Vector3(14.42f,5.4f,-1.65f), new Vector3(14.42f,3.6f,3f),
            new Vector3(16.58f,3.6f,1.8f), new Vector3(16.58f,1.8f,-3f), new Vector3(14.42f,1.8f,-1.65f), new Vector3(14.42f,0f,3f)
        };

        public Transform CurrentTarget => target;

        public void Configure(Transform cameraTransform, Transform assembly)
        {
            head = cameraTransform;
            assemblyPoint = assembly;
            // Visuals are runtime-only. Creating them while the editor builds the prefab
            // would save a second, inert arrow that has no runtime references.
            if (Application.isPlaying) BuildVisuals();
        }

        private void Awake()
        {
            inventory = GetComponent<CampusXRInventory>();
            extinguisher = GetComponent<CampusXRExtinguisherController>();
            if (head == null)
            {
                var origin = GetComponent<Unity.XR.CoreUtils.XROrigin>();
                if (origin != null && origin.Camera != null) head = origin.Camera.transform;
            }
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            if (head == null || arrow != null) return;
            var canvasObject = new GameObject("Next Step Pointer", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(head, false);
            canvasObject.transform.localPosition = new Vector3(0f, -.06f, 1.05f);
            canvasObject.transform.localScale = Vector3.one * .001f;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = head.GetComponent<Camera>();
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(260f, 150f);

            var arrowObject = new GameObject("Direction Arrow", typeof(RectTransform), typeof(Image));
            arrowObject.transform.SetParent(canvasObject.transform, false);
            arrow = (RectTransform)arrowObject.transform;
            arrow.sizeDelta = new Vector2(54f, 98f);
            arrow.anchoredPosition = new Vector2(0f, 18f);
            var arrowImage = arrowObject.GetComponent<Image>();
            arrowTexture = CreateIsoscelesTriangle(48, 86);
            arrowSprite = Sprite.Create(arrowTexture, new Rect(0, 0, arrowTexture.width, arrowTexture.height), new Vector2(.5f, .5f), 100f);
            arrowImage.sprite = arrowSprite;
            arrowImage.color = new Color(.2f, 1f, .75f, 1f);
            arrowImage.preserveAspect = true;
            arrowImage.raycastTarget = false;

            var labelObject = new GameObject("Distance", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(canvasObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.sizeDelta = new Vector2(260f, 48f);
            labelRect.anchoredPosition = new Vector2(0f, -48f);
            distanceLabel = labelObject.GetComponent<Text>();
            distanceLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            distanceLabel.fontSize = 25;
            distanceLabel.alignment = TextAnchor.MiddleCenter;
            distanceLabel.color = Color.white;
            distanceLabel.raycastTarget = false;

            beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            beacon.name = "Next Step Beacon";
            var collider = beacon.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
            beacon.localScale = new Vector3(.13f, 1.1f, .13f);
            beaconRenderer = beacon.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Sprites/Default"));
            material.color = new Color(.12f, 1f, .68f, .72f);
            beaconRenderer.material = material;

            routeAnchor = new GameObject("Route Guidance Target").transform;
            routeAnchor.SetParent(transform, true);
        }

        private static Texture2D CreateIsoscelesTriangle(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var solid = new Color32(255, 255, 255, 255);
            for (int y = 0; y < height; y++)
            {
                // Wide base at the bottom, narrow tip at the top.
                float halfWidth = (1f - y / (float)(height - 1)) * (width - 1) * .5f;
                float centre = (width - 1) * .5f;
                for (int x = 0; x < width; x++) texture.SetPixel(x, y, Mathf.Abs(x - centre) <= halfWidth ? solid : clear);
            }
            texture.name = "Guidance Isosceles Triangle";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply();
            return texture;
        }

        private void LateUpdate()
        {
            if (head == null) return;
            if (inventory == null) inventory = GetComponent<CampusXRInventory>();
            if (extinguisher == null) extinguisher = GetComponent<CampusXRExtinguisherController>();
            target = ResolveTarget();
            bool visible = target != null && !LanguageSelectionUI.IsBlocking;
            if (arrow != null) arrow.parent.gameObject.SetActive(visible);
            if (beacon != null) beacon.gameObject.SetActive(visible);
            if (!visible) return;

            Vector3 delta = target.position - head.position;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            Vector3 local = head.InverseTransformDirection(delta.normalized);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            arrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
            distanceLabel.text = distance <= arrivalDistance ? "HERE" : Mathf.CeilToInt(distance) + " m";
            beacon.position = target.position + Vector3.up * (1.1f + Mathf.Sin(Time.unscaledTime * 3f) * .18f);
            beacon.rotation = Quaternion.Euler(0f, Time.unscaledTime * 80f, 0f);
        }

        private Transform ResolveTarget()
        {
            if (atmosphere == null) atmosphere = FindObjectOfType<CampusDrillAtmosphere>(true);
            if (atmosphere != null && !atmosphere.IsAlarmActive)
                return FindObjectsOfType<FacilityNotice>(true)
                    .Where(x => x.Prompt.ToLowerInvariant().Contains("alarm"))
                    .OrderBy(x => FlatDistance(x.transform.position)).Select(x => x.transform).FirstOrDefault();

            if (inventory == null || inventory.HeldTowel == null)
                return FindObjectsOfType<CampusTowel>(true).Where(x => x.gameObject.activeInHierarchy)
                    .OrderBy(x => FlatDistance(x.transform.position)).Select(x => x.transform).FirstOrDefault();
            if (!inventory.HeldTowel.IsWet)
                return FindObjectsOfType<CampusFaucet>(true).Where(x => x.gameObject.activeInHierarchy)
                    .OrderBy(x => FlatDistance(x.transform.position)).Select(x => x.transform).FirstOrDefault();

            var liveFire = FindObjectsOfType<CampusFireTarget>(true)
                .Where(x => x.gameObject.activeInHierarchy && !x.Extinguished)
                .OrderBy(x => NavigationDistance(x.transform.position)).FirstOrDefault();

            // Reaching the ground floor completes the indoor route. From this point the
            // assembly point always wins, even if fires remain active elsewhere inside.
            if (transform.position.y <= 1.2f) return assemblyPoint;

            // A nearby fire blocks safe passage: temporarily divert the user to the
            // nearest available extinguisher, then point back to that fire once equipped.
            if (liveFire != null && NavigationDistance(liveFire.transform.position) <= fireDiversionDistance)
            {
                if (extinguisher == null || extinguisher.Held == null)
                {
                    var item = FindObjectsOfType<CampusExtinguisher>(true)
                        .Where(x => x.gameObject.activeInHierarchy && (extinguisher == null || x != extinguisher.Held))
                        .OrderBy(x => NavigationDistance(x.transform.position)).FirstOrDefault();
                    if (item != null) return item.transform;
                }
                return liveFire.transform;
            }

            return ResolveRouteWaypoint();
        }

        private Transform ResolveRouteWaypoint()
        {
            int selected = LanguageSelectionUI.SelectedRoute;
            Vector3[] points = selected == 0 ? RouteA : RouteB;
            if (activeRoute != selected)
            {
                activeRoute = selected;
                routeIndex = 0;
            }

            // Advance only after physically reaching the current 3D waypoint. This makes
            // each beacon disappear and the next floor/landing beacon appear immediately.
            while (routeIndex < points.Length - 1 && NavigationDistance(points[routeIndex]) <= arrivalDistance)
                routeIndex++;
            routeAnchor.position = points[Mathf.Clamp(routeIndex, 0, points.Length - 1)];
            return routeAnchor;
        }

        private float FlatDistance(Vector3 position)
        {
            Vector3 delta = position - head.position;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        private float NavigationDistance(Vector3 position) => Vector3.Distance(transform.position, position);

        private void OnDestroy()
        {
            if (beaconRenderer != null && beaconRenderer.sharedMaterial != null)
            {
                if (Application.isPlaying) Destroy(beaconRenderer.material);
                else DestroyImmediate(beaconRenderer.sharedMaterial);
            }
            if (arrowSprite != null)
            {
                if (Application.isPlaying) Destroy(arrowSprite); else DestroyImmediate(arrowSprite);
            }
            if (arrowTexture != null)
            {
                if (Application.isPlaying) Destroy(arrowTexture); else DestroyImmediate(arrowTexture);
            }
        }
    }
}
