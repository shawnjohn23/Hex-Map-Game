using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// Map generator job to initialize the map.
/// </summary>
[BurstCompile]
public struct InitializeMapJob : IJobFor
{
	NativeArray<HexCellData> cellData;

	int waterLevel;

	public static JobHandle Schedule(
		NativeArray<HexCellData> cellData,
		MapGeneratorSettings settings,
		JobHandle dependency) => new InitializeMapJob()
		{
			cellData = cellData,
			waterLevel = settings.waterLevel
		}.ScheduleParallel(cellData.Length, cellData.Length / 8, dependency);

	public void Execute(int index)
	{
		HexCellData data = cellData[index];
		data.values = data.values.WithWaterLevel(waterLevel);
		cellData[index] = data;
	}
}
