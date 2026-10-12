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

	// 0 = new seed every game (it is logged); any other number replays that game.
	[SerializeField]
	int gameSeed = 0;

	/// <summary>Begin a game on the current map: seed the rules, place start candidates.</summary>
	public void StartNewGame()
	{
		GameRandom.Init(gameSeed != 0 ? gameSeed : System.Environment.TickCount);
		Debug.Log($"Game seed: {GameRandom.Seed}");
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
		// Temporary: until the start-game menu exists, G starts a game on
		// the current map. Only before anyone has claimed.
		if (debugKeys && Keyboard.current != null &&
			Keyboard.current.gKey.wasPressedThisFrame &&
			GameState.Phase == GamePhase.Setup && !AnyClaimed())
		{
			StartNewGame();
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
				city.TickReturning();
				foreach (WorkParty party in city.Parties)
				{
					party.Tick();
				}
				foreach (WorkParty party in city.Parties.ToArray())
				{
					if (!IsWorkable(grid.GetCell(party.CellIndex), city.Owner))
					{
						city.SendHome(party.Count, party.TravelTurns);
						grid.CellWorkers[party.CellIndex]?.Remove(party);
						city.Parties.Remove(party);
						party.Dispose();
					}
				}
				foreach (ConstructionSite site in city.Sites.ToArray())
				{
					if (site.Tick())
					{
						CompleteSite(site);
					}
				}
				city.Feed();
			}
		}
	}

	/// <summary>A forest pops can be sent to: land, trees, no city.</summary>
	public bool IsWorkableForest(HexCell cell) =>
		cell && !cell.Values.IsUnderwater &&
		cell.Values.PlantLevel > 0 && !cell.City;
		
	/// <summary>A forest anyone can work, or a farm only its owner can work.</summary>
	public bool IsWorkable(HexCell cell, int player) =>
		IsWorkableForest(cell) ||
		(cell && !cell.Values.IsUnderwater && !cell.City &&
		cell.Values.FarmLevel > 0 &&
		grid.CellOwners[cell.Index] == player);

	/// <summary>Forests hold 2 workers at any level; farms hold one per level.</summary>
	public int MaxWorkers(HexCell cell) =>
		cell.Values.PlantLevel > 0 ? 2 : cell.Values.FarmLevel;

	/// <summary>All pops sent to a cell, walking or working.</summary>
	public int WorkersAt(HexCell cell)
	{
		int total = 0;
		List<WorkParty> list = grid.CellWorkers[cell.Index];
		if (list != null)
		{
			foreach (WorkParty party in list)
			{
				total += party.Count;
			}
		}
		return total;
	}

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
		if (!IsWorkable(cell, home ? home.Owner : -1) || !home || count < 1 ||
			count > home.Idle || count > MaxWorkers(cell) - WorkersAt(cell))
		{
			return false;
		}
		List<WorkParty> list =
			grid.CellWorkers[cell.Index] ??= new List<WorkParty>();
		WorkParty party = list.Find(p => p.Home == home);
		if (party != null)
		{
			party.AddPops(count);
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
	

	/// <summary>Send count pops home from a forest; idle is derived, so nothing else to fix.</summary>
	public bool TryUnassign(HexCell cell, City home, int count)
	{
		List<WorkParty> list = grid.CellWorkers[cell.Index];
		WorkParty party = list?.Find(p => p.Home == home);
		if (party == null || count < 1)
		{
			return false;
		}
		int removed = Mathf.Min(count, party.Count);
		party.RemovePops(count);
		home.SendHome(removed, party.TravelTurns);
		if (party.Count == 0)
		{
			list.Remove(party);
			home.Parties.Remove(party);
			party.Dispose();
		}
		else
		{
			party.Refresh();
		}
		// Parties sharing the cell are laid out by list position.
		foreach (WorkParty other in list)
		{
			other.Refresh();
		}
		return true;
	}

	/// <summary>Idle pops across all of a player's cities.</summary>
	public int FreePops(int player)
	{
		int total = 0;
		foreach (City city in grid.Cities)
		{
			if (city.State == CityState.Claimed && city.Owner == player)
			{
				total += city.Idle;
			}
		}
		return total;
	}

	/// <summary>A copy of the player's work parties on a cell.</summary>
	public List<WorkParty> PartiesAt(HexCell cell, int player)
	{
		var result = new List<WorkParty>();
		List<WorkParty> list = grid.CellWorkers[cell.Index];
		if (list != null)
		{
			foreach (WorkParty party in list)
			{
				if (party.Home.Owner == player)
				{
					result.Add(party);
				}
			}
		}
		return result;
	}	

	/// <summary>Empty, legal land: no forest, building, site, or other owner.</summary>
	/// <summary>
	/// Legal if unowned, no forest, no city, no site, no road or special.
	/// Unowned map farms and houses are just scenery; building replaces them.
	/// </summary>
	public bool IsBuildable(HexCell cell, int player) =>
		BuildBlocker(cell, player) == null;

	/// <summary>Why a cell can't be built on, or null if it can.</summary>
	public string BuildBlocker(HexCell cell, int player)
	{
		if (!cell) return "no cell";
		if (cell.Values.IsUnderwater) return "underwater";
		if (cell.City) return "city";
		if (grid.CellSites[cell.Index] != null) return "construction site";
		if (cell.Values.PlantLevel != 0) return "forest";
		if (cell.Values.SpecialIndex != 0) return "special feature";
		if (cell.Flags.HasAny(HexFlags.Roads)) return "road";
		if (grid.CellOwners[cell.Index] != PlayerColors.Neutral) return "owned";
		return null;
	}

	/// <summary>
	/// Pay the wood now and send the nearest city's builder with it.
	/// The site blocks the cell immediately.
	/// </summary>
	public bool TryBuild(HexCell cell, BuildKind kind, int player)
	{
		if (!IsBuildable(cell, player))
		{
			return false;
		}
		City home = ClosestCityWithIdle(cell, player);
		if (!home ||
			!home.Storage.TrySpend(ResourceType.Wood,
				ConstructionSite.WoodCost(kind)))
		{
			return false;
		}
		var site = new ConstructionSite(home, cell.Index, kind);
		grid.CellSites[cell.Index] = site;
		home.Sites.Add(site);
		return true;
	}

	void CompleteSite(ConstructionSite site)
	{
		HexCell cell = grid.GetCell(site.CellIndex);
		if (site.Kind == BuildKind.Farm)
		{
			cell.SetUrbanLevel(0);
			cell.SetFarmLevel(1);
		}
		else
		{
			cell.SetFarmLevel(0);
			cell.SetUrbanLevel(1);
			site.Home.AddHousing(ConstructionSite.HouseCapacity);
		}
		cell.SetOwner(site.Home.Owner);
		grid.CellSites[site.CellIndex] = null;
		site.Home.Sites.Remove(site);
		site.Home.SendHome(site.Builders, site.TravelTurns);
		site.Dispose();
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
			return farEnough[GameRandom.Range(0, farEnough.Count)];
		}
		Debug.LogWarning(
			$"Fallback used: best distance {farthestDistance} < minimum {minimumDistance}");
		return farthest;
	}
}
