using UnityEngine;

/// <summary>
/// Shared rules for any trip between a city and a cell: how many turns it
/// takes, and whether an enemy unit is standing somewhere on the way.
/// Work parties and construction sites both use this, so there is one
/// place to change when roads and rivers start speeding trips up.
/// </summary>
public static class Route
{
	// Placeholder: cells covered per turn.
	const int CellsPerTurn = 3;

	/// <summary>Whole turns to cover the distance one way (at least 1).</summary>
	public static int TravelTurns(City home, int cellIndex)
	{
		int distance = home.Location.Coordinates.DistanceTo(
			home.Grid.GetCell(cellIndex).Coordinates);
		return Mathf.Max(1, Mathf.CeilToInt(distance / (float)CellsPerTurn));
	}

	/// <summary>True if an enemy unit stands on any cell between city and site.</summary>
	public static bool IsBlocked(City home, int cellIndex)
	{
		HexGrid grid = home.Grid;
		HexCoordinates from = home.Location.Coordinates;
		HexCoordinates to = grid.GetCell(cellIndex).Coordinates;
		int steps = from.DistanceTo(to);
		for (int i = 1; i <= steps; i++)
		{
			float t = (float)i / steps;
			if (grid.TryGetCellIndex(LerpCell(from, to, t), out int index))
			{
				HexUnit unit = grid.CellUnits[index];
				if (unit && unit.Owner != home.Owner)
				{
					return true;
				}
			}
		}
		return false;
	}

	// Cube-coordinate lerp plus rounding, same idea as
	// HexCoordinates.FromPosition. Note: ignores map wrapping.
	static HexCoordinates LerpCell(HexCoordinates a, HexCoordinates b, float t)
	{
		float x = Mathf.Lerp(a.X, b.X, t);
		float z = Mathf.Lerp(a.Z, b.Z, t);
		float y = -x - z;
		int iX = Mathf.RoundToInt(x);
		int iY = Mathf.RoundToInt(y);
		int iZ = Mathf.RoundToInt(z);
		if (iX + iY + iZ != 0)
		{
			float dX = Mathf.Abs(x - iX);
			float dY = Mathf.Abs(y - iY);
			float dZ = Mathf.Abs(z - iZ);
			if (dX > dY && dX > dZ)
			{
				iX = -iY - iZ;
			}
			else if (dZ > dY)
			{
				iZ = -iX - iY;
			}
		}
		return new HexCoordinates(iX, iZ);
	}
}
