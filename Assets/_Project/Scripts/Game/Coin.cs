using System;
using UnityEngine;

namespace PoliceChase.Game
{
    /// <summary>Vật phẩm nhặt được: xu vàng (giá trị nhỏ, nhiều) hoặc túi tiền (giá trị lớn, hiếm).</summary>
    public class Coin : MonoBehaviour
    {
        public int value = 5;
        public float spinSpeed = 180f;
        public float bobHeight = 0.25f;

        public static event Action<Coin, int> Collected;

        Vector3 basePos;
        float phase;
        bool taken;
        float popTimer;

        void Start()
        {
            basePos = transform.position;
            phase = UnityEngine.Random.value * 10f;
        }

        void Update()
        {
            if (taken)
            {
                popTimer += Time.deltaTime;
                transform.localScale = Vector3.one * (1f + popTimer * 6f);
                if (popTimer > 0.15f) Destroy(gameObject);
                return;
            }
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 3f + phase) * bobHeight;
        }

        void OnTriggerEnter(Collider other)
        {
            if (taken) return;
            var go = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!go.CompareTag("Player")) return;
            taken = true;
            Collected?.Invoke(this, value);
        }
    }
}
