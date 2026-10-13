using System.Collections.Generic;
using UnityEngine;

public enum CityState { Candidate, Claimed }

public class City : MonoBehaviour
{
	public const int StartingPopulation = 2;
	public const int ComboHousing = 2;   // capacity the combo block adds
	public const float StarvationChance = 0.5f;

	public HexGrid Grid { get; set; }

	public CityState State { get; private set; } = CityState.Candidate;

	/// <summary>This city's own storage; only its owner may see it.</summary>
	public Stockpile Storage { get; } = new Stockpile();

	public int HealthyPop { get; private set; }
	public int WeakPop { get; private set; }
	public int Population => HealthyPop + WeakPop;
	public int PopulationCapacity { get; private set; }

	/// <summary>Work parties from this city (same objects as in the grid).</summary>
	public List<WorkParty> Parties { get; } = new();

	public int Assigned
	{
		get
		{
			int total = 0;
			foreach (WorkParty party in Parties)
			{
				total += party.Count;
			}
			foreach (ConstructionSite site in Sites)
			{
				total += site.Builders;
			}
			return total + Army.Count;
		}
	}

	/// <summary>Construction sites this city sent builders to.</summary>
	public List<ConstructionSite> Sites { get; } = new();

	/// <summary>Soldiers trained here. They eat but produce nothing.</summary>
	public List<HexUnit> Army { get; } = new();
	public int Soldiers => Army.Count;

	struct Returning
	{
		public int Count;
		public int TurnsLeft;
	}

	readonly List<Returning> returning = new();

	/// <summary>Pops walking home; not available until they arrive.</summary>
	public int ReturningPops
	{
		get
		{
			int total = 0;
			foreach (Returning r in returning)
			{
				total += r.Count;
			}
			return total;
		}
	}

	/// <summary>Pops not working, building or walking home. Always derived.</summary>
	public int Idle => Mathf.Max(0, Population - Assigned - ReturningPops);

	public void SendHome(int count, int turns)
	{
		if (count > 0)
		{
			returning.Add(new Returning
			{
				Count = count,
				TurnsLeft = Mathf.Max(1, turns)
			});
		}
	}

	/// <summary>Once per turn: walkers get closer; at 0 they are idle again.</summary>
	public void TickReturning()
	{
		for (int i = returning.Count - 1; i >= 0; i--)
		{
			Returning r = returning[i];
			r.TurnsLeft--;
			if (r.TurnsLeft <= 0)
			{
				returning.RemoveAt(i);
			}
			else
			{
				returning[i] = r;
			}
		}
	}

	public void AddHousing(int capacity) => PopulationCapacity += capacity;

	/// <summary>Grain plus meat: what pops can eat.</summary>
	public int Food =>
		Storage.Get(ResourceType.Grain) + Storage.Get(ResourceType.Meat);

	public int VisionRange => 3;

	int locationCellIndex = -1;
	int owner = PlayerColors.Neutral;
	
	// Consecutive turns with enough food and some to spare (growth rule).
	int surplusTurns;

	public HexCell Location
	{
		get => Grid.GetCell(locationCellIndex);
		set
		{
			if (locationCellIndex >= 0)
			{
				HexCell location = Grid.GetCell(locationCellIndex);
				Grid.DecreaseVisibility(location, VisionRange);
				location.City = null;
			}
			locationCellIndex = value.Index;
			value.City = this;
			Grid.IncreaseVisibility(value, VisionRange);
			transform.localPosition = MarkerPosition(value);
			Grid.MakeChildOfColumn(transform, value.Coordinates.ColumnIndex);
		}
	}

	public int Owner
	{
		get => owner;
		set
		{
			owner = value;
			foreach (Renderer r in GetComponentsInChildren<Renderer>())
			{
				r.material.color = PlayerColors.Colors[value];
			}
		}
	}

	public void Claim(int player)
	{
		Owner = player;
		State = CityState.Claimed;
		HealthyPop = StartingPopulation;
		PopulationCapacity = ComboHousing;

		// The map's urban feature draws the houses; the owner tints them.
		HexCell cell = Location;
		cell.SetOwner(player);
		cell.SetUrbanLevel(1);
	}

