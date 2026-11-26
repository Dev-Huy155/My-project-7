using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PoliceChase.UI;
using PoliceChase.Vehicles;

namespace PoliceChase.Game
{
    /// <summary>
    /// Điểm năng lượng tên lửa: quả cầu xanh có tia sét + cột sáng cao (nhìn thấy từ xa) + hiện trên bản đồ nhỏ.
    /// Chạy xe qua để nạp năng lượng tăng tốc.
    /// </summary>
    public class BoostPickup : MonoBehaviour
    {
        public static readonly List<BoostPickup> All = new List<BoostPickup>();
        public static event Action<BoostPickup> Collected;

        [Tooltip("Phần năng lượng được nạp (1 = đầy bình)")]
        [Range(0.1f, 1f)] public float refillFraction = 0.6f;

        public bool Taken { get; private set; }
        Transform bolt, halo, core;
        Vector3 basePos;
        float phase, popTimer;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            basePos = transform.position;
            phase = UnityEngine.Random.value * 10f;
        }

        void Update()
        {
            if (Taken)
            {
                popTimer += Time.deltaTime;
                transform.localScale = Vector3.one * (1f + popTimer * 5f);
                if (popTimer > 0.2f) Destroy(gameObject);
                return;
            }
            float t = Time.time + phase;
            if (bolt != null) bolt.Rotate(0f, 160f * Time.deltaTime, 0f, Space.World);
            if (core != null) core.localScale = Vector3.one * (1.1f + 0.1f * Mathf.Sin(t * 6f));
            transform.position = basePos + Vector3.up * Mathf.Sin(t * 2.5f) * 0.3f;
            var cam = Camera.main;
            if (halo != null && cam != null) halo.rotation = cam.transform.rotation;
        }

        void OnTriggerEnter(Collider other)
        {
            if (Taken) return;
            var go = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!go.CompareTag("Player")) return;
            var nitro = go.GetComponent<CarNitro>();
            if (nitro != null && nitro.enabled) nitro.AddCharge(nitro.capacitySeconds * refillFraction);
            Taken = true;
            Collected?.Invoke(this);
            var game = FindFirstObjectByType<GameplayBootstrap>();
            if (game != null) game.Toast(nitro != null && nitro.Charge01 >= 0.999f ? "TÊN LỬA ĐẦY!" : "+ NĂNG LƯỢNG TÊN LỬA");
        }

        /// <summary>Dựng 1 điểm năng lượng tại vị trí (mặt đất).</summary>
        public static BoostPickup Create(Vector3 groundPos, Transform parent)
        {
            var root = new GameObject("BoostPickup");
            root.transform.SetParent(parent, false);
            root.transform.position = groundPos + Vector3.up * 1.8f;

            Color cyan = new Color(0.25f, 0.8f, 1f);
            var p = root.AddComponent<BoostPickup>();

            // Lõi phát sáng
            p.core = Prim(root.transform, PrimitiveType.Sphere, RocketBoostFX.Unlit(new Color(0.4f, 1.4f, 2.2f, 0.75f)), Vector3.zero, Vector3.one * 1.1f);
            // Quầng sáng luôn quay về camera
            var haloSr = new GameObject("Halo").AddComponent<SpriteRenderer>();
            haloSr.transform.SetParent(root.transform, false);
            haloSr.sprite = GameSkin.SoftDot;
            haloSr.color = new Color(cyan.r, cyan.g, cyan.b, 0.7f);
            haloSr.transform.localScale = Vector3.one * 3.2f;
            haloSr.sortingOrder = -1;
            p.halo = haloSr.transform;
            // Tia sét xoay
            var boltSr = new GameObject("Bolt").AddComponent<SpriteRenderer>();
            boltSr.transform.SetParent(root.transform, false);
            boltSr.sprite = UIKit.Icon("bolt");
            boltSr.color = new Color(1f, 0.85f, 0.2f);
            boltSr.transform.localScale = Vector3.one * 1.6f;
            boltSr.sortingOrder = 5;
            p.bolt = boltSr.transform;
            // Cột sáng cao để nhìn thấy từ xa
            Prim(root.transform, PrimitiveType.Cylinder, RocketBoostFX.Unlit(new Color(0.3f, 0.85f, 1f, 0.16f)), new Vector3(0f, 14f, 0f), new Vector3(0.7f, 16f, 0.7f));
            // Vòng sáng dưới đất
            var ring = new GameObject("Ring").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(root.transform, false);
            ring.sprite = GameSkin.FloorGlow;
            ring.color = new Color(cyan.r, cyan.g, cyan.b, 0.85f);
            ring.transform.localPosition = new Vector3(0f, -1.7f, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = Vector3.one * 7f;

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 3.2f;
            return p;
        }

        static Transform Prim(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }
    }

    /// <summary>
    /// Rải các điểm năng lượng tên lửa quanh người chơi trên đường (NavMesh). Nhặt xong thì xuất hiện chỗ khác.
    /// </summary>
    public class BoostSpawner : MonoBehaviour
    {
        public Transform player;
        public int count = 6;
        public float minDistance = 70f;
        public float maxDistance = 260f;
        public float despawnDistance = 450f;
        [Tooltip("Khoảng cách tối thiểu giữa 2 điểm")]
        public float spacing = 60f;
        public float respawnDelay = 4f;
        [Range(0.1f, 1f)] public float refillFraction = 0.6f;

        readonly List<BoostPickup> pickups = new List<BoostPickup>();
        float nextSpawn;

        void Start()
        {
            if (player == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) player = p.transform;
            }
            BoostPickup.Collected += OnCollected;
        }

        void OnDestroy() => BoostPickup.Collected -= OnCollected;
        void OnCollected(BoostPickup p) => nextSpawn = Time.time + respawnDelay;

        void Update()
        {
            if (player == null) return;
            pickups.RemoveAll(p => p == null || p.Taken);
            Vector3 pp = player.position;
            for (int i = pickups.Count - 1; i >= 0; i--)
                if ((pickups[i].transform.position - pp).sqrMagnitude > despawnDistance * despawnDistance)
                {
                    Destroy(pickups[i].gameObject);
                    pickups.RemoveAt(i);
                }

            // Lúc đầu rải ngay, sau đó mỗi lần nhặt thì chờ respawnDelay.
            if (pickups.Count < count && (Time.time >= nextSpawn || Time.timeSinceLevelLoad < 3f))
                TrySpawn(pp);
        }

        void TrySpawn(Vector3 center)
        {
            for (int t = 0; t < 6; t++)
            {
                Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
                float dist = UnityEngine.Random.Range(minDistance, maxDistance);
                Vector3 probe = center + new Vector3(dir.x, 0f, dir.y) * dist;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 15f, NavMesh.AllAreas)) continue;
                bool crowded = false;
                foreach (var p in pickups)
                    if ((p.transform.position - hit.position).sqrMagnitude < spacing * spacing) { crowded = true; break; }
                if (crowded) continue;
                var pk = BoostPickup.Create(hit.position, transform);
                pk.refillFraction = refillFraction;
                pickups.Add(pk);
                return;
            }
        }
    }
}
