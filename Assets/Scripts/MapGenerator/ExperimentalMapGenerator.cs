using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Experimental map generator that will replace HexMapGenerator.
/// </summary>
public class ExperimentalMapGenerator
{
	public HexGrid grid;

	public MapGeneratorSettings settings;

	/// <summary>
	/// Generate a random hex map.
	/// </summary>
	/// <param name="x">X size of the map.</param>
	/// <param name="z">Z size of the map.</param>
	/// <param name="wrapping">Whether east-west wrapping is enabled.</param>
	public void GenerateMap(int x, int z, bool wrapping)
	{
		HexMapInfo info = new(x, z, wrapping);
		Random.State originalRandomState = Random.state;
		if (!settings.useFixedSeed)
		{
			settings.seed = Random.Range(0, int.MaxValue);
			settings.seed ^= (int)System.DateTime.Now.Ticks;
			settings.seed ^= (int)Time.unscaledTime;
			settings.seed &= int.MaxValue;
		}
		Random.InitState(settings.seed);

		int cellCount = x * z;
		grid.CreateMap(x, z, wrapping);

		var cellData = new NativeArray<HexCellData>(
			grid.CellData, Allocator.TempJob);

		InitializeMapJob.Schedule(cellData, settings, default).Complete();

		cellData.CopyTo(grid.CellData);

		CreateLandJob.Execute(
			grid, settings, cellCount,
			out int landCells);
		ErodeLandJob.Execute(grid, settings, cellCount);

		cellData.CopyFrom(grid.CellData);

		CreateClimateJob.Schedule(
			cellData,
			info,
			settings,
			default,
			out NativeArray<CreateClimateJob.ClimateData> climateNA).Complete();

		cellData.Dispose();
		List<CreateClimateJob.ClimateData> climate =
			ListPool<CreateClimateJob.ClimateData>.Get();
		climate.AddRange(climateNA);
		climateNA.Dispose();		

		CreateRiversJob.Execute(grid, settings, climate, cellCount, landCells);
		SetTerrainTypeJob.Execute(grid, settings, climate, cellCount);
		ListPool<CreateClimateJob.ClimateData>.Add(climate);
		grid.RefreshAllCells();

		Random.state = originalRandomState;
	}
}