	/// <summary>
	/// Each pop eats 1 food (meat first, then grain) from this city's
	/// storage. Unfed healthy pops become weak; unfed weak pops die; fed
	/// pops are healthy next turn. Placeholder: weak pops go unfed first.
	/// Pops that die leave their work parties, farthest party first.
	/// </summary>
	public void Feed()
	{
		Army.RemoveAll(unit => !unit);
		int pop = Population;
		int eaten = Mathf.Min(pop, Food);
		int meatEaten = Mathf.Min(eaten, Storage.Get(ResourceType.Meat));
		Storage.TrySpend(ResourceType.Meat, meatEaten);
		Storage.TrySpend(ResourceType.Grain, eaten - meatEaten);
		int unfed = pop - eaten;

		// Only the unfed pops (the ones farthest from the storehouse) roll.
		int weakUnfed = Mathf.Min(WeakPop, unfed);
		int healthyUnfed = Mathf.Min(HealthyPop, unfed - weakUnfed);
		int weakFed = WeakPop - weakUnfed;                           // recover

		int died = 0;
		for (int i = 0; i < weakUnfed; i++)
		{
			if (GameRandom.Chance(StarvationChance)) died++;
		}
		int weakened = 0;
		for (int i = 0; i < healthyUnfed; i++)
		{
			if (GameRandom.Chance(StarvationChance)) weakened++;
		}

		HealthyPop = (HealthyPop - weakened) + weakFed;
		WeakPop = weakened + (weakUnfed - died);
		TrimParties();

		// Growth: 6 turns in a row of enough food and some to spare.
		if (unfed == 0 && Food >= Population)
		{
			surplusTurns = Mathf.Min(surplusTurns + 1, 6);
			if (surplusTurns >= 6 && Population < PopulationCapacity)
			{
				HealthyPop++;
				surplusTurns = 0;
			}
		}
		else
		{
			surplusTurns = 0;
		}
		Debug.Log($"Player {Owner + 1} city fed: pop {Population} " +
			$"(healthy {HealthyPop}, weak {WeakPop}), {weakened} weakened, " +
			$"{died} died, " +
			$"grain {Storage.Get(ResourceType.Grain)}, " +
			$"meat {Storage.Get(ResourceType.Meat)}");
	}

	// If pops died, remove them from work parties, farthest party first.
	void TrimParties()
	{
		int excess = Assigned - Population;
		// Pops walking home die first.
		for (int i = returning.Count - 1; i >= 0 && excess > 0; i--)
		{
			Returning r = returning[i];
			int take = Mathf.Min(excess, r.Count);
			r.Count -= take;
			excess -= take;
			if (r.Count == 0)
			{
				returning.RemoveAt(i);
			}
			else
			{
				returning[i] = r;
			}
		}
		// Farthest group first, whether a work party or a soldier. Ties go to
		// the worker, then to whichever is first in its list, so the order
		// is always the same.
		while (excess > 0 && (Parties.Count > 0 || Army.Count > 0))
		{
			HexUnit farSoldier = null;
			int soldierDistance = -1;
			foreach (HexUnit soldier in Army)
			{
				int d = soldier.Location.Coordinates.DistanceTo(
					Location.Coordinates);
				if (d > soldierDistance)
				{
					soldierDistance = d;
					farSoldier = soldier;
				}
			}
			WorkParty farthest = null;
			int partyDistance = -1;
			foreach (WorkParty party in Parties)
			{
				if (party.Distance > partyDistance)
				{
					partyDistance = party.Distance;
					farthest = party;
				}
			}
			
			if (farSoldier && soldierDistance > partyDistance)
			{
				Army.Remove(farSoldier);
				Grid.RemoveUnit(farSoldier);
				excess--;
				continue;
			}

			int removed = Mathf.Min(excess, farthest.Count);
			farthest.RemovePops(removed);
			excess -= removed;
			if (farthest.Count == 0)
			{
				Parties.Remove(farthest);
				Grid.CellWorkers[farthest.CellIndex]?.Remove(farthest);
				farthest.Dispose();
			}
			else
			{
				farthest.Refresh();
			}
		}
	}

	public void ValidateLocation() =>
		transform.localPosition = MarkerPosition(Grid.GetCell(locationCellIndex));

	// On river cells, move the marker off the river toward the first
	// edge the river does not cross, so it sits beside the water.
	static Vector3 MarkerPosition(HexCell cell)
	{
		Vector3 position = cell.Position;
		HexFlags flags = cell.Flags;
		if (flags.HasAny(HexFlags.River))
		{
			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				if (!flags.HasRiver(d))
				{
					position += (HexMetrics.GetFirstSolidCorner(d) +
						HexMetrics.GetSecondSolidCorner(d)) * 0.5f;
					break;
				}
			}
		}
		return position;
	}

	public void Die()
	{
		foreach (WorkParty party in Parties)
		{
			party.Dispose();
		}
		Parties.Clear();
		HexCell location = Grid.GetCell(locationCellIndex);
		Grid.DecreaseVisibility(location, VisionRange);
		location.City = null;
		if (State == CityState.Claimed)
		{
			// Undo what Claim changed on the cell.
			location.SetUrbanLevel(0);
			location.SetOwner(PlayerColors.Neutral);
		}
		Destroy(gameObject);
	}

	void Awake() => Owner = owner;
}
