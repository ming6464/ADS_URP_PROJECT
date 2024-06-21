using Unity.Mathematics;
using Unity.Transforms;

public static class DotsEX
{
    /// <summary>
    /// Tạo thành phần LocalToWorld từ vị trí, xoay, và tỷ lệ
    /// </summary>
    public static LocalToWorld GetComponentWorldTf(float3 position, quaternion rotation, float scale)
    {
        return new LocalToWorld()
        {
            Value = MathExt.TRSToMatrix(position, rotation, new float3(1, 1, 1) * scale),
        };
    }

    /// <summary>
    /// Tạo thành phần LocalTransform từ vị trí, xoay, và tỷ lệ
    /// </summary>
    public static LocalTransform GetComponentLocalTf(float3 position, quaternion rotation, float scale)
    {
        return new LocalTransform()
        {
            Position = position,
            Rotation = rotation,
            Scale = scale,
        };
    }

    /// <summary>
    /// Chuyển đổi dữ liệu từ LocalTransform sang LocalToWorld
    /// </summary>
    public static LocalToWorld ConvertDataLocalToWorldTf(LocalTransform lt)
    {
        return GetComponentWorldTf(lt.Position, lt.Rotation, lt.Scale);
    }

    /// <summary>
    /// Chuyển đổi dữ liệu từ LocalToWorld sang LocalTransform
    /// </summary>
    public static LocalTransform ConvertDataWorldToLocalTf(LocalToWorld ltw)
    {
        return GetComponentLocalTf(ltw.Position, ltw.Rotation, ltw.Value.c0.x);
    }
}

public static class MathExt
{
    /// <summary>
    /// Chuyển đổi float3 thành quaternion
    /// </summary>
    public static quaternion Float3ToQuaternion(float3 euler)
    {
        return quaternion.EulerXYZ(math.radians(euler));
    }

    /// <summary>
    /// Chuyển đổi quaternion thành float3
    /// </summary>
    public static float3 QuaternionToFloat3(quaternion q)
    {
        return math.degrees(ToEulerAngles(q));
    }

    /// <summary>
    /// Chuyển đổi quaternion thành góc Euler
    /// </summary>
    private static float3 ToEulerAngles(quaternion q)
    {
        float3 angles;

        // Roll (xoay trục x)
        float sinr_cosp = 2 * (q.value.w * q.value.x + q.value.y * q.value.z);
        float cosr_cosp = 1 - 2 * (q.value.x * q.value.x + q.value.y * q.value.y);
        angles.x = math.atan2(sinr_cosp, cosr_cosp);

        // Pitch (xoay trục y)
        float sinp = 2 * (q.value.w * q.value.y - q.value.z * q.value.x);
        if (math.abs(sinp) >= 1)
            angles.y = math.sign(sinp) * (math.PI / 2); // sử dụng 90 độ nếu ngoài phạm vi
        else
            angles.y = math.asin(sinp);

        // Yaw (xoay trục z)
        float siny_cosp = 2 * (q.value.w * q.value.z + q.value.x * q.value.y);
        float cosy_cosp = 1 - 2 * (q.value.y * q.value.y + q.value.z * q.value.z);
        angles.z = math.atan2(siny_cosp, cosy_cosp);

        return angles;
    }

    /// <summary>
    /// Tạo một ma trận từ vị trí, xoay và tỷ lệ
    /// </summary>
    public static float4x4 TRSToMatrix(float3 position, quaternion rotation, float3 scale)
    {
        return float4x4.TRS(position, rotation, scale);
    }

    /// <summary>
    /// Chuyển đổi ma trận thành vị trí, xoay và tỷ lệ
    /// </summary>
    public static void MatrixToTRS(float4x4 matrix, out float3 position, out quaternion rotation, out float3 scale)
    {
        position = matrix.c3.xyz;
        rotation = quaternion.LookRotationSafe(matrix.c2.xyz, matrix.c1.xyz);
        scale = new float3(math.length(matrix.c0.xyz), math.length(matrix.c1.xyz), math.length(matrix.c2.xyz));
    }

    #region Random

    /// <summary>
    /// Lấy giá trị ngẫu nhiên trong phạm vi cho trước
    /// </summary>
    public static int GetRandomRange(this Random random, int min, int max)
    {
        return random.NextInt(min, max);
    }

    /// <summary>
    /// Lấy giá trị float3 ngẫu nhiên trong phạm vi cho trước
    /// </summary>
    public static float3 GetRandomRange(this Random random, float3 min, float3 max)
    {
        return random.NextFloat3(min, max);
    }

    /// <summary>
    /// Lấy giá trị float2 ngẫu nhiên trong phạm vi cho trước
    /// </summary>
    public static float2 GetRandomRange(this Random random, float2 min, float2 max)
    {
        return random.NextFloat2(min, max);
    }

    /// <summary>
    /// Lấy giá trị float ngẫu nhiên trong phạm vi cho trước
    /// </summary>
    public static float GetRandomRange(this Random random, float min, float max)
    {
        return random.NextFloat(min, max);
    }

    /// <summary>
    /// Lấy giá trị ngẫu nhiên trong phạm vi cho trước
    /// </summary>
    public static int GetRandomRange(int min, int max)
    {
        return GetRandomProperty(GetSeedWithTime()).NextInt(min, max);
    }

