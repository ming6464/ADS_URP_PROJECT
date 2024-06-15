using Unity.Mathematics;


public static class MathExt
{
    // Chuyển đổi từ float3 (góc Euler) sang quaternion
    public static quaternion Float3ToQuaternion(float3 euler)
    {
        // Sử dụng quaternion.EulerXYZ để chuyển đổi
        return quaternion.EulerXYZ(math.radians(euler));
    }

    // Chuyển đổi từ quaternion sang float3 (góc Euler)
    public static float3 QuaternionToFloat3(quaternion q)
    {
        // Sử dụng phương pháp chuyển đổi từ quaternion sang Euler angles
        return math.degrees(ToEulerAngles(q));
    }

    private static float3 ToEulerAngles(quaternion q)
    {
        float3 angles;

        // Roll (x-axis rotation)
        float sinr_cosp = 2 * (q.value.w * q.value.x + q.value.y * q.value.z);
        float cosr_cosp = 1 - 2 * (q.value.x * q.value.x + q.value.y * q.value.y);
        angles.x = math.atan2(sinr_cosp, cosr_cosp);

        // Pitch (y-axis rotation)
        float sinp = 2 * (q.value.w * q.value.y - q.value.z * q.value.x);
        if (math.abs(sinp) >= 1)
            angles.y = math.sign(sinp) * (math.PI / 2); // use 90 degrees if out of range
        else
            angles.y = math.asin(sinp);

        // Yaw (z-axis rotation)
        float siny_cosp = 2 * (q.value.w * q.value.z + q.value.x * q.value.y);
        float cosy_cosp = 1 - 2 * (q.value.y * q.value.y + q.value.z * q.value.z);
        angles.z = math.atan2(siny_cosp, cosy_cosp);

        return angles;
    }

    public static float4x4 TRSToMatrix(float3 position, quaternion rotation, float3 scale)
    {
        return float4x4.TRS(position, rotation, scale);
    }

    public static void MatrixToTRS(float4x4 matrix, out float3 position, out quaternion rotation, out float3 scale)
    {
        position = matrix.c3.xyz;
        rotation = quaternion.LookRotationSafe(matrix.c2.xyz, matrix.c1.xyz);
        scale = new float3(math.length(matrix.c0.xyz), math.length(matrix.c1.xyz), math.length(matrix.c2.xyz));
    }

    #region Random

    public static int GetRandomRange(this Random random, int min, int max)
    {
        return random.NextInt(min, max);
    }

    public static float3 GetRandomRange(this Random random, float3 min, float3 max)
    {
        return random.NextFloat3(min, max);
    }

    public static float2 GetRandomRange(this Random random, float2 min, float2 max)
    {
        return random.NextFloat2(min, max);
    }

    public static float GetRandomRange(this Random random, float min, float max)
    {
        return random.NextFloat(min, max);
    }

    public static int GetRandomRange(int min, int max)
    {
        return GetRandomProperty(GetSeedWithTime()).NextInt(min, max);
    }

    public static float3 GetRandomRange(uint seed, float3 min, float3 max)
    {
        return GetRandomProperty(seed).NextFloat3(min, max);
    }


    public static float3 GetRandomRange(float3 min, float3 max)
    {
        return GetRandomProperty(GetSeedWithTime()).NextFloat3(min, max);
    }

    public static float2 GetRandomRange(float2 min, float2 max)
    {
        return GetRandomProperty(GetSeedWithTime()).NextFloat2(min, max);
    }

    public static float GetRandomRange(float min, float max)
    {
        return GetRandomProperty(GetSeedWithTime()).NextFloat(min, max);
    }

    private static Random GetRandomProperty(uint seed)
    {
        return Random.CreateFromIndex(seed);
    }

    public static uint GetSeedWithTime()
    {
        long tick = GetTimeTick();

        if (tick > 255)
        {
            tick %= 255;
        }

        return (uint)tick;
    }

    public static long GetTimeTick()
    {
        return System.DateTime.Now.Ticks;
    }

    #endregion
}