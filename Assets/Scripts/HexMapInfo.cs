using Unity.Mathematics;

/// <summary>
/// Information about a hex map.
/// </summary>
public readonly struct HexMapInfo
{
	/// <summary>
	/// The XZ size of the map.
	/// </summary>
	public readonly int2 size;

	/// <summary>
	/// The wrap size of the map.
	/// Equal to the X size when wrapping and 0 when not wrapping.
	/// </summary>
	public readonly int wrapSize;

	/// <summary>
	/// Create new map info.
	/// </summary>
	/// <param name="x">X size of the map.</param>
	/// <param name="z">Z size of the map.</param>
	/// <param name="wrapping">Whether the map wraps X.</param>
	public HexMapInfo(int x, int z, bool wrapping) :
		this(new int2(x, z), wrapping) {}
	
	/// <summary>
	/// Create new map info.
	/// </summary>
	/// <param name="size">XZ size of the map.</param>
	/// <param name="wrapping">Whether the map wraps X.</param>
	public HexMapInfo(int2 size, bool wrapping)
	{
		this.size = size;
		wrapSize = wrapping ? size.x : 0;
	}

	/// <summary>
	/// Try to get the cell index for specific <see cref="HexCoordinates"/>.
	/// </summary>
	/// <param name="coordinates"><see cref="HexCoordinates"/>
	/// of the cell.</param>
	/// <param name="cell">The cell index if it exists, otherwise -1.</param>
	/// <returns>Whether the cell index exists.</returns>
	public readonly bool TryGetCellIndex(
		HexCoordinates coordinates,
		out int cellIndex)
	{
		int2 o = coordinates.OffsetCoordinates;
		if (o.y < 0 || o.y >= size.y || o.x < 0 || o.x >= size.x)
		{
			cellIndex = -1;
			return false;
		}
		cellIndex = o.x + o.y * size.x;
		return true;
	}

	/// <summary>
	/// Try to get the cell index for a neighbor of specific
	/// <see cref="HexCoordinates"/>.
	/// </summary>
	/// <param name="coordinates"><see cref="HexCoordinates"/>
	/// of the cell.</param>
	/// <param name="neighborDirection"/><see cref="HexDirection"/>
	/// to neighbor.</param>
	/// <param name="cell">The cell index if it exists, otherwise -1.</param>
	/// <returns>Whether the cell index exists.</returns>
	public readonly bool TryGetNeighborCellIndex(
		HexCoordinates coordinates,
		HexDirection neighborDirection,
		out int neighborIndex) => TryGetCellIndex(
			coordinates.Step(neighborDirection, wrapSize),
			out neighborIndex);
}
