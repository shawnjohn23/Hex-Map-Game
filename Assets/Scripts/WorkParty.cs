using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A group of pops from one home city working one cell (forest now;
/// farms and stone nodes later). Plain data plus its little brown
/// rectangles. The cell's list in HexGrid.CellWorkers is the source of
/// truth for who works where; totals are always derived from it.
/// </summary>
public class WorkParty
{
	static readonly Color PersonColor = new Color(0.45f, 0.28f, 0.12f);

	public City Home { get; }
	public int CellIndex { get; }
	public int Count { get; set; }

	readonly List<GameObject> people = new();

	public WorkParty(City home, int cellIndex, int count)
	{
		Home = home;
		CellIndex = cellIndex;
		Count = count;
	}

	/// <summary>Hex distance from the home city to the work cell.</summary>
	public int Distance =>
		Home.Location.Coordinates.DistanceTo(
			Home.Grid.GetCell(CellIndex).Coordinates);

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
			person.transform.localScale = new Vector3(1f, 3f, 0.7f);
			person.GetComponent<Renderer>().material.color = PersonColor;
			Vector3 offset = new Vector3(
				(i - (Count - 1) * 0.5f) * 1.6f, 1.5f, -2f - 2.5f * slot);
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
