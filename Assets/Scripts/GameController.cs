using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
	[SerializeField]
	HexGrid grid;

	[SerializeField]
	int minimumDistance = 4;

	[SerializeField]
	int candidateCount = 4;

	// Debug: press T over a cell during Setup to drop a candidate city
	// there, ignoring the legality rules (e.g. to test rivers and roads).
	[SerializeField]
	bool debugKeys = true;

	void Start()
	{
		PlaceCandidates();
	}

	void Update()
	{
		if (debugKeys && GameState.Phase == GamePhase.Setup &&
			Keyboard.current != null && Mouse.current != null &&
			Keyboard.current.tKey.wasPressedThisFrame)
		{
			HexCell target = grid.GetCell(Camera.main.ScreenPointToRay(
				Mouse.current.position.ReadValue()));
			if (target && !target.City && !target.Values.IsUnderwater)
			{
				grid.SpawnCity(target);
			}
		}
		// Re-roll candidates, only allowed before anyone has claimed.
		if (Keyboard.current != null &&
			Keyboard.current.gKey.wasPressedThisFrame &&
			GameState.Phase == GamePhase.Setup &&
			!AnyClaimed())
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
				Debug.LogWarning("No legal start cell found.");
				break;
			}
			// Measure BEFORE spawning, otherwise the nearest city is itself (0).
			int nearest = DistanceToNearestCity(cell);
			grid.SpawnCity(cell);
			Debug.Log(
				$"Candidate {i}: {cell.Coordinates}, nearest other city: {nearest}");
		}
	}

	/// <summary>
	/// Try to claim the city on a cell for the current player (Setup only).
	/// </summary>
	public bool TryClaim(HexCell cell)
	{
		if (GameState.Phase != GamePhase.Setup || !cell)
		{
			return false;
		}
		City city = cell.City;
		if (!city || city.State != CityState.Candidate)
		{
			return false;
		}
		city.Claim(GameState.CurrentPlayer);
		// Temporary starting resources for testing.
		city.Storage.Add(ResourceType.Stone, 5);
		city.Storage.Add(ResourceType.Wood, 5);
		city.Storage.Add(ResourceType.Grain, 6);
		city.Storage.Add(ResourceType.Meat, 4);
		GameState.CompleteClaim();
		if (GameState.Phase == GamePhase.Playing)
		{
			grid.RemoveUnclaimedCities();
		}
		return true;
	}

	/// <summary>Per-turn upkeep for one player's cities.</summary>
	public void RunUpkeep(int player)
	{
		foreach (City city in grid.Cities)
		{
			if (city.State == CityState.Claimed && city.Owner == player)
			{
				city.Feed();
			}
		}
	}

	/// <summary>A forest pops can be sent to: land, trees, no city.</summary>
	public bool IsWorkableForest(HexCell cell) =>
		cell && !cell.Values.IsUnderwater &&
		cell.Values.PlantLevel > 0 && !cell.City;

	/// <summary>The player's closest city that has idle pops, or null.</summary>
	public City ClosestCityWithIdle(HexCell target, int player)
	{
		City best = null;
		int bestDistance = int.MaxValue;
		foreach (City city in grid.Cities)
		{
			if (city.State != CityState.Claimed || city.Owner != player ||
				city.Idle <= 0)
			{
				continue;
			}
			int d = target.Coordinates.DistanceTo(city.Location.Coordinates);
			if (d < bestDistance)
			{
				bestDistance = d;
				best = city;
			}
		}
		return best;
	}

	/// <summary>
	/// Send count idle pops from home to work the forest on a cell.
	/// Pops from the same home merge into one party.
	/// </summary>
	public bool TryAssign(HexCell cell, City home, int count)
	{
		if (!IsWorkableForest(cell) || !home || count < 1 || count > home.Idle)
		{
			return false;
		}
		List<WorkParty> list =
			grid.CellWorkers[cell.Index] ??= new List<WorkParty>();
		WorkParty party = list.Find(p => p.Home == home);
		if (party != null)
		{
			party.Count += count;
		}
		else
		{
			party = new WorkParty(home, cell.Index, count);
			list.Add(party);
			home.Parties.Add(party);
		}
		party.Refresh();
		return true;
	}

	bool AnyClaimed()
	{
		foreach (City city in grid.Cities)
		{
			if (city.State == CityState.Claimed)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsLegalStartCell(HexCell cell) =>
		cell.Flags.HasAll(HexFlags.Explorable) &&
		!cell.Values.IsUnderwater &&
		!cell.Unit && !cell.City &&
		// Clean cell so the urban feature can show the city's houses.
		cell.Values.UrbanLevel == 0 &&
		cell.Values.FarmLevel == 0 &&
		cell.Values.PlantLevel == 0 &&
		cell.Values.SpecialIndex == 0 &&
		cell.Flags.HasNone(HexFlags.Roads);

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
		Debug.LogWarning(
			$"Fallback used: best distance {farthestDistance} < minimum {minimumDistance}");
		return farthest;
	}
}
