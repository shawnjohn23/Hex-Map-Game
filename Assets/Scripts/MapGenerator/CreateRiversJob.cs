using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Map generator job to create rivers.
/// Currently not an antual Burst job, but will become one in the future.
/// </summary>
public struct CreateRiversJob
{
	HexGrid grid;

	MapGeneratorSettings settings;

	List<CreateClimateJob.ClimateData> climate;

	List<HexDirection> flowDirections;

	int cellCount, landCells;

	public static void Execute(
		HexGrid grid,
		MapGeneratorSettings settings,
		List<CreateClimateJob.ClimateData> climate,
		int cellCount,
		int landCells)
	{
		List<HexDirection> directions = ListPool<HexDirection>.Get();
		new CreateRiversJob()
		{
			grid = grid,
			settings = settings,
			climate = climate,
			flowDirections = directions,
			cellCount = cellCount,
			landCells = landCells
		}.Execute();
		ListPool<HexDirection>.Add(directions);
	}

	void Execute()
	{
		List<int> riverOrigins = GetRiverOrigins();
		int riverBudget = Mathf.RoundToInt(
			landCells * settings.riverPercentage * 0.01f);
		while (riverBudget > 0 && riverOrigins.Count > 0)
		{
			int index = Random.Range(0, riverOrigins.Count);
			int lastIndex = riverOrigins.Count - 1;
			int originIndex = riverOrigins[index];
			HexCellData origin = grid.CellData[originIndex];
			riverOrigins[index] = riverOrigins[lastIndex];
			riverOrigins.RemoveAt(lastIndex);

			if (!origin.HasRiver)
			{
				bool isValidOrigin = true;
				for (HexDirection d = HexDirection.NE;
					d <= HexDirection.NW; d++)
				{
					if (grid.TryGetCellIndex(
						origin.coordinates.Step(d), out int neighborIndex) &&
						(grid.CellData[neighborIndex].HasRiver ||
							grid.CellData[neighborIndex].IsUnderwater))
					{
						isValidOrigin = false;
						break;
					}
				}
				if (isValidOrigin)
				{
					riverBudget -= CreateRiver(originIndex);
				}
			}
		}

		if (riverBudget > 0)
		{
			Debug.LogWarning("Failed to use up river budget.");
		}

		ListPool<int>.Add(riverOrigins);
	}

	List<int> GetRiverOrigins()
	{
		List<int> riverOrigins = ListPool<int>.Get();
		for (int i = 0; i < cellCount; i++)
		{
			HexCellData cell = grid.CellData[i];
			if (cell.IsUnderwater)
			{
				continue;
			}
			CreateClimateJob.ClimateData data = climate[i];
			float weight =
				data.moisture * (cell.Elevation - settings.waterLevel) /
				(settings.elevationMaximum - settings.waterLevel);
			if (weight > 0.75f)
			{
				riverOrigins.Add(i);
				riverOrigins.Add(i);
			}
			if (weight > 0.5f)
			{
				riverOrigins.Add(i);
			}
			if (weight > 0.25f)
			{
				riverOrigins.Add(i);
			}
		}
		return riverOrigins;
	}

	int CreateRiver(int originIndex)
	{
		int length = 1;
		int cellIndex = originIndex;
		HexCellData cell = grid.CellData[cellIndex];
		HexDirection direction = HexDirection.NE;
		while (!cell.IsUnderwater)
		{
			int minNeighborElevation = int.MaxValue;
			flowDirections.Clear();
			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				if (!grid.TryGetCellIndex(
					cell.coordinates.Step(d), out int neighborIndex))
				{
					continue;
				}
				HexCellData neighbor = grid.CellData[neighborIndex];

				if (neighbor.Elevation < minNeighborElevation)
				{
					minNeighborElevation = neighbor.Elevation;
				}

				if (neighborIndex == originIndex || neighbor.HasIncomingRiver)
				{
					continue;
				}

				int delta = neighbor.Elevation - cell.Elevation;
				if (delta > 0)
				{
					continue;
				}

				if (neighbor.HasOutgoingRiver)
				{
					grid.CellData[cellIndex].flags = cell.flags.WithRiverOut(d);
					grid.CellData[neighborIndex].flags =
						neighbor.flags.WithRiverIn(d.Opposite());
					return length;
				}

				if (delta < 0)
				{
					flowDirections.Add(d);
					flowDirections.Add(d);
					flowDirections.Add(d);
				}
				if (length == 1 ||
					(d != direction.Next2() && d != direction.Previous2()))
				{
					flowDirections.Add(d);
				}
				flowDirections.Add(d);
			}

			if (flowDirections.Count == 0)
			{
				if (length == 1)
				{
					return 0;
				}

				if (minNeighborElevation >= cell.Elevation)
				{
					cell.values = cell.values.WithWaterLevel(
						minNeighborElevation);
					if (minNeighborElevation == cell.Elevation)
					{
						cell.values = cell.values.WithElevation(
							minNeighborElevation - 1);
					}
					grid.CellData[cellIndex].values = cell.values;
				}
				break;
			}

			direction = flowDirections[Random.Range(0, flowDirections.Count)];
			cell.flags = cell.flags.WithRiverOut(direction);
			grid.TryGetCellIndex(
				cell.coordinates.Step(direction), out int outIndex);
			grid.CellData[outIndex].flags =
				grid.CellData[outIndex].flags.WithRiverIn(direction.Opposite());

			length += 1;

			if (minNeighborElevation >= cell.Elevation &&
				Random.value < settings.extraLakeProbability)
			{
				cell.values = cell.values.WithWaterLevel(cell.Elevation);
				cell.values = cell.values.WithElevation(cell.Elevation - 1);
			}
			grid.CellData[cellIndex] = cell;
			cellIndex = outIndex;
			cell = grid.CellData[cellIndex];
		}
		return length;
	}
}
