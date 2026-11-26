namespace PoliceChase.Vehicles
{
    /// <summary>
    /// Trạng thái các nút cảm ứng trên điện thoại (do GameHUD cập nhật).
    /// PlayerCarInput và CarNitro cộng giá trị này với bàn phím / tay cầm.
    /// </summary>
    public static class MobileInput
    {
        public static float Throttle;   // -1 (phanh/lùi) .. 1 (ga)
        public static float Steer;      // -1 (trái) .. 1 (phải)
        public static bool Handbrake;
        public static bool Nitro;

        public static void Reset()
        {
            Throttle = 0f;
            Steer = 0f;
            Handbrake = false;
            Nitro = false;
        }
    }
}
