using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Map generator job to erode land.
/// Currently not an antual Burst job, but will become one in the future.
/// </summary>
public struct ErodeLandJob
{
	HexGrid grid;

	MapGeneratorSettings settings;

	int cellCount;

	public static void Execute(
		HexGrid grid,
		MapGeneratorSettings settings,
		int cellCount)
	{
		new ErodeLandJob()
		{
			grid = grid,
			settings = settings,
			cellCount = cellCount
		}.Execute();
	}

	void Execute()
	{
		List<int> erodibleIndices = ListPool<int>.Get();
		for (int i = 0; i < cellCount; i++)
		{
			if (IsErodible(i, grid.CellData[i].Elevation))
			{
				erodibleIndices.Add(i);
			}
		}

		int targetErodibleCount =
			(int)(erodibleIndices.Count *
			(100 - settings.erosionPercentage) * 0.01f);
		
		while (erodibleIndices.Count > targetErodibleCount)
		{
			Erode(erodibleIndices, Random.Range(0, erodibleIndices.Count));
		}

		ListPool<int>.Add(erodibleIndices);
	}

	void Erode(List<int> erodibleIndices, int index)
	{
		int cellIndex = erodibleIndices[index];
		HexCellData cell = grid.CellData[cellIndex];
		int targetCellIndex = GetErosionTarget(cellIndex, cell.Elevation);

		grid.CellData[cellIndex].values = cell.values =
			cell.values.WithElevation(cell.Elevation - 1);

		HexCellData targetCell = grid.CellData[targetCellIndex];
		grid.CellData[targetCellIndex].values = targetCell.values =
			targetCell.values.WithElevation(targetCell.Elevation + 1);

		if (!IsErodible(cellIndex, cell.Elevation))
		{
			int lastIndex = erodibleIndices.Count - 1;
			erodibleIndices[index] = erodibleIndices[lastIndex];
			erodibleIndices.RemoveAt(lastIndex);
		}

		for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
		{
			if (grid.TryGetCellIndex(
				cell.coordinates.Step(d), out int neighborIndex) &&
				grid.CellData[neighborIndex].Elevation ==
					cell.Elevation + 2 &&
				!erodibleIndices.Contains(neighborIndex))
			{
				erodibleIndices.Add(neighborIndex);
			}
		}

		if (IsErodible(targetCellIndex, targetCell.Elevation) &&
			!erodibleIndices.Contains(targetCellIndex))
		{
			erodibleIndices.Add(targetCellIndex);
		}

		for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
		{
			if (grid.TryGetCellIndex(
				targetCell.coordinates.Step(d), out int neighborIndex) &&
				neighborIndex != cellIndex &&
				grid.CellData[neighborIndex].Elevation ==
					targetCell.Elevation + 1 &&
				!IsErodible(
					neighborIndex, grid.CellData[neighborIndex].Elevation))
			{
				erodibleIndices.Remove(neighborIndex);
			}
		}
	}

	bool IsErodible(int cellIndex, int cellElevation)
	{
		int erodibleElevation = cellElevation - 2;
		HexCoordinates coordinates = grid.CellData[cellIndex].coordinates;
		for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
		{
			if (grid.TryGetCellIndex(
				coordinates.Step(d), out int neighborIndex) &&
				grid.CellData[neighborIndex].Elevation <= erodibleElevation)
			{
				return true;
			}
		}
		return false;
	}

	int GetErosionTarget (int cellIndex, int cellElevation)
	{
		List<int> candidates = ListPool<int>.Get();
		int erodibleElevation = cellElevation - 2;
		HexCoordinates coordinates = grid.CellData[cellIndex].coordinates;
		for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
		{
			if (grid.TryGetCellIndex(
				coordinates.Step(d), out int neighborIndex) &&
				grid.CellData[neighborIndex].Elevation <= erodibleElevation
			)
			{
				candidates.Add(neighborIndex);
			}
		}
		int target = candidates[Random.Range(0, candidates.Count)];
		ListPool<int>.Add(candidates);
		return target;
	}
}
