using System.Collections.Generic;
using System.IO;

/// <summary>
/// The faction / ownership state layer for a map. This is the authoritative
/// game state that sits ON TOP of the HexMap engine. It deliberately holds no
/// rendering or Unity behaviour so it can be reasoned about (and later
/// simulated headlessly) independently of the map engine.
///
/// Ownership is stored as a parallel array indexed by cell index, mirroring the
/// engine's existing parallel-array layout (CellUnits, CellPositions,
/// cellVisibility). Owner id 0 == neutral; factions are 1..N.
/// </summary>
public class FactionState
{
	// Self-describing save block: magic + version so the block is detectable
	// at the end of a map file and old (vanilla) maps load untouched.
	const int blockMagic = 0x46414354; // "FACT"
	const int blockVersion = 1;

	byte[] cellOwners;

	/// <summary>Factions in turn order. Index 0 is the first player.</summary>
	public List<Faction> Factions { get; private set; } = new();

	/// <summary>Index into <see cref="Factions"/> whose turn it is.</summary>
	public int CurrentPlayerIndex { get; set; }

	/// <summary>1-based turn counter.</summary>
	public int TurnNumber { get; set; } = 1;

	/// <summary>Owner id (1..N) of the faction whose turn it is.</summary>
	public int CurrentOwnerId =>
		Factions.Count > 0 ? Factions[CurrentPlayerIndex].Id : 0;

	/// <summary>Number of cells the owner array currently covers.</summary>
	public int CellCount => cellOwners?.Length ?? 0;

	/// <summary>
	/// (Re)size ownership storage for a freshly created/loaded map. Called by
	/// HexGrid.CreateCells so the array always matches the live cell count.
	/// Resets all cells to neutral.
	/// </summary>
	public void OnMapCreated(int cellCount)
	{
		if (cellOwners == null || cellOwners.Length != cellCount)
		{
			cellOwners = new byte[cellCount];
		}
		else
		{
			System.Array.Clear(cellOwners, 0, cellOwners.Length);
		}
	}

	/// <summary>Configure the players. Call once when starting a game.</summary>
	public void SetFactions(IEnumerable<Faction> factions)
	{
		Factions = new List<Faction>(factions);
		CurrentPlayerIndex = 0;
		TurnNumber = 1;
	}

	/// <summary>Owner id of a cell (0 == neutral).</summary>
	public int GetOwner(int cellIndex) =>
		cellOwners != null && (uint)cellIndex < (uint)cellOwners.Length
			? cellOwners[cellIndex] : 0;

	/// <summary>Set the owner id of a cell. Returns true if it changed.</summary>
	public bool SetOwner(int cellIndex, int ownerId)
	{
		if (cellOwners == null || (uint)cellIndex >= (uint)cellOwners.Length)
		{
			return false;
		}
		if (cellOwners[cellIndex] == ownerId)
		{
			return false;
		}
		cellOwners[cellIndex] = (byte)ownerId;
		return true;
	}

	/// <summary>Count cells owned by a faction id.</summary>
	public int CountOwned(int ownerId)
	{
		if (cellOwners == null)
		{
			return 0;
		}
		int count = 0;
		for (int i = 0; i < cellOwners.Length; i++)
		{
			if (cellOwners[i] == ownerId)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>Advance to the next player, incrementing the turn counter
	/// when wrapping back to the first player.</summary>
	public void AdvanceTurn()
	{
		if (Factions.Count == 0)
		{
			return;
		}
		CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Factions.Count;
		if (CurrentPlayerIndex == 0)
		{
			TurnNumber++;
		}
	}

	// --- Serialization ---------------------------------------------------
	// Appended after the engine's unit data. Self-versioned and self-bounded
	// so vanilla maps (which lack this block) are unaffected.

	public void Save(BinaryWriter writer)
	{
		writer.Write(blockMagic);
		writer.Write(blockVersion);
		writer.Write(TurnNumber);
		writer.Write(CurrentPlayerIndex);

		int count = cellOwners?.Length ?? 0;
		writer.Write(count);
		for (int i = 0; i < count; i++)
		{
			writer.Write(cellOwners[i]);
		}
		// TODO(milestone 4): persist per-unit Owner here, keyed by the engine's
		// unit save order, so unit ownership survives save/load without
		// touching the vanilla HexUnit save format.
	}

	/// <summary>
	/// Attempt to read a faction block. The caller must only invoke this when
	/// bytes remain after the engine's data. If the magic does not match, the
	/// stream is left as-is and nothing is applied (treated as a vanilla map).
	/// </summary>
	public void Load(BinaryReader reader)
	{
		int magic = reader.ReadInt32();
		if (magic != blockMagic)
		{
			// Not our block — an older/foreign trailer. Leave neutral.
			return;
		}
		int version = reader.ReadInt32();
		TurnNumber = reader.ReadInt32();
		CurrentPlayerIndex = reader.ReadInt32();

		int count = reader.ReadInt32();
		if (cellOwners == null || cellOwners.Length != count)
		{
			cellOwners = new byte[count];
		}
		for (int i = 0; i < count; i++)
		{
			cellOwners[i] = reader.ReadByte();
		}
		// version is currently always 1; branch here when the format grows.
	}
}
