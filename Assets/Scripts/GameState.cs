using System.Collections.Generic;

/// <summary>
/// The single surface that both the human UI and AI agents read and act
/// through. It bundles the map engine (HexGrid) with the faction state and
/// exposes the legal-move enumeration that agents reason against.
///
/// Keeping every world change behind IGameAction means a human and an AI drive
/// the game through the exact same interface — which is the whole point of the
/// architecture: get this boundary right and the AI is almost an afterthought.
/// </summary>
public class GameState
{
	public HexGrid Grid { get; }
	public FactionState Factions { get; }

	public GameState(HexGrid grid, FactionState factions)
	{
		Grid = grid;
		Factions = factions;
	}

	/// <summary>Owner id of the faction whose turn it is.</summary>
	public int CurrentOwnerId => Factions.CurrentOwnerId;

	/// <summary>Enumerate the cells owned by a faction id (by cell index).</summary>
	public IEnumerable<int> CellsOwnedBy(int ownerId)
	{
		int n = Grid.CellData.Length;
		for (int i = 0; i < n; i++)
		{
			if (Factions.GetOwner(i) == ownerId)
			{
				yield return i;
			}
		}
	}

	/// <summary>Enumerate the units belonging to a faction id. Units are read
	/// from the engine's per-cell unit array, so we never need access to the
	/// engine's private unit list.</summary>
	public IEnumerable<HexUnit> UnitsOf(int ownerId)
	{
		HexUnit[] cellUnits = Grid.CellUnits;
		for (int i = 0; i < cellUnits.Length; i++)
		{
			HexUnit u = cellUnits[i];
			if (u != null && u.Owner == ownerId)
			{
				yield return u;
			}
		}
	}

	/// <summary>
	/// Produce every legal action for the current player. Agents pick from
	/// this list; the human UI emits the same action types directly.
	/// Deliberately simple for the vertical slice — claim + end turn — with
	/// movement to follow once the per-turn budget lands.
	/// </summary>
	public List<IGameAction> EnumerateLegalActions()
	{
		var actions = new List<IGameAction>();
		int me = CurrentOwnerId;

		// Claim any neutral land cell adjacent to something I already control.
		int n = Grid.CellData.Length;
		for (int i = 0; i < n; i++)
		{
			var claim = new ClaimCellAction(i);
			if (claim.IsLegal(this))
			{
				actions.Add(claim);
			}
		}

		// Ending the turn is always legal.
		actions.Add(new EndTurnAction());
		return actions;
	}
}