    /// <summary>
    /// Lấy giá trị float3 ngẫu nhiên từ seed và phạm vi cho trước
    /// </summary>
    public static float3 GetRandomRange(uint seed, float3 min, float3 max)
    {
        return GetRandomProperty(seed).NextFloat3(min, max);
    }

    /// <summary>
    /// Lấy giá trị float3 ngẫu nhiên trong phạm vi cho trước
    /// </summary>
    public static float3 GetRandomRange(float3 min, float3 max)
    {
        return GetRandomProperty(GetSeedWithTime()).NextFloat3(min, max);
    }

    /// <summary>
    /// Lấy giá trị float2 ngẫu nhiên trong phạm vi cho trước
    /// </summary>
    public static float2 GetRandomRange(float2 min, float2 max)
    {
        return GetRandomProperty(GetSeedWithTime()).NextFloat2(min, max);
    }

    /// <summary>
    /// Lấy giá trị float ngẫu nhiên trong phạm vi cho trước
    /// </summary>
    public static float GetRandomRange(float min, float max)
    {
        return GetRandomProperty(GetSeedWithTime()).NextFloat(min, max);
    }

    /// <summary>
    /// Tạo một Random từ seed
    /// </summary>
    private static Random GetRandomProperty(uint seed)
    {
        return Random.CreateFromIndex(seed);
    }

    /// <summary>
    /// Lấy seed từ thời gian hiện tại
    /// </summary>
    public static uint GetSeedWithTime()
    {
        long tick = GetTimeTick();

        if (tick > 255)
        {
            tick %= 255;
        }

        return (uint)tick;
    }

    /// <summary>
    /// Lấy số tick của thời gian hiện tại
    /// </summary>
    public static long GetTimeTick()
    {
        return System.DateTime.Now.Ticks;
    }

    #endregion

    #region MyRegion
    
    /// <summary>
    /// Di chuyển float3 từ vị trí hiện tại đến mục tiêu với một bước nhất định.
    /// </summary>
    /// <param name="current">Vị trí hiện tại.</param>
    /// <param name="target">Vị trí mục tiêu.</param>
    /// <param name="step">Bước di chuyển tối đa cho mỗi lần cập nhật.</param>
    /// <returns>float3 mới sau khi di chuyển.</returns>
    public static float3 MoveTowards(float3 current, float3 target, float step)
    {
        float3 direction = target - current;
        float distance = math.length(direction);
        if (distance <= step || distance == 0f)
            return target;
        return current + math.normalize(direction) * step;
    }
    
    /// <summary>
    /// Di chuyển float2 từ vị trí hiện tại đến mục tiêu với một bước nhất định.
    /// </summary>
    /// <param name="current">Vị trí hiện tại.</param>
    /// <param name="target">Vị trí mục tiêu.</param>
    /// <param name="step">Bước di chuyển tối đa cho mỗi lần cập nhật.</param>
    /// <returns>float2 mới sau khi di chuyển.</returns>
    public static float2 MoveTowards(float2 current, float2 target, float step)
    {
        float2 direction = target - current;
        float distance = math.length(direction);
        if (distance <= step || distance == 0f)
            return target;
        return current + math.normalize(direction) * step;
    }
    
    /// <summary>
    /// Di chuyển quaternion từ giá trị hiện tại đến mục tiêu với một góc quay nhất định.
    /// </summary>
    /// <param name="current">Quaternion hiện tại.</param>
    /// <param name="target">Quaternion mục tiêu.</param>
    /// <param name="maxDegreesDelta">Góc quay tối đa cho mỗi lần cập nhật.</param>
    /// <returns>quaternion mới sau khi quay.</returns>
    public static quaternion MoveTowards(quaternion current, quaternion target, float maxDegreesDelta)
    {
        float angle = math.degrees(math.angle(current, target));
        if (angle <= maxDegreesDelta)
            return target;
        return math.slerp(current, target, maxDegreesDelta / angle);
    }
    
    /// <summary>
    /// Di chuyển int từ giá trị hiện tại đến mục tiêu với một bước nhất định.
    /// </summary>
    /// <param name="current">Giá trị hiện tại.</param>
    /// <param name="target">Giá trị mục tiêu.</param>
    /// <param name="step">Bước di chuyển tối đa cho mỗi lần cập nhật.</param>
    /// <returns>int mới sau khi di chuyển.</returns>
    public static int MoveTowards(int current, int target, int step)
    {
        int difference = target - current;
        if (math.abs(difference) <= step)
            return target;
        return current + (int)math.sign(difference) * step;
    }

    /// <summary>
    /// Di chuyển float từ giá trị hiện tại đến mục tiêu với một bước nhất định.
    /// </summary>
    /// <param name="current">Giá trị hiện tại.</param>
    /// <param name="target">Giá trị mục tiêu.</param>
    /// <param name="step">Bước di chuyển tối đa cho mỗi lần cập nhật.</param>
    /// <returns>float mới sau khi di chuyển.</returns>
    public static float MoveTowards(float current, float target, float step)
    {
        float difference = target - current;
        if (math.abs(difference) <= step)
            return target;
        return current + math.sign(difference) * step;
    }
    
    #endregion Interpolate
    
}
