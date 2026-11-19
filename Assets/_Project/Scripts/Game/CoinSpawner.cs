using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace PoliceChase.Game
{
    /// <summary>
    /// Rải xu quanh xe người chơi trên mặt đường (NavMesh): luôn giữ khoảng "maxCoins" xu trong vòng bán kính,
    /// xu ở quá xa thì thu hồi. Không cần model – tự dựng xu vàng và túi tiền bằng khối cơ bản.
    /// </summary>
    public class CoinSpawner : MonoBehaviour
    {
        public Transform player;
        public int maxCoins = 40;
        public float minDistance = 25f;
        public float maxDistance = 220f;
        public float despawnDistance = 320f;
        public int coinValue = 5;
        public int bagValue = 50;
        [Range(0f, 1f)] public float bagChance = 0.08f;
        public float spawnPerSecond = 10f;

        readonly List<Coin> coins = new List<Coin>();
        Material goldMat, bagMat;
        float spawnBudget;

        void Start()
        {
            if (player == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) player = p.transform;
            }
            goldMat = Mat(new Color(1f, 0.78f, 0.1f), 0.8f);
            bagMat = Mat(new Color(0.2f, 0.7f, 0.25f), 0.2f);
        }

        void Update()
        {
            if (player == null) return;
            coins.RemoveAll(c => c == null);

            Vector3 pp = player.position;
            for (int i = coins.Count - 1; i >= 0; i--)
                if ((coins[i].transform.position - pp).sqrMagnitude > despawnDistance * despawnDistance)
                {
                    Destroy(coins[i].gameObject);
                    coins.RemoveAt(i);
                }

            spawnBudget = Mathf.Min(spawnBudget + spawnPerSecond * Time.deltaTime, maxCoins);
            int tries = 0;
            while (coins.Count < maxCoins && spawnBudget >= 1f && tries++ < 5)
            {
                if (TrySpawn(pp)) spawnBudget -= 1f;
            }
        }

        bool TrySpawn(Vector3 center)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            float dist = UnityEngine.Random.Range(minDistance, maxDistance);
            Vector3 probe = center + new Vector3(dir.x, 0f, dir.y) * dist;
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 12f, NavMesh.AllAreas)) return false;

            bool bag = UnityEngine.Random.value < bagChance;
            var coin = Build(hit.position + Vector3.up * 1.2f, bag);
            coins.Add(coin);
            return true;
        }

        Coin Build(Vector3 pos, bool bag)
        {
            var root = new GameObject(bag ? "MoneyBag" : "Coin");
            root.transform.SetParent(transform, false);
            root.transform.position = pos;

            var visual = GameObject.CreatePrimitive(bag ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            if (bag)
            {
                visual.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                visual.GetComponent<MeshRenderer>().sharedMaterial = bagMat;
                var knot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(knot.GetComponent<Collider>());
                knot.transform.SetParent(root.transform, false);
                knot.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                knot.transform.localScale = Vector3.one * 0.5f;
                knot.GetComponent<MeshRenderer>().sharedMaterial = goldMat;
            }
            else
            {
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = new Vector3(1.2f, 0.08f, 1.2f);
                visual.GetComponent<MeshRenderer>().sharedMaterial = goldMat;
            }

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = bag ? 2.5f : 2.2f;

            var coin = root.AddComponent<Coin>();
            coin.value = bag ? bagValue : coinValue;
            return coin;
        }

        static Material Mat(Color c, float metallic)
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            var shader = Shader.Find(srp ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 0.35f);
            }
            return m;
        }
    }
}
