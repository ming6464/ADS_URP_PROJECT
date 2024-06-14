using Unity.Mathematics;


public static class MathExt
{
    public static quaternion Float3ToQuaternion(float3 euler)
    {
        return quaternion.EulerXYZ(euler);
    }

    public static float3 QuaternionToFloat3(quaternion q)
    {
        return math.degrees(math.EulerXYZ(q));
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