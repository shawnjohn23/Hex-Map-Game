using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// Map generator job to create cell climates.
/// Currently not an antual Burst job, but will become one in the future.
/// </summary>
[BurstCompile]
public struct CreateClimateJob : IJob
{
	[ReadOnly]
	NativeArray<HexCellData> cellData;

	HexMapInfo info;

	float
		elevationMaximum,
		evaporationFactor,
		precipitationFactor,
		runoffFactor,
		seepageFactor,
		startingMoisture,
		windStrength;
	
	HexDirection mainDispersalDirection;

	public struct ClimateData {
		public float clouds, moisture;
	}

	NativeArray<ClimateData> climate;

	public static JobHandle Schedule(
		NativeArray<HexCellData> cellData,
		HexMapInfo info,
		MapGeneratorSettings settings,
		JobHandle dependency,
		out NativeArray<ClimateData> climate)
	{
		climate = new(cellData.Length, Allocator.TempJob,
			NativeArrayOptions.UninitializedMemory);
		return new CreateClimateJob()
		{
			cellData = cellData,
			info = info,
			elevationMaximum = settings.elevationMaximum,
			evaporationFactor = settings.evaporationFactor,
			precipitationFactor = settings.precipitationFactor,
			runoffFactor = settings.runoffFactor,
			seepageFactor = settings.seepageFactor,
			startingMoisture = settings.startingMoisture,
			windStrength = settings.windStrength,
			mainDispersalDirection = settings.windDirection.Opposite(),
			climate = climate
		}.Schedule(dependency);
	}

	public void Execute()
	{
		NativeArray<ClimateData> climateA = climate;
		NativeArray<ClimateData> climateB = new(climate.Length, Allocator.Temp,
			NativeArrayOptions.UninitializedMemory);

		var initialData = new ClimateData
		{
			moisture = startingMoisture
		};
		var clearData = new ClimateData();
		for (int i = 0; i < cellData.Length; i++)
		{
			climateA[i] = initialData;
			climateB[i] = clearData;
		}

		for (int cycle = 0; cycle < 40; cycle++)
		{
			for (int i = 0; i < cellData.Length; i++)
			{
				EvolveClimate(i, climateA, climateB);
			}
			(climateB, climateA) = (climateA, climateB);
		}
	}

	void EvolveClimate(
		int cellIndex,
		NativeArray<ClimateData> climate,
		NativeArray<ClimateData> nextClimate)
	{
		HexCellData cell = cellData[cellIndex];
		ClimateData cellClimate = climate[cellIndex];

		if (cell.IsUnderwater)
		{
			cellClimate.moisture = 1f;
			cellClimate.clouds += evaporationFactor;
		}
		else
		{
			float evaporation =
				cellClimate.moisture * evaporationFactor;
			cellClimate.moisture -= evaporation;
			cellClimate.clouds += evaporation;
		}

		float precipitation = cellClimate.clouds * precipitationFactor;
		cellClimate.clouds -= precipitation;
		cellClimate.moisture += precipitation;

		float cloudMaximum =
			1f - cell.ViewElevation / (elevationMaximum + 1f);
		if (cellClimate.clouds > cloudMaximum)
		{
			cellClimate.moisture += cellClimate.clouds - cloudMaximum;
			cellClimate.clouds = cloudMaximum;
		}

		float cloudDispersal =
			cellClimate.clouds * (1f / (5f + windStrength));
		float runoff = cellClimate.moisture * runoffFactor * (1f / 6f);
		float seepage =
			cellClimate.moisture * seepageFactor * (1f / 6f);
		for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
		{
			if (!info.TryGetNeighborCellIndex(
				cell.coordinates, d, out int neighborIndex))
			{
				continue;
			}
			ClimateData neighborClimate = nextClimate[neighborIndex];
			if (d == mainDispersalDirection)
			{
				neighborClimate.clouds += cloudDispersal * windStrength;
			}
			else
			{
				neighborClimate.clouds += cloudDispersal;
			}

			int elevationDelta = cellData[neighborIndex].ViewElevation -
				cell.ViewElevation;
			if (elevationDelta < 0)
			{
				cellClimate.moisture -= runoff;
				neighborClimate.moisture += runoff;
			}
			else if (elevationDelta == 0)
			{
				cellClimate.moisture -= seepage;
				neighborClimate.moisture += seepage;
			}

			nextClimate[neighborIndex] = neighborClimate;
		}

		ClimateData nextCellClimate = nextClimate[cellIndex];
		nextCellClimate.moisture += cellClimate.moisture;
		if (nextCellClimate.moisture > 1f)
		{
			nextCellClimate.moisture = 1f;
		}
		nextClimate[cellIndex] = nextCellClimate;
		climate[cellIndex] = new ClimateData();
	}
}
