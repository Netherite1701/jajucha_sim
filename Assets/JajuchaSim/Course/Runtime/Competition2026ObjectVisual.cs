using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>Procedural print-scale visuals for the official 2026 signs.</summary>
    public sealed class Competition2026ObjectVisual : MonoBehaviour
    {
        private CourseObjectInstance _item;
        private Renderer[] _lamps;
        private Material _lampOff;
        private Material _lampOn;
        private StartSignalState _lastSignal = StartSignalState.Waiting;
        private AudioSource _audio;
        private Rigidbody _vehicle;
        private Vector3 _obstacleStart;
        private float _obstacleTimer = -1f;

        public float ObstacleWaitSec = 3f;
        public float ObstacleExitSec = 1f;
        public float ObstacleExitDistanceCm = 50f;

        public void Configure(CourseObjectInstance item)
        {
            _item = item;
            ObstacleWaitSec = item.ObstacleWaitSec > 0f ? item.ObstacleWaitSec : 3f;
            ObstacleExitSec = item.ObstacleExitSec > 0f ? item.ObstacleExitSec : 1f;
            switch (item.Type)
            {
                case ObjectType.StartSignal: BuildStartingLight(); break;
                case ObjectType.YellowFlag: BuildBoard("Competition2026/yellow_flag", 21f, 29.7f, false); break;
                case ObjectType.PitBarrier: BuildBoard("Competition2026/pit_barrier", 36.37f, 25.68f, true); break;
                case ObjectType.DynamicObstacle:
                    BuildBoard("Competition2026/dynamic_obstacle", 36.37f, 25.68f, true);
                    gameObject.AddComponent<BoxCollider>().size = new Vector3(36.37f, 25.68f, 1f);
                    _obstacleStart = transform.position;
                    break;
            }
        }

        private void BuildStartingLight()
        {
            BuildBoard("Competition2026/starting_light_sign", 8f, 16f, false, -5f);
            _lampOff = ColorMaterial(new Color(0.12f, 0.04f, 0.04f));
            _lampOn = ColorMaterial(new Color(1f, 0.05f, 0.02f));
            _lamps = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lamp.name = $"RedLamp{i + 1}";
                lamp.transform.SetParent(transform, false);
                lamp.transform.localScale = new Vector3(2.8f, 2.8f, 1.4f);
                lamp.transform.localPosition = new Vector3(2.5f, 3f + i * 3.2f, 0f);
                Object.Destroy(lamp.GetComponent<Collider>());
                _lamps[i] = lamp.GetComponent<Renderer>();
                _lamps[i].sharedMaterial = _lampOff;
            }
            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGo.name = "StartingLightBase";
            baseGo.transform.SetParent(transform, false);
            baseGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            baseGo.transform.localScale = new Vector3(8f, 2f, 6f);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.clip = BuildBuzzerClip();
        }

        private void BuildBoard(string resourcePath, float widthCm, float heightCm, bool collider, float xOffset = 0f)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "OfficialPrintArtwork";
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(xOffset, heightCm * 0.5f, 0f);
            quad.transform.localScale = new Vector3(widthCm, heightCm, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.mainTexture = Resources.Load<Texture2D>(resourcePath);
            renderer.sharedMaterial = material;
            var existing = quad.GetComponent<Collider>();
            if (existing != null && !collider) Object.Destroy(existing);
        }

        private void Update()
        {
            if (_item == null) return;
            if (_item.Type == ObjectType.StartSignal) UpdateStartingLight();
            if (_item.Type == ObjectType.DynamicObstacle) UpdateDynamicObstacle();
        }

        private void UpdateStartingLight()
        {
            int lit = (int)_item.SignalState >= (int)StartSignalState.Lamp1 && (int)_item.SignalState <= (int)StartSignalState.Lamp4
                ? (int)_item.SignalState : 0;
            for (int i = 0; i < (_lamps?.Length ?? 0); i++)
                _lamps[i].sharedMaterial = i < lit ? _lampOn : _lampOff;
            if (_item.SignalState == StartSignalState.Released && _lastSignal != StartSignalState.Released)
                _audio?.Play();
            _lastSignal = _item.SignalState;
        }

        private void UpdateDynamicObstacle()
        {
            if (_vehicle == null)
            {
                foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                    if (rb.name.IndexOf("Vehicle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rb.name.IndexOf("Jajucha", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    { _vehicle = rb; break; }
            }
            if (_vehicle == null) return;
            if (_obstacleTimer < 0f && Vector3.Distance(_vehicle.position, transform.position) <= 60f)
                _obstacleTimer = 0f;
            if (_obstacleTimer < 0f) return;
            _obstacleTimer += Time.deltaTime;
            float t = Mathf.Clamp01((_obstacleTimer - ObstacleWaitSec) / Mathf.Max(0.01f, ObstacleExitSec));
            // The sign's local forward axis is perpendicular to the road for
            // the printed 2026 candidate rotations (0° for east/west and 90°
            // for north/south).  Moving along local right would send the
            // obstacle down the lane instead of out of the road.
            transform.position = _obstacleStart + transform.forward * (ObstacleExitDistanceCm * t);
        }

        private static Material ColorMaterial(Color color)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            return new Material(shader) { color = color };
        }

        private static AudioClip BuildBuzzerClip()
        {
            const int rate = 22050;
            const int count = rate;
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = Mathf.Sin(2f * Mathf.PI * 880f * i / rate) * 0.2f;
            var clip = AudioClip.Create("2026StartBuzzer", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
