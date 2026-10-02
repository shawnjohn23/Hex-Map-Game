using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Map generator job to set terrain type of cells.
/// Currently not an antual Burst job, but will become one in the future.
/// </summary>
public struct SetTerrainTypeJob
{
	static readonly float[] temperatureBands = { 0.1f, 0.3f, 0.6f };
	static readonly float[] moistureBands = { 0.12f, 0.28f, 0.85f };

	struct Biome
	{
		public int terrain, plant;

		public Biome(int terrain, int plant)
		{
			this.terrain = terrain;
			this.plant = plant;
		}
	}

	static readonly Biome[] biomes = {
		new(0, 0), new(4, 0), new(4, 0), new(4, 0),
		new(0, 0), new(2, 0), new(2, 1), new(2, 2),
		new(0, 0), new(1, 0), new(1, 1), new(1, 2),
		new(0, 0), new(1, 1), new(1, 2), new(1, 3)
	};

	HexGrid grid;

	MapGeneratorSettings settings;

	List<CreateClimateJob.ClimateData> climate;

	int temperatureJitterChannel;

	int rockDesertElevation;

	public static void Execute(
		HexGrid grid,
		MapGeneratorSettings settings,
		List<CreateClimateJob.ClimateData> climate,
		int cellCount)
	{
		var job = new SetTerrainTypeJob()
		{
			grid = grid,
			settings = settings,
			climate = climate,
			temperatureJitterChannel = Random.Range(0, 4),
			rockDesertElevation = settings.elevationMaximum -
				(settings.elevationMaximum - settings.waterLevel) / 2
		};

		for (int i = 0; i < cellCount; i++)
		{
			job.Execute(i);
		}
	}

	void Execute(int index)
	{
		HexCellData cell = grid.CellData[index];
		float temperature = DetermineTemperature(index, cell);
		grid.CellData[index].values = !cell.IsUnderwater ?
			GetLandValues(cell, temperature, climate[index].moisture) :
			GetUnderwaterValues(cell, temperature);
	}

	float DetermineTemperature(int cellIndex, HexCellData cell)
	{
		float latitude = (float)cell.coordinates.Z /
			grid.CellCountZ;
		if (settings.hemisphere == MapGeneratorSettings.HemisphereMode.Both)
		{
			latitude *= 2f;
			if (latitude > 1f)
			{
				latitude = 2f - latitude;
			}
		}
		else if (
			settings.hemisphere == MapGeneratorSettings.HemisphereMode.North)
		{
			latitude = 1f - latitude;
		}

		float temperature = Mathf.LerpUnclamped(
			settings.lowTemperature, settings.highTemperature, latitude);

		temperature *= 1f -
			(cell.ViewElevation - settings.waterLevel) /
			(settings.elevationMaximum - settings.waterLevel + 1f);

		float jitter = HexMetrics.SampleNoise(
			grid.CellPositions[cellIndex] * 0.1f)[temperatureJitterChannel];

		temperature += (jitter * 2f - 1f) * settings.temperatureJitter;

		return temperature;
	}

	HexValues GetLandValues(
		HexCellData cell, float temperature, float moisture)
	{
		int t = 0;
		for (; t < temperatureBands.Length; t++)
		{
			if (temperature < temperatureBands[t])
			{
				break;
			}
		}
		int m = 0;
		for (; m < moistureBands.Length; m++)
		{
			if (moisture < moistureBands[m])
			{
				break;
			}
		}
		Biome cellBiome = biomes[t * 4 + m];

		if (cellBiome.terrain == 0)
		{
			if (cell.Elevation >= rockDesertElevation)
			{
				cellBiome.terrain = 3;
			}
		}
		else if (cell.Elevation == settings.elevationMaximum)
		{
			cellBiome.terrain = 4;
		}

		if (cellBiome.terrain == 4)
		{
			cellBiome.plant = 0;
		}
		else if (cellBiome.plant < 3 && cell.HasRiver)
		{
			cellBiome.plant += 1;
		}
		return cell.values.
			WithTerrainTypeIndex(cellBiome.terrain).
			WithPlantLevel(cellBiome.plant);
	}

	HexValues GetUnderwaterValues(HexCellData cell, float temperature)
	{
		int terrain;
		if (cell.Elevation == settings.waterLevel - 1)
		{
			int cliffs = 0, slopes = 0;
			for (HexDirection d = HexDirection.NE;
				d <= HexDirection.NW; d++)
			{
				if (!grid.TryGetCellIndex(
					cell.coordinates.Step(d), out int neighborIndex))
				{
					continue;
				}
				int delta = grid.CellData[neighborIndex].Elevation -
					cell.WaterLevel;
				if (delta == 0)
				{
					slopes += 1;
				}
				else if (delta > 0)
				{
					cliffs += 1;
				}
			}

			if (cliffs + slopes > 3)
			{
				terrain = 1;
			}
			else if (cliffs > 0)
			{
				terrain = 3;
			}
			else if (slopes > 0)
			{
				terrain = 0;
			}
			else
			{
				terrain = 1;
			}
		}
		else if (cell.Elevation >= settings.waterLevel)
		{
			terrain = 1;
		}
		else if (cell.Elevation < 0)
		{
			terrain = 3;
		}
		else
		{
			terrain = 2;
		}

		if (terrain == 1 && temperature < temperatureBands[0])
		{
			terrain = 2;
		}
		return cell.values.WithTerrainTypeIndex(terrain);
	}
}
