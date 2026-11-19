using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoliceChase.Game
{
    [Serializable]
    public class CarUpgrades
    {
        public string carId;
        public int engine, handling, durability, nitro;

        public int Get(UpgradeType t)
        {
            switch (t)
            {
                case UpgradeType.Engine: return engine;
                case UpgradeType.Handling: return handling;
                case UpgradeType.Durability: return durability;
                default: return nitro;
            }
        }

        public void Set(UpgradeType t, int v)
        {
            switch (t)
            {
                case UpgradeType.Engine: engine = v; break;
                case UpgradeType.Handling: handling = v; break;
                case UpgradeType.Durability: durability = v; break;
                default: nitro = v; break;
            }
        }
    }

    [Serializable]
    public class SaveData
    {
        public int coins;
        public string selectedCar;
        public List<string> owned = new List<string>();
        public List<CarUpgrades> upgrades = new List<CarUpgrades>();
        public float bestSurvivalSeconds;
        public bool initialized;
    }

    /// <summary>
    /// Lưu tiến trình (xu, xe đã mua, xe đang chọn, cấp nâng cấp) bằng JSON trong PlayerPrefs.
    /// Mọi thay đổi được lưu ngay lập tức.
    /// </summary>
    public static class SaveSystem
    {
        const string Key = "PoliceChase_Save_v1";
        static SaveData data;

        public static event Action Changed;

        public static SaveData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static int Coins => Data.coins;

        static void Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            if (data == null) data = new SaveData();
            EnsureDefaults();
        }

        /// <summary>Lần đầu chơi: tặng xu khởi đầu + xe miễn phí (giá 0).</summary>
        static void EnsureDefaults()
        {
            var catalog = CarCatalog.Load();
            bool dirty = false;
            if (!data.initialized)
            {
                data.initialized = true;
                if (catalog != null) data.coins = catalog.startingCoins;
                dirty = true;
            }
            if (catalog != null)
            {
                foreach (var c in catalog.cars)
                    if (c != null && c.price <= 0 && !data.owned.Contains(c.id)) { data.owned.Add(c.id); dirty = true; }
                if (string.IsNullOrEmpty(data.selectedCar) || !data.owned.Contains(data.selectedCar))
                {
                    data.selectedCar = data.owned.Count > 0 ? data.owned[0] : (catalog.cars.Count > 0 ? catalog.cars[0].id : null);
                    dirty = true;
                }
            }
            if (dirty) Save();
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void AddCoins(int amount)
        {
            if (amount == 0) return;
            Data.coins = Mathf.Max(0, Data.coins + amount);
            Save();
        }

        public static bool Owns(string carId) => Data.owned.Contains(carId);

        public static bool TryBuy(CarDef car)
        {
            if (car == null || Owns(car.id) || Data.coins < car.price) return false;
            Data.coins -= car.price;
            Data.owned.Add(car.id);
            Save();
            return true;
        }

        public static void Select(string carId)
        {
            if (!Owns(carId)) return;
            Data.selectedCar = carId;
            Save();
        }

        public static CarUpgrades GetUpgrades(string carId)
        {
            foreach (var u in Data.upgrades) if (u.carId == carId) return u;
            var n = new CarUpgrades { carId = carId };
            Data.upgrades.Add(n);
            return n;
        }

        public static bool TryUpgrade(CarDef car, UpgradeType type)
        {
            if (car == null || !Owns(car.id)) return false;
            var up = GetUpgrades(car.id);
            int level = up.Get(type);
            if (level >= UpgradeRules.MaxLevel) return false;
            int cost = UpgradeRules.Cost(car, type, level);
            if (Data.coins < cost) return false;
            Data.coins -= cost;
            up.Set(type, level + 1);
            Save();
            return true;
        }

        public static void RecordSurvival(float seconds)
        {
            if (seconds <= Data.bestSurvivalSeconds) return;
            Data.bestSurvivalSeconds = seconds;
            Save();
        }

        /// <summary>Xoá toàn bộ tiến trình (dùng khi thử nghiệm).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            data = null;
            Load();
            Changed?.Invoke();
        }
    }
}
