using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Map generator job to create land.
/// Currently not an antual Burst job, but will become one in the future.
/// </summary>
public struct CreateLandJob
{
	HexGrid grid;

	MapGeneratorSettings settings;

	HexCellPriorityQueue searchFrontier;

	int searchFrontierPhase;

	struct MapRegion
	{
		public int xMin, xMax, zMin, zMax;
	}

	int cellCount;

	public static void Execute(
		HexGrid grid,
		MapGeneratorSettings settings,
		int cellCount,
		out int landCells)
	{
		landCells = new CreateLandJob()
		{
			grid = grid,
			settings = settings,
			searchFrontier = new HexCellPriorityQueue(grid),
			cellCount = cellCount
		}.Execute();
	}

	int Execute()
	{
		List<MapRegion> regions = GetRegions();

		int landBudget = Mathf.RoundToInt(
			cellCount * settings.landPercentage * 0.01f);
		int landCells = landBudget;
		for (int guard = 0; guard < 10000; guard++)
		{
			bool sink = Random.value < settings.sinkProbability;
			for (int i = 0; i < regions.Count; i++)
			{
				MapRegion region = regions[i];
				int chunkSize = Random.Range(
					settings.chunkSizeMin, settings.chunkSizeMax - 1);
				if (sink)
				{
					landBudget = SinkTerrain(chunkSize, landBudget, region);
				}
				else
				{
					landBudget = RaiseTerrain(chunkSize, landBudget, region);
					if (landBudget == 0)
					{
						return landCells;
					}
				}
			}
		}
		if (landBudget > 0)
		{
			Debug.LogWarning(
				"Failed to use up " + landBudget + " land budget.");
			landCells -= landBudget;
		}
		ListPool<MapRegion>.Add(regions);
		return landCells;
	}

	List<MapRegion> GetRegions()
	{
		var regions = ListPool<MapRegion>.Get();

		int borderX = grid.Wrapping ?
			settings.regionBorder : settings.mapBorderX;
		MapRegion region;
		switch (settings.regionCount)
		{
		default:
			if (grid.Wrapping)
			{
				borderX = 0;
			}
			region.xMin = borderX;
			region.xMax = grid.CellCountX - borderX;
			region.zMin = settings.mapBorderZ;
			region.zMax = grid.CellCountZ - settings.mapBorderZ;
			regions.Add(region);
			break;
		case 2:
			if (Random.value < 0.5f)
			{
				region.xMin = borderX;
				region.xMax = grid.CellCountX / 2 - settings.regionBorder;
				region.zMin = settings.mapBorderZ;
				region.zMax = grid.CellCountZ - settings.mapBorderZ;
				regions.Add(region);
				region.xMin = grid.CellCountX / 2 + settings.regionBorder;
				region.xMax = grid.CellCountX - borderX;
				regions.Add(region);
			}
			else
			{
				if (grid.Wrapping)
				{
					borderX = 0;
				}
				region.xMin = borderX;
				region.xMax = grid.CellCountX - borderX;
				region.zMin = settings.mapBorderZ;
				region.zMax = grid.CellCountZ / 2 - settings.regionBorder;
				regions.Add(region);
				region.zMin = grid.CellCountZ / 2 + settings.regionBorder;
				region.zMax = grid.CellCountZ - settings.mapBorderZ;
				regions.Add(region);
			}
			break;
		case 3:
			region.xMin = borderX;
			region.xMax = grid.CellCountX / 3 - settings.regionBorder;
			region.zMin = settings.mapBorderZ;
			region.zMax = grid.CellCountZ - settings.mapBorderZ;
			regions.Add(region);
			region.xMin = grid.CellCountX / 3 + settings.regionBorder;
			region.xMax = grid.CellCountX * 2 / 3 - settings.regionBorder;
			regions.Add(region);
			region.xMin = grid.CellCountX * 2 / 3 + settings.regionBorder;
			region.xMax = grid.CellCountX - borderX;
			regions.Add(region);
			break;
		case 4:
			region.xMin = borderX;
			region.xMax = grid.CellCountX / 2 - settings.regionBorder;
			region.zMin = settings.mapBorderZ;
			region.zMax = grid.CellCountZ / 2 - settings.regionBorder;
			regions.Add(region);
			region.xMin = grid.CellCountX / 2 + settings.regionBorder;
			region.xMax = grid.CellCountX - borderX;
			regions.Add(region);
			region.zMin = grid.CellCountZ / 2 + settings.regionBorder;
			region.zMax = grid.CellCountZ - settings.mapBorderZ;
			regions.Add(region);
			region.xMin = borderX;
			region.xMax = grid.CellCountX / 2 - settings.regionBorder;
			regions.Add(region);
			break;
		}
		return regions;
	}

