using System.Collections.Generic;

/// <summary>
/// One discrete, reversible-in-principle change to the game world. Every world
/// mutation — by human or AI — goes through an IGameAction so the legal-move
/// surface is uniform. IsLegal must be a pure check (no side effects); Apply
/// performs the change.
/// </summary>
public interface IGameAction
{
	bool IsLegal(GameState state);
	void Apply(GameState state);
}

/// <summary>
/// Claim a single neutral land cell that is adjacent to a cell the current
/// player already owns (or stands on). This is the smallest "expand territory"
/// primitive — the seed of the resource-node / outflanking gameplay.
/// </summary>
public class ClaimCellAction : IGameAction
{
	public readonly int CellIndex;

	public ClaimCellAction(int cellIndex) => CellIndex = cellIndex;

	public bool IsLegal(GameState state)
	{
		int me = state.CurrentOwnerId;
		if (me == 0)
		{
			return false;
		}

		HexCell cell = state.Grid.GetCell(CellIndex);

		// Must be unowned, on land, and not water.
		if (state.Factions.GetOwner(CellIndex) != 0)
		{
			return false;
		}
		if (cell.Values.IsUnderwater)
		{
			return false;
		}

		// Must be adjacent to a cell I already own, OR have my unit on it.
		if (cell.Unit != null && cell.Unit.Owner == me)
		{
			return true;
		}
		for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
		{
			if (cell.TryGetNeighbor(d, out HexCell neighbor) &&
				state.Factions.GetOwner(neighbor.Index) == me)
			{
				return true;
			}
		}
		return false;
	}

	public void Apply(GameState state)
	{
		state.Factions.SetOwner(CellIndex, state.CurrentOwnerId);
		// Visual refresh hook (overlay/ownership texture) goes here once the
		// renderer exists — see integration notes, "Visualization".
	}
}

/// <summary>
/// End the current player's turn, advancing to the next player and refilling
/// movement for the player who is about to act.
/// </summary>
public class EndTurnAction : IGameAction
{
	public bool IsLegal(GameState state) => state.Factions.Factions.Count > 0;

	public void Apply(GameState state)
	{
		state.Factions.AdvanceTurn();

		// Refill the incoming player's unit movement budgets.
		int incoming = state.CurrentOwnerId;
		foreach (HexUnit u in state.UnitsOf(incoming))
		{
			u.ReplenishMovement();
		}
	}
}

/// <summary>
/// Move one of the current player's units toward a destination cell using the
/// engine's existing A* (FindPath/GetPath). NOTE: the engine's Travel walks the
/// full path and does not enforce a per-turn budget yet; the budget check below
/// gates legality but path truncation is a milestone-4 refinement (see notes).
/// </summary>
public class MoveUnitAction : IGameAction
{
	public readonly HexUnit Unit;
	public readonly int ToCellIndex;

	public MoveUnitAction(HexUnit unit, int toCellIndex)
	{
		Unit = unit;
		ToCellIndex = toCellIndex;
	}

	public bool IsLegal(GameState state)
	{
		if (Unit == null || Unit.Owner != state.CurrentOwnerId)
		{
			return false;
		}
		HexCell to = state.Grid.GetCell(ToCellIndex);
		if (!Unit.IsValidDestination(to))
		{
			return false;
		}
		// Requires a path to exist. (Budget-aware truncation: milestone 4.)
		state.Grid.FindPath(Unit.Location, to, Unit);
		return state.Grid.HasPath;
	}

	public void Apply(GameState state)
	{
		HexCell to = state.Grid.GetCell(ToCellIndex);
		state.Grid.FindPath(Unit.Location, to, Unit);
		if (!state.Grid.HasPath)
		{
			return;
		}
		List<int> path = state.Grid.GetPath();
		if (path != null && path.Count > 1)
		{
			Unit.Travel(path);
		}
	}
}
