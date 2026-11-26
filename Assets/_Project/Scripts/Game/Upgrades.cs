using UnityEngine;
using PoliceChase.Vehicles;

namespace PoliceChase.Game
{
    public enum UpgradeType { Engine, Handling, Durability, Nitro }

    public enum GameMode { Chase, FreeRoam }

    /// <summary>Chỉ số cuối cùng của 1 xe sau khi cộng nâng cấp.</summary>
    public struct CarStats
    {
        public float maxSpeed, acceleration, turnRate, grip, mass;
        public float arrestTimeMultiplier;   // càng lớn càng lâu bị bắt
        public float nitroSeconds;           // 0 = chưa có nitro
    }

    /// <summary>Luật nâng cấp: cấp tối đa, giá, và mỗi cấp tăng bao nhiêu.</summary>
    public static class UpgradeRules
    {
        public const int MaxLevel = 5;

        public static string Name(UpgradeType t)
        {
            switch (t)
            {
                case UpgradeType.Engine: return "Động cơ";
                case UpgradeType.Handling: return "Lái và bám đường";
                case UpgradeType.Durability: return "Độ bền";
                default: return "Nitro";
            }
        }

        public static string Description(UpgradeType t)
        {
            switch (t)
            {
                case UpgradeType.Engine: return "Tăng tốc độ tối đa và độ bốc";
                case UpgradeType.Handling: return "Bẻ lái nhạy, bám đường khi vào cua";
                case UpgradeType.Durability: return "Xe chắc hơn, cảnh sát khó bắt hơn";
                default: return "Tăng tốc tức thì, dùng được lâu hơn";
            }
        }

        /// <summary>Giá để lên cấp tiếp theo (level = cấp hiện tại).</summary>
        public static int Cost(CarDef car, UpgradeType t, int level)
        {
            float basePrice = 150f + (car != null ? car.price : 0) * 0.1f;
            if (t == UpgradeType.Nitro) basePrice *= 1.2f;
            return Mathf.RoundToInt(basePrice * (level + 1) / 10f) * 10;
        }

        public static CarStats Compute(CarDef car, CarUpgrades up)
        {
            int e = up != null ? up.engine : 0, h = up != null ? up.handling : 0;
            int d = up != null ? up.durability : 0, n = up != null ? up.nitro : 0;
            return new CarStats
            {
                maxSpeed = car.maxSpeed * (1f + 0.06f * e),
                acceleration = car.acceleration * (1f + 0.08f * e),
                turnRate = car.turnRate * (1f + 0.06f * h),
                grip = car.grip * (1f + 0.10f * h),
                mass = car.mass * (1f + 0.15f * d),
                arrestTimeMultiplier = 1f + 0.25f * d,
                nitroSeconds = 3f + 0.75f * n,   // 3 giây tên lửa cơ bản + mỗi cấp 0.75 giây
            };
        }

        /// <summary>Gán chỉ số vào CarController (và Rigidbody nếu xe đã chạy).</summary>
        public static void Apply(CarStats s, CarController car)
        {
            car.maxSpeed = s.maxSpeed;
            car.acceleration = s.acceleration;
            car.turnRate = s.turnRate;
            car.grip = s.grip;
            car.mass = s.mass;
            var rb = car.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = s.mass;
            // Lò xo giảm xóc tỉ lệ theo khối lượng để xe nặng không bị lún.
            car.springStrength = 35000f * s.mass / 1200f;
            car.springDamper = 4000f * Mathf.Sqrt(s.mass / 1200f);
        }
    }

    /// <summary>Thông tin truyền từ menu sang màn chơi.</summary>
    public static class GameSession
    {
        public static GameMode Mode = GameMode.Chase;
        public static int RunCoins;
        /// <summary>Thời gian 1 ván (giây). 0 = không giới hạn. -1 = chưa chọn (dùng mặc định của scene).</summary>
        public static float RoundSeconds = -1f;
    }
}
