using System.Collections.Generic;
using UnityEngine;

namespace PoliceChase.Game
{
    /// <summary>Thông tin 1 chiếc xe trong garage.</summary>
    [System.Serializable]
    public class CarDef
    {
        [Tooltip("Mã xe, không trùng nhau, không đổi sau khi đã phát hành (dùng để lưu game)")]
        public string id = "car_01";
        public string displayName = "Xe 1";
        [Tooltip("Model / prefab xe (vd. Car_06 trong bộ Cartoon City)")]
        public GameObject model;
        [Tooltip("Giá mua (xu). 0 = có sẵn từ đầu")]
        public int price = 0;

        [Header("Chỉ số gốc (chưa nâng cấp)")]
        [Tooltip("Tốc độ tối đa (m/s). 35 ≈ 126 km/h")]
        public float maxSpeed = 32f;
        public float acceleration = 13f;
        public float turnRate = 105f;
        public float grip = 9f;
        public float mass = 1200f;
    }

    /// <summary>
    /// Danh sách tất cả xe trong game. Đặt file ở Assets/_Project/Resources/CarCatalog.asset
    /// để cả menu lẫn màn chơi tự tìm thấy (dùng Tools > Police Chase > Tạo danh sách xe tự động).
    /// </summary>
    [CreateAssetMenu(menuName = "Police Chase/Car Catalog", fileName = "CarCatalog")]
    public class CarCatalog : ScriptableObject
    {
        public List<CarDef> cars = new List<CarDef>();
        [Tooltip("Số xu khi chơi lần đầu")]
        public int startingCoins = 0;

        static CarCatalog cached;

        /// <summary>Tìm CarCatalog trong thư mục Resources.</summary>
        public static CarCatalog Load()
        {
            if (cached == null) cached = Resources.Load<CarCatalog>("CarCatalog");
            if (cached == null)
                Debug.LogWarning("[CarCatalog] Không tìm thấy Assets/_Project/Resources/CarCatalog.asset. " +
                                 "Dùng Tools > Police Chase > Tạo danh sách xe tự động.");
            return cached;
        }

        public CarDef Find(string id)
        {
            foreach (var c in cars) if (c != null && c.id == id) return c;
            return cars.Count > 0 ? cars[0] : null;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < cars.Count; i++) if (cars[i] != null && cars[i].id == id) return i;
            return 0;
        }
    }
}
