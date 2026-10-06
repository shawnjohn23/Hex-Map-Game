using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
	[SerializeField]
	HexGrid grid;

	[SerializeField]
	int minimumDistance = 40;

	[SerializeField]
	int candidateCount = 4;

	void Update()
	{
		if (Keyboard.current != null &&
			Keyboard.current.gKey.wasPressedThisFrame)
		{
			PlaceCandidates();
		}
	}

	public void PlaceCandidates()
	{
		grid.ClearCities();
		for (int i = 0; i < candidateCount; i++)
		{
			HexCell cell = PickStartCell();
			if (!cell)
			{
				int nearest = DistanceToNearestCity(cell);
				grid.SpawnCity(cell);
				Debug.Log($"Candidate {i}: {cell.Coordinates}, nearest other city: {nearest}");
			}
			grid.SpawnCity(cell);
		}
	}

	public bool IsLegalStartCell(HexCell cell) =>
		cell.Flags.HasAll(HexFlags.Explorable) &&
		!cell.Values.IsUnderwater &&
		!cell.Unit && !cell.City;

	int DistanceToNearestCity(HexCell cell)
	{
		int nearest = int.MaxValue;
		foreach (City city in grid.Cities)
		{
			int d = cell.Coordinates.DistanceTo(city.Location.Coordinates);
			if (d < nearest)
			{
				nearest = d;
			}
		}
		return nearest;
	}

	HexCell PickStartCell()
	{
		var farEnough = new List<HexCell>();
		HexCell farthest = default;
		int farthestDistance = -1;

		for (int i = 0; i < grid.CellData.Length; i++)
		{
			HexCell cell = grid.GetCell(i);
			if (!IsLegalStartCell(cell))
			{
				continue;
			}
			int d = DistanceToNearestCity(cell);
			if (d >= minimumDistance)
			{
				farEnough.Add(cell);
			}
			if (d > farthestDistance)
			{
				farthestDistance = d;
				farthest = cell;
			}
		}

		if (farEnough.Count > 0)
		{
			return farEnough[Random.Range(0, farEnough.Count)];
		}
		return farthest;
	}
}