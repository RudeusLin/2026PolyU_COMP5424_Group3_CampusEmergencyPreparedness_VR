using UnityEngine;

namespace CampusEmergency.Prototype
{
    [ExecuteAlways]
    public sealed class PrototypeSceneBootstrap : MonoBehaviour
    {
        public const int LabelFontSize = 64;

        [SerializeField, HideInInspector] private bool textSizesNormalized;
        private Font labelFont;
        private Material labelMaterial;

        private void OnEnable()
        {
            Font.textureRebuilt += RefreshFontTexture;
            InitializeText();
        }

        private void Start()
        {
            InitializeText();
        }

        public void InitializeText()
        {
            TextMesh[] labels = GetComponentsInChildren<TextMesh>(true);
            if (labels.Length == 0) return;
            if (labelFont == null)
            {
                try { labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch (System.ArgumentException) { labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            }
            if (labelMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("WorldSpaceText");
                if (shader == null)
                {
                    Debug.LogError("Missing Resources/WorldSpaceText shader.", this);
                    return;
                }
                labelMaterial = new Material(shader)
                {
                    name = "TeachingBuildingWorldText",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            foreach (TextMesh text in labels)
            {
                text.font = labelFont;
                text.fontSize = LabelFontSize;
                // The layout stores a world font size; TextMesh uses font pixels / 10.
                if (!textSizesNormalized) text.characterSize *= 10f / LabelFontSize;
                labelFont.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                MeshRenderer renderer = text.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = labelMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            textSizesNormalized = true;
            RefreshFontTexture(labelFont);
        }

        public void InitializeNormalizedText()
        {
            textSizesNormalized = true;
            InitializeText();
        }

        private void RefreshFontTexture(Font rebuiltFont)
        {
            if (rebuiltFont == labelFont && labelMaterial != null)
                labelMaterial.mainTexture = labelFont.material.mainTexture;
        }

        private void OnDisable()
        {
            Font.textureRebuilt -= RefreshFontTexture;
        }

        private void OnDestroy()
        {
            // Disabling the initializer must not invalidate materials on visible labels.
            if (labelMaterial == null) return;
            if (Application.isPlaying) Destroy(labelMaterial);
            else DestroyImmediate(labelMaterial);
            labelMaterial = null;
        }
    }
}
