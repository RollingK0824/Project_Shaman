using UnityEngine;

namespace ProjectShaman.AI
{
    // Ring above an object, facing the local camera: a dim full ring and a bright arc that fills clockwise from the top
    public class AI_ChannelGauge : MonoBehaviour
    {
        private const int SEGMENTS = 48;

        private LineRenderer _back;
        private LineRenderer _fill;
        private Vector3 _localPosition;
        private float _radius;
        private float _shown = -1f;

        public static AI_ChannelGauge Create(Transform parent, Vector3 localPosition, float radius, float width, Color backColor, Color fillColor)
        {
            GameObject gaugeObject = new GameObject("ChannelGauge");
            gaugeObject.transform.SetParent(parent, false);
            gaugeObject.transform.localPosition = localPosition;

            AI_ChannelGauge gauge = gaugeObject.AddComponent<AI_ChannelGauge>();
            gauge._localPosition = localPosition;
            gauge._radius = radius;
            gauge._back = CreateLine(gaugeObject.transform, "Back", width, backColor, 0);
            gauge._fill = CreateLine(gaugeObject.transform, "Fill", width, fillColor, 1);
            gauge.DrawArc(gauge._back, 1f);
            gaugeObject.SetActive(false);
            return gauge;
        }

        // Negative hides the gauge
        public void Show(float progress)
        {
            bool visible = progress >= 0f;
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }

            if (!visible)
            {
                _shown = -1f;
                return;
            }

            progress = Mathf.Clamp01(progress);
            if (!Mathf.Approximately(progress, _shown))
            {
                _shown = progress;
                DrawArc(_fill, progress);
            }
        }

        private void LateUpdate()
        {
            transform.localPosition = _localPosition;
            AI_ViewerCamera.Face(transform);
        }

        private void DrawArc(LineRenderer line, float amount)
        {
            int points = Mathf.Max(2, Mathf.CeilToInt(SEGMENTS * amount) + 1);
            line.positionCount = points;

            for (int i = 0; i < points; i++)
            {
                float angle = (90f - 360f * amount * i / (points - 1)) * Mathf.Deg2Rad;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * _radius);
            }
        }

        private static LineRenderer CreateLine(Transform parent, string name, float width, Color color, int order)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            // The gauge faces the camera along -Z, so a later ring sits a hair closer and never z-fights
            lineObject.transform.localPosition = new Vector3(0f, 0f, -0.01f * order);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = order;
            line.material = CreateMaterial(color, order);
            return line;
        }

        private static Material CreateMaterial(Color color, int order)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            // Fill draws after the back ring so it sits on top
            Material material = new Material(shader) { name = "Channel gauge (runtime)", color = color };
            material.renderQueue = 3000 + order;
            return material;
        }
    }
}
