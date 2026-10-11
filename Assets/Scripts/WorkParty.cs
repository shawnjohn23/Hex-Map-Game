using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A group of pops from one home city working one cell (forest now;
/// farms and stone nodes later). Plain data plus its little brown
/// rectangles. The cell's list in HexGrid.CellWorkers is the source of
/// truth for who works where; totals are always derived from it.
///
/// Pops arrive in batches: each batch starts producing only after it has
/// walked out and its first load has come back (2 x TravelTurns).
/// </summary>
public class WorkParty
{
	static readonly Color PersonColor = new Color(0.45f, 0.28f, 0.12f);

	// Placeholder numbers, tune freely.
	const int WoodPerPop = 2;
	const int MeatPerPop = 1;
	const int GrainPerPop = 2;
	// One pop clears a forest level in 6 turns, two pops in 3, and so on.
	const int PopTurnsPerLevel = 6;

	struct Arrival
	{
		public int Count;
		public int TurnsLeft;
	}

	public City Home { get; }
	public int CellIndex { get; }

	/// <summary>All pops sent here, whether producing or still arriving.</summary>
	public int Count { get; private set; }

	readonly List<Arrival> arriving = new();
	readonly List<GameObject> people = new();

	public WorkParty(City home, int cellIndex, int count)
	{
		Home = home;
		CellIndex = cellIndex;
		AddPops(count);
	}

	/// <summary>Hex distance from the home city to the work cell.</summary>
	public int Distance =>
		Home.Location.Coordinates.DistanceTo(
			Home.Grid.GetCell(CellIndex).Coordinates);

	/// <summary>Whole turns to cover the distance one way (at least 1).</summary>
	public int TravelTurns => Route.TravelTurns(Home, CellIndex);

	/// <summary>Pops that are producing now (derived, never stored).</summary>
	public int Working
	{
		get
		{
			int total = Count;
			foreach (Arrival a in arriving)
			{
				total -= a.Count;
			}
			return total;
		}
	}

	/// <summary>Send more pops; they produce after their own travel time.</summary>
	public void AddPops(int count)
	{
		Count += count;
		arriving.Add(new Arrival { Count = count, TurnsLeft = 2 * TravelTurns });
	}

	/// <summary>
	/// Remove pops (recalled or dead): the ones still arriving go first,
	/// newest batch first, then working pops.
	/// </summary>
	public void RemovePops(int count)
	{
		count = Mathf.Min(count, Count);
		int remaining = count;
		for (int i = arriving.Count - 1; i >= 0 && remaining > 0; i--)
		{
			Arrival a = arriving[i];
			int take = Mathf.Min(remaining, a.Count);
			a.Count -= take;
			remaining -= take;
			if (a.Count == 0)
			{
				arriving.RemoveAt(i);
			}
			else
			{
				arriving[i] = a;
			}
		}
		Count -= count;
	}

	bool IsBlocked() => Route.IsBlocked(Home, CellIndex);

	/// <summary>
	/// Called once at the end of the owner's turn. A blocked party freezes
	/// completely: arrivals do not advance and nothing is delivered.
	/// </summary>
	public void Tick()
	{
		if (IsBlocked())
		{
			return;
		}
		for (int i = arriving.Count - 1; i >= 0; i--)
		{
			Arrival a = arriving[i];
			a.TurnsLeft--;
			if (a.TurnsLeft <= 0)
			{
				arriving.RemoveAt(i);
			}
			else
			{
				arriving[i] = a;
			}
		}
		int working = Working;
		if (working <= 0)
		{
			return;
		}
		HexCell cell = Home.Grid.GetCell(CellIndex);
		if (cell.Values.PlantLevel <= 0)
		{
			if (cell.Values.FarmLevel > 0)
			{
				int grain = GrainPerPop * working;
				Home.Storage.Add(ResourceType.Grain, grain);
				Debug.Log($"Farm party of {working} (of {Count}) from player " +
					$"{Home.Owner + 1} delivered {grain} grain " +
					$"from {cell.Coordinates}");
			}
			return; // a cleared forest yields nothing
		}
		int wood = WoodPerPop * working;
		int meat = MeatPerPop * working;
		Home.Storage.Add(ResourceType.Wood, wood);
		Home.Storage.Add(ResourceType.Meat, meat);
		Debug.Log($"Party of {working} (of {Count}) from player " +
			$"{Home.Owner + 1} delivered {wood} wood, {meat} meat " +
			$"from {cell.Coordinates}");
		Deplete(cell, working);
	}

	// The cell's counter is shared by every party working it. Each full
	// PopTurnsPerLevel lowers the forest one level; leftovers carry over.
	void Deplete(HexCell cell, int popTurns)
	{
		HexGrid grid = Home.Grid;
		int oldLevel = cell.Values.PlantLevel;
		int level = oldLevel;
		int harvested = grid.CellHarvest[CellIndex] + popTurns;
		while (harvested >= PopTurnsPerLevel && level > 0)
		{
			harvested -= PopTurnsPerLevel;
			level--;
		}
		grid.CellHarvest[CellIndex] = level > 0 ? harvested : 0;
		if (level != oldLevel)
		{
			cell.SetPlantLevel(level);
			Debug.Log($"Forest at {cell.Coordinates} dropped to level {level}");
		}
	}

	/// <summary>Rebuild the rectangles: one per pop, in a row on the cell.</summary>
	public void Refresh()
	{
		HexGrid grid = Home.Grid;
		HexCell cell = grid.GetCell(CellIndex);
		List<WorkParty> sharing = grid.CellWorkers[CellIndex];
		int slot = sharing != null ? Mathf.Max(0, sharing.IndexOf(this)) : 0;

		Dispose();
		for (int i = 0; i < Count; i++)
		{
			GameObject person = GameObject.CreatePrimitive(PrimitiveType.Cube);
			// No collider, so the cubes never block clicks on cells.
			UnityEngine.Object.Destroy(person.GetComponent<Collider>());
			person.transform.localScale = new Vector3(0.5f, 1.5f, 0.35f);
			person.GetComponent<Renderer>().material.color = PersonColor;
			Vector3 offset = new Vector3(
				(i - (Count - 1) * 0.5f) * 0.9f, 0.75f, -1.5f - 1.5f * slot);
			person.transform.localPosition = cell.Position + offset;
			grid.MakeChildOfColumn(person.transform, cell.Coordinates.ColumnIndex);
			people.Add(person);
		}
	}

	public void Dispose()
	{
		foreach (GameObject person in people)
		{
			if (person)
			{
				UnityEngine.Object.Destroy(person);
			}
		}
		people.Clear();
	}
}
