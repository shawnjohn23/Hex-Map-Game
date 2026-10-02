using System.IO;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Immutable three-component hexagonal coordinates.
/// </summary>
[System.Serializable]
public struct HexCoordinates
{
	private static readonly int2[] neighborOffsets = {
		new(0, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1)
	};

	[SerializeField]
	private int2 xz;

	/// <summary>
	/// X coordinate.
	/// </summary>
	public readonly int X => xz.x;

	/// <summary>
	/// Z coordinate.
	/// </summary>
	public readonly int Z => xz.y;

	/// <summary>
	/// Y coordinate, derived from X and Z.
	/// </summary>
	public readonly int Y => -X - Z;

	/// <summary>
	/// Offset coordinates.
	/// </summary>
	public readonly int2 OffsetCoordinates => new(X + Z / 2, Z);

	/// <summary>
	/// X position in hex space, where the distance between cell centers
	/// of east-west neighbors is one unit.
	/// </summary>
	public readonly float HexX => X + Z / 2 + ((Z & 1) == 0 ? 0f : 0.5f);

	/// <summary>
	/// Z position in hex space, where the distance between cell centers
	/// of east-west neighbors is one unit.
	/// </summary>
	public readonly float HexZ => Z * HexMetrics.outerToInner;

	public readonly int ColumnIndex => (X + Z / 2) / HexMetrics.chunkSizeX;

	/// <summary>
	/// Create hex coordinates.
	/// </summary>
	/// <param name="x">X coordinate.</param>
	/// <param name="z">Z coordinate.</param>
	public HexCoordinates(int x, int z) :
		this(new int2(x, z), HexMetrics.wrapSize) {}

	/// <summary>
	/// Create hex coordinates.
	/// </summary>
	/// <param name="xz">XZ coordinates.</param>
	/// <param name="wrapSize">
	/// Map wrap size for X dimension, zero if no wrapping.
	/// </param>
	public HexCoordinates(int2 xz, int wrapSize)
	{
		if (wrapSize > 0)
		{
			int oX = xz.x + xz.y / 2;
			if (oX < 0)
			{
				xz.x += wrapSize;
			}
			else if (oX >= wrapSize)
			{
				xz.x -= wrapSize;
			}
		}
		this.xz = xz;
	}

	/// <summary>
	/// Determine distance between this and another set of coordinates.
	/// Takes <see cref="HexMetrics.Wrapping"/> into account.
	/// </summary>
	/// <param name="other">Coordinate to determine distance to.</param>
	/// <returns>Distance in cells.</returns>
	public readonly int DistanceTo(HexCoordinates other)
	{
		int xy =
			(X < other.X ? other.X - X : X - other.X) +
			(Y < other.Y ? other.Y - Y : Y - other.Y);

		if (HexMetrics.Wrapping)
		{
			other.xz.x += HexMetrics.wrapSize;
			int xyWrapped =
				(X < other.X ? other.X - X : X - other.X) +
				(Y < other.Y ? other.Y - Y : Y - other.Y);
			if (xyWrapped < xy)
			{
				xy = xyWrapped;
			}
			else
			{
				other.xz.x -= 2 * HexMetrics.wrapSize;
				xyWrapped =
					(X < other.X ? other.X - X : X - other.X) +
					(Y < other.Y ? other.Y - Y : Y - other.Y);
				if (xyWrapped < xy)
				{
					xy = xyWrapped;
				}
			}
		}

		return (xy + (Z < other.Z ? other.Z - Z : Z - other.Z)) / 2;
	}

	/// <summary>
	/// Return (wrapped) coordinates after a single step in a given direction.
	/// </summary>
	/// <param name="direction">Step direction.</param>
	/// <returns>Coordinates.</returns>
	public readonly HexCoordinates Step(HexDirection direction) =>
		Step(direction, HexMetrics.wrapSize);

	/// <summary>
	/// Return (wrapped) coordinates after a single step in a given direction.
	/// </summary>
	/// <param name="direction">Step direction.</param>
	/// <param name="wrapSize">
	/// Map wrap size for X dimension, zero if no wrapping.
	/// </param>
	/// <returns>Coordinates.</returns>
	public readonly HexCoordinates Step(HexDirection direction, int wrapSize) =>
		new(xz + neighborOffsets[(int)direction], wrapSize);
	
	/// <summary>
	/// Create hex coordinates from array offset coordinates.
	/// </summary>
	/// <param name="x">X offset coordinate.</param>
	/// <param name="z">Z offset coordinate.</param>
	/// <returns>Hex coordinates.</returns>
	public static HexCoordinates FromOffsetCoordinates(int x, int z) =>
		new(x - z / 2, z);

	/// <summary>
	/// Create hex coordinates for the cell that contains a position.
	/// </summary>
	/// <param name="position">A 3D position assumed to lie
	/// inside the map.</param>
	/// <returns>Hex coordinates.</returns>
	public static HexCoordinates FromPosition(Vector3 position)
	{
		float x = position.x / HexMetrics.innerDiameter;
		float y = -x;

		float offset = position.z / (HexMetrics.outerRadius * 3f);
		x -= offset;
		y -= offset;

		int iX = Mathf.RoundToInt(x);
		int iY = Mathf.RoundToInt(y);
		int iZ = Mathf.RoundToInt(-x -y);

		if (iX + iY + iZ != 0)
		{
			float dX = Mathf.Abs(x - iX);
			float dY = Mathf.Abs(y - iY);
			float dZ = Mathf.Abs(-x -y - iZ);

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

	/// <summary>
	/// Create a string representation of the coordinates.
	/// </summary>
	/// <returns>A string of the form (X, Y, Z).</returns>
	public readonly override string ToString() =>
		"(" + X.ToString() + ", " + Y.ToString() + ", " + Z.ToString() + ")";

	/// <summary>
	/// Create a multi-line string representation of the coordinates.
	/// </summary>
	/// <returns>A string of the form X\nY\nZ\n.</returns>
	public readonly string ToStringOnSeparateLines() =>
		X.ToString() + "\n" + Y.ToString() + "\n" + Z.ToString();

	/// <summary>
	/// Save the coordinates.
	/// </summary>
	/// <param name="writer"><see cref="BinaryWriter"/> to use.</param>
	public readonly void Save(BinaryWriter writer)
	{
		writer.Write(X);
		writer.Write(Z);
	}

	/// <summary>
	/// Load coordinates.
	/// </summary>
	/// <param name="reader"><see cref="BinaryReader"/> to use.</param>
	/// <returns>The coordinates.</returns>
	public static HexCoordinates Load(BinaryReader reader)
	{
		HexCoordinates c;
		c.xz.x = reader.ReadInt32();
		c.xz.y = reader.ReadInt32();
		return c;
	}
}