	int RaiseTerrain(int chunkSize, int budget, MapRegion region)
	{
		searchFrontierPhase += 1;
		int firstCellIndex = GetRandomCellIndex(region);
		grid.SearchData[firstCellIndex] = new HexCellSearchData
		{
			searchPhase = searchFrontierPhase
		};
		searchFrontier.Enqueue(firstCellIndex);
		HexCoordinates center = grid.CellData[firstCellIndex].coordinates;

		int rise = Random.value < settings.highRiseProbability ? 2 : 1;
		int size = 0;
		while (size < chunkSize && searchFrontier.TryDequeue(out int index))
		{
			HexCellData current = grid.CellData[index];
			int originalElevation = current.Elevation;
			int newElevation = originalElevation + rise;
			if (newElevation > settings.elevationMaximum)
			{
				continue;
			}
			grid.CellData[index].values =
				current.values.WithElevation(newElevation);
			if (originalElevation < settings.waterLevel &&
				newElevation >= settings.waterLevel && --budget == 0
			)
			{
				break;
			}
			size += 1;

			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				if (grid.TryGetCellIndex(
					current.coordinates.Step(d), out int neighborIndex) &&
					grid.SearchData[neighborIndex].searchPhase <
						searchFrontierPhase)
				{
					grid.SearchData[neighborIndex] = new HexCellSearchData
					{
						searchPhase = searchFrontierPhase,
						distance = grid.CellData[neighborIndex].coordinates.
							DistanceTo(center),
						heuristic = Random.value < settings.jitterProbability ?
							1 : 0
					};
					searchFrontier.Enqueue(neighborIndex);
				}
			}
		}
		searchFrontier.Clear();
		return budget;
	}

	int SinkTerrain(int chunkSize, int budget, MapRegion region)
	{
		searchFrontierPhase += 1;
		int firstCellIndex = GetRandomCellIndex(region);
		grid.SearchData[firstCellIndex] = new HexCellSearchData
		{
			searchPhase = searchFrontierPhase
		};
		searchFrontier.Enqueue(firstCellIndex);
		HexCoordinates center = grid.CellData[firstCellIndex].coordinates;

		int sink = Random.value < settings.highRiseProbability ? 2 : 1;
		int size = 0;
		while (size < chunkSize && searchFrontier.TryDequeue(out int index))
		{
			HexCellData current = grid.CellData[index];
			int originalElevation = current.Elevation;
			int newElevation = current.Elevation - sink;
			if (newElevation < settings.elevationMinimum)
			{
				continue;
			}
			grid.CellData[index].values =
				current.values.WithElevation(newElevation);
			if (originalElevation >= settings.waterLevel &&
				newElevation < settings.waterLevel
			)
			{
				budget += 1;
			}
			size += 1;

			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				if (grid.TryGetCellIndex(
					current.coordinates.Step(d), out int neighborIndex) &&
					grid.SearchData[neighborIndex].searchPhase <
						searchFrontierPhase)
				{
					grid.SearchData[neighborIndex] = new HexCellSearchData
					{
						searchPhase = searchFrontierPhase,
						distance = grid.CellData[neighborIndex].coordinates.
							DistanceTo(center),
						heuristic = Random.value < settings.jitterProbability ?
							1 : 0
					};
					searchFrontier.Enqueue(neighborIndex);
				}
			}
		}
		searchFrontier.Clear();
		return budget;
	}

	int GetRandomCellIndex (MapRegion region) => grid.GetCellIndex(
		Random.Range(region.xMin, region.xMax),
		Random.Range(region.zMin, region.zMax));
}
