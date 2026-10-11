using UnityEngine;

public enum BuildKind { Farm, House }

/// <summary>
/// A farm or house being built on a cell. The wood is paid up front and
/// the builder walks out with it, so both arrive when the timer runs out.
/// The cell's entry in HexGrid.CellSites is what stops anything else
/// from being built there. Plain data plus one placeholder marker.
/// </summary>
public class ConstructionSite
{
	// Placeholder numbers, tune freely.
	public const int HouseCapacity = 1;
	const int BuildTurns = 0; // extra turns on top of the walk

	public static int WoodCost(BuildKind kind) =>
		kind == BuildKind.House ? 6 : 4;

	public City Home { get; }
	public int CellIndex { get; }
	public BuildKind Kind { get; }
	public int Builders => 1;
	public int TurnsLeft { get; private set; }

	public int TravelTurns => Route.TravelTurns(Home, CellIndex);

	GameObject marker;

	public ConstructionSite(City home, int cellIndex, BuildKind kind)
	{
		Home = home;
		CellIndex = cellIndex;
		Kind = kind;
		TurnsLeft = TravelTurns + BuildTurns;
		CreateMarker();
	}

	/// <summary>
	/// Called once at the end of the owner's turn. A blocked site freezes.
	/// Returns true when construction is finished.
	/// </summary>
	public bool Tick()
	{
		if (Route.IsBlocked(Home, CellIndex))
		{
			return false;
		}
		TurnsLeft--;
		return TurnsLeft <= 0;
	}

	void CreateMarker()
	{
		HexGrid grid = Home.Grid;
		HexCell cell = grid.GetCell(CellIndex);
		marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
		// No collider, so the marker never blocks clicks on cells.
		UnityEngine.Object.Destroy(marker.GetComponent<Collider>());
		marker.transform.localScale = new Vector3(4f, 1f, 4f);
		marker.GetComponent<Renderer>().material.color =
			PlayerColors.Colors[Home.Owner];
		marker.transform.localPosition = cell.Position + Vector3.up * 0.5f;
		grid.MakeChildOfColumn(marker.transform, cell.Coordinates.ColumnIndex);
	}

	public void Dispose()
	{
		if (marker)
		{
			UnityEngine.Object.Destroy(marker);
		}
	}
}
